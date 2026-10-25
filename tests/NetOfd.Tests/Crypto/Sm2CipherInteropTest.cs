using System;
using System.IO;
using NetOfd.Core.Crypto;
using NetOfd.Crypto;
using NetOfd.Crypto.Integrity;
using NetOfd.Sign;
using NetOfd.Sign.Sm2Strut;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Xunit;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Tests.Crypto;

/// <summary>
/// SM2Cipher（GB/T 35276 §7.2）与证书加密互操作、PEM/PKCS12 工具测试。
/// 对应 Java: org.ofdrw.gm.sm2strut.SM2Cipher / org.ofdrw.gm.cert.*
/// </summary>
public class Sm2CipherInteropTest
{
    private static string ResPath(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Resources", name);

    private static byte[] EncryptC1C3C2(AsymmetricKeyParameter pub, byte[] data)
    {
        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(true, new ParametersWithRandom(pub, new SecureRandom()));
        return engine.ProcessBlock(data, 0, data.Length);
    }

    private static byte[] DecryptC1C3C2(AsymmetricKeyParameter priv, byte[] data)
    {
        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(false, priv);
        return engine.ProcessBlock(data, 0, data.Length);
    }

    /// <summary>SM2Cipher 与 C1C3C2 双向往返，且 DER 结构可解出明文。</summary>
    [Fact]
    public void Sm2Cipher_RoundTrip_WithRealEncryption()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        byte[] fek = new byte[16];
        new Random(42).NextBytes(fek);

        // 公钥加密（C1C3C2）→ 包装为 SM2Cipher → DER 编码
        byte[] c1c3c2 = EncryptC1C3C2(kp.Public, fek);
        byte[] der = Sm2Cipher.FromC1C3C2(c1c3c2).GetEncoded();

        // DER 应为 SEQUENCE
        Assert.IsAssignableFrom<Asn1Sequence>(Asn1Object.FromByteArray(der));

        // 解析 DER → 转回 C1C3C2 → 私钥解密恢复 FEK
        var parsed = Sm2Cipher.GetInstance(Asn1Object.FromByteArray(der));
        byte[] restored = parsed.ConvertC1C3C2();
        byte[] decrypted = DecryptC1C3C2(kp.Private, restored);

        Assert.Equal(fek, decrypted);
    }

    /// <summary>证书加密器写出的 EncryptedWK 必须是 SM2Cipher DER 结构（对齐 Java），且可被解密器还原。</summary>
    [Fact]
    public void Cert_Encrypt_WritesSm2CipherDer_AndDecrypts()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2("格式验证用户", kp);
        byte[] fek = new byte[16];
        byte[] iv = new byte[16];
        new Random(7).NextBytes(fek);
        new Random(8).NextBytes(iv);

        var encryptor = new UserCertEncryptor("格式验证用户", cert);
        UserInfo ui = encryptor.Encrypt(fek, iv);

        // EncryptedWK 应为 ASN.1 SEQUENCE（SM2Cipher）
        var asn1 = Asn1Object.FromByteArray(ui.GetEncryptedWK());
        Assert.IsAssignableFrom<Asn1Sequence>(asn1);
        var cipher = Sm2Cipher.GetInstance(asn1);
        Assert.Equal(32, cipher.Hash.GetOctets().Length);

        // 解密器应能从 DER 结构恢复 FEK
        var decryptor = new UserCertDecryptor((ECPrivateKeyParameters)kp.Private);
        DecryptResult result = decryptor.Decrypt(ui);
        Assert.Equal(fek, result.Fek);
        Assert.Equal(iv, result.Iv);
    }

    /// <summary>兼容旧版：裸 C1C3C2 格式的 EncryptedWK 也应能被解密器还原。</summary>
    [Fact]
    public void Cert_Decrypt_LegacyRawC1C3C2()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        byte[] fek = new byte[16];
        byte[] iv = new byte[16];
        new Random(9).NextBytes(fek);
        new Random(10).NextBytes(iv);

        var ui = new UserInfo().SetUserName("旧版用户").SetIVValue(iv).SetEncryptedWK(EncryptC1C3C2(kp.Public, fek));
        var decryptor = new UserCertDecryptor((ECPrivateKeyParameters)kp.Private);
        DecryptResult result = decryptor.Decrypt(ui);
        Assert.Equal(fek, result.Fek);
    }

    /// <summary>PEM 加载：加载 Java 测试资源中的 SM2 私钥与证书并用于完整性保护。</summary>
    [Fact]
    public void Pem_LoadJavaTestKeyAndCert()
    {
        var key = CertUtil.LoadPemPrivateKey(ResPath("sign_key.pem"));
        var cert = CertUtil.LoadPemCert(ResPath("sign_cert.pem"));

        Assert.True(key.IsPrivate);
        Assert.IsType<ECPrivateKeyParameters>(key);
        Assert.Contains("Test Certificate", cert.SubjectDN.ToString());

        // 公私钥匹配：用加载的密钥对做完整性保护-校验回路
        byte[] sample = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", "sample.ofd"));
        var protectedBytes = OfdIntegrity.Protect(sample, new GmProtectSigner(key, cert));
        Assert.True(new OfdIntegrityVerifier().Integrity(protectedBytes, new GmProtectVerifier()));
    }

    /// <summary>PKCS12 工具：写入后应能读回私钥、用户证书与证书链。</summary>
    [Fact]
    public void Pkcs12_ReadPrvKeyAndUserCert()
    {
        const string pwd = "p12pass";
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2("P12用户", kp);

        // 构造 P12
        var store = new Pkcs12StoreBuilder().Build();
        store.SetKeyEntry("user", new AsymmetricKeyEntry(kp.Private), new[] { new X509CertificateEntry(cert) });
        byte[] p12Bytes;
        using (var ms = new MemoryStream())
        {
            store.Save(ms, pwd.ToCharArray(), new SecureRandom());
            p12Bytes = ms.ToArray();
        }

        using (var fs = new MemoryStream(p12Bytes))
        {
            var prv = Pkcs12Tools.ReadPrvKey(fs, pwd);
            Assert.True(prv.IsPrivate);
        }

        using (var fs = new MemoryStream(p12Bytes))
        {
            X509Certificate userCert = Pkcs12Tools.ReadUserCert(fs, pwd);
            Assert.Equal(cert.SerialNumber, userCert.SerialNumber);
        }
    }
}