using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// 证书辅助：生成自签名 SM2 证书（测试/演示用），以及证书编码/解析。
/// 注意：符合国密标准的电子签章应使用具有型号证书的设备产生的证书，
/// 此处的自签名证书仅用于本地功能验证。
/// </summary>
public static class CertUtil
{
    /// <summary>
    /// 生成自签名 SM2 证书。
    /// </summary>
    /// <param name="commonName">证书主题/签发者通用名（CN）。</param>
    /// <param name="keyPair">SM2 密钥对（证书公钥与签名私钥）。</param>
    /// <returns>DER 编码的 X509 证书。</returns>
    public static X509Certificate GenerateSelfSignedSm2(string commonName, AsymmetricCipherKeyPair keyPair)
    {
        var random = new SecureRandom();
        var generator = new X509V3CertificateGenerator();

        // 序列号
        var serial = BigInteger.ValueOf(DateTime.UtcNow.Ticks);
        generator.SetSerialNumber(serial);

        var dn = new X509Name($"CN={commonName}");
        generator.SetSubjectDN(dn);
        generator.SetIssuerDN(dn);

        var notBefore = DateTime.UtcNow.AddDays(-1);
        var notAfter = DateTime.UtcNow.AddYears(10);
        generator.SetNotBefore(notBefore);
        generator.SetNotAfter(notAfter);

        generator.SetPublicKey(keyPair.Public);

        generator.AddExtension(X509Extensions.KeyUsage, true,
            new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.NonRepudiation));
        generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));

        // 使用 SM3withSM2 进行证书自签名
        var sigFactory = new Asn1SignatureFactory("SM3withSM2", keyPair.Private, random);
        return generator.Generate(sigFactory);
    }

    /// <summary>将 BouncyCastle 证书转换为 .NET X509Certificate2（用于读取主题等信息）。</summary>
    public static X509Certificate2 ToDotNet(X509Certificate cert)
    {
        return X509CertificateLoader.LoadCertificate(cert.GetEncoded());
    }

    /// <summary>从 DER 字节解析 BouncyCastle 证书。</summary>
    public static X509Certificate ParseDer(byte[] der)
    {
        return new X509Certificate(der);
    }

    /// <summary>从 DER 字节解析 .NET X509Certificate2。</summary>
    public static X509Certificate2 ParseDerToDotNet(byte[] der)
    {
        return X509CertificateLoader.LoadCertificate(der);
    }

    /// <summary>从 PEM 文件加载 SM2 私钥（支持 "EC PRIVATE KEY" / "PRIVATE KEY" 格式）。对应 Java: PEMLoader.loadPrivateKey。</summary>
    public static AsymmetricKeyParameter LoadPemPrivateKey(string pemFile)
    {
        using var reader = new StreamReader(pemFile);
        return LoadPemPrivateKey(reader);
    }

    /// <summary>从 PEM 文本流加载 SM2 私钥。</summary>
    public static AsymmetricKeyParameter LoadPemPrivateKey(TextReader reader)
    {
        var pemReader = new Org.BouncyCastle.OpenSsl.PemReader(reader);
        var obj = pemReader.ReadObject() ?? throw new IOException("PEM 文件中未找到私钥对象");
        if (obj is AsymmetricCipherKeyPair kp) return kp.Private;
        if (obj is AsymmetricKeyParameter akp && akp.IsPrivate) return akp;
        throw new IOException($"PEM 对象类型无法识别为私钥：{obj.GetType().Name}");
    }

    /// <summary>从 PEM 文件加载证书（"CERTIFICATE" 块）。对应 Java: PEMLoader.loadCert。</summary>
    public static X509Certificate LoadPemCert(string pemFile)
    {
        using var reader = new StreamReader(pemFile);
        return LoadPemCert(reader);
    }

    /// <summary>从 PEM 文本流加载证书。</summary>
    public static X509Certificate LoadPemCert(TextReader reader)
    {
        var pemReader = new Org.BouncyCastle.OpenSsl.PemReader(reader);
        var obj = pemReader.ReadObject() ?? throw new IOException("PEM 文件中未找到证书对象");
        return obj switch
        {
            X509Certificate cert => cert,
            X509CertificateStructure structure => new X509Certificate(structure),
            Asn1Sequence seq => new X509Certificate(X509CertificateStructure.GetInstance(seq)),
            _ => throw new IOException($"PEM 对象类型无法识别为证书：{obj.GetType().Name}")
        };
    }
}
