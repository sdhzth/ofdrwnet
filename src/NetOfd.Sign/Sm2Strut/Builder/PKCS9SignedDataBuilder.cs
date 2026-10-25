using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using Attribute = Org.BouncyCastle.Asn1.Cms.Attribute;

namespace NetOfd.Sign.Sm2Strut.Builder;

/// <summary>
/// 签名数据类型构造器（符合 GB/T 35275 第 8 章 signedData）。
/// <para>签名对象为 signerInfos 中的 authenticatedAttributes 字段（内部结构为 PKCS#9）。</para>
/// 参考 ofdrw-gm 的 sm2strut.builder.PKCS9SignedDataBuilder。
/// </summary>
public static class PKCS9SignedDataBuilder
{
    /// <summary>组装签名数据类型（签名对象为 PKCS#9 authenticatedAttributes）。</summary>
    /// <param name="plaintext">需要保护的原文。</param>
    /// <param name="signCertificate">签名使用的证书。</param>
    /// <param name="keyPair">SM2 签名密钥对。</param>
    /// <param name="extCertArr">扩展证书序列（如根证书等，可选）。</param>
    public static SignedData SignedData(
        byte[] plaintext,
        Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair keyPair,
        X509Certificate signCertificate,
        IReadOnlyList<X509Certificate>? extCertArr = null)
    {
        if (plaintext == null || plaintext.Length == 0)
            throw new ArgumentException("签名原文(plaintext)为空");
        if (signCertificate == null)
            throw new ArgumentException("证书(signCert)为空");
        if (keyPair == null)
            throw new ArgumentException("签名密钥对(keyPair)为空");

        // 消息摘要算法标识符的集合，固定 SM3 算法
        var digestAlgorithms = new DerSet(new AlgorithmIdentifier(OIDs.Sm3));
        // 待签名数据内容（PKCS#9 中不携带原文内容）
        var contentInfo = new ContentInfo { ContentType = OIDs.Data };
        // 计算待保护内容的摘要
        byte[] digest = GmCrypto.Sm3(plaintext);

        int len = 1;
        if (extCertArr != null) len += extCertArr.Count;
        var certArr = new Asn1Encodable[len];
        var signerCert = signCertificate.CertificateStructure;
        certArr[0] = signerCert;
        if (extCertArr != null)
            for (int i = 0; i < extCertArr.Count; i++)
                certArr[i + 1] = extCertArr[i].CertificateStructure;
        var certificates = new DerSet(certArr);

        // 签名证书 ID
        var isn = new IssuerAndSerialNumber(signerCert.Issuer, signerCert.SerialNumber);
        // 生成签名者信息（签名对象为 PKCS#9 属性集合）
        var signerInfo = Sign(digest, keyPair, isn);

        var signerInfos = new DerSet(signerInfo);
        return new SignedData(digestAlgorithms, contentInfo, certificates, signerInfos);
    }

    /// <summary>构造签名者信息（对 PKCS#9 属性集合签名）。</summary>
    /// <param name="digest">原文摘要（message-digest 属性）。</param>
    /// <param name="keyPair">SM2 签名密钥对。</param>
    /// <param name="isn">签名者公钥证书的颁发者与序列号。</param>
    public static SignerInfo Sign(
        byte[] digest, Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair keyPair, IssuerAndSerialNumber isn)
    {
        // PKCS#9 属性：contentType / signingTime / messageDigest
        var contentType = new Attribute(CmsAttributes.ContentType, new DerSet(OIDs.Data));
        var signingTime = new Attribute(CmsAttributes.SigningTime,
            new DerSet(new DerUtcTime(DateTime.UtcNow.ToString("yyMMddHHmmss") + "Z")));
        var messageDigest = new Attribute(CmsAttributes.MessageDigest, new DerSet(new DerOctetString(digest)));

        // 构造 authenticatedAttributes（SET OF Attribute）
        var authenticatedAttributes = new DerSet(
            new Asn1Encodable[] { contentType, signingTime, messageDigest });

        // 对 authenticatedAttributes 的 DER 编码进行签名
        var attrsDer = authenticatedAttributes.GetEncoded();
        var signature = GmCrypto.Sm2Sign(attrsDer, keyPair.Private);

        var res = new SignerInfo(
            isn,
            new AlgorithmIdentifier(OIDs.Sm3),      // 固定 SM3 算法
            new AlgorithmIdentifier(OIDs.Sm2Sign),  // 固定 SM2 椭圆曲线数字签名算法
            new DerOctetString(signature))
        {
            AuthenticatedAttributes = authenticatedAttributes
        };
        return res;
    }
}