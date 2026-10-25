using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign.Sm2Strut.Builder;

/// <summary>
/// 签名数据结构建造器：将 SM2 签名封装为符合 GB/T 35275 第 8 章的 signedData。
/// 参考 ofdrw-gm 的 sm2strut.builder.SignedDataBuilder。
/// </summary>
public static class SignedDataBuilder
{
    /// <summary>封装签名数据结构（单证书）。</summary>
    public static SignedData SignedData(byte[] plaintext, byte[] signature, X509Certificate certificate)
    {
        if (plaintext == null || plaintext.Length == 0)
            throw new ArgumentException("签名原文(plaintext)为空");
        if (signature == null || signature.Length == 0)
            throw new ArgumentException("签名值(signature)为空");
        if (certificate == null)
            throw new ArgumentException("证书(certificate)为空");
        var certSigArr = new List<CertSigHolder> { new(signature, certificate) };
        return SignedData(plaintext, certSigArr, null);
    }

    /// <summary>封装签名数据结构（多证书，可附加 CA 证书）。</summary>
    public static SignedData SignedData(byte[] plaintext, IReadOnlyList<CertSigHolder> certSigArr, IReadOnlyList<X509Certificate>? extCertArr)
    {
        if (plaintext == null || plaintext.Length == 0)
            throw new ArgumentException("签名原文(plaintext)为空");
        if (certSigArr == null || certSigArr.Count == 0)
            throw new ArgumentException("证书、签名值列表(signature)为空");

        // 信息摘要算法标识符集合，固定 SM3 算法
        var digestAlgorithms = new DerSet(new AlgorithmIdentifier(OIDs.Sm3));

        // 待签名数据
        var contentInfo = new ContentInfo(OIDs.Data, new DerOctetString(plaintext));

        int len = certSigArr.Count;
        if (extCertArr != null) len += extCertArr.Count;

        var certArr = new Asn1Encodable[len];
        int i = 0;
        foreach (var holder in certSigArr)
            certArr[i++] = holder.GetAsn1Cert();
        if (extCertArr != null)
            foreach (var c in extCertArr)
                certArr[i++] = c.CertificateStructure;
        var certificates = new DerSet(certArr);

        var signers = new Asn1Encodable[certSigArr.Count];
        for (int j = 0; j < certSigArr.Count; j++)
            signers[j] = Sm2Signer(certSigArr[j].Signature, certSigArr[j].GetAsn1Cert());
        var signerInfos = new DerSet(signers);

        return new SignedData(digestAlgorithms, contentInfo, certificates, signerInfos);
    }

    /// <summary>构建签名者信息（SM2 椭圆曲线数字签名算法，SM3 摘要）。</summary>
    public static SignerInfo Sm2Signer(byte[] signature, X509CertificateStructure certificate)
    {
        var issuerAndSerialNumber = new IssuerAndSerialNumber(certificate.Issuer, certificate.SerialNumber);
        return new SignerInfo(
            issuerAndSerialNumber,
            new AlgorithmIdentifier(OIDs.Sm3),      // 固定 SM3 算法
            new AlgorithmIdentifier(OIDs.Sm2Sign),  // 固定 SM2 椭圆曲线数字签名算法
            new DerOctetString(signature));
    }
}