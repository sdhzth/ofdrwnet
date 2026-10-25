using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// SES_V1（GM/T 0031-2014 安全电子签章密码技术规范）电子签章数据编解码与验签。
/// <para>
/// 结构（DER）：
/// <code>
/// SES_Signature ::= SEQUENCE {
///     toSign   TBS_Sign,
///     signature BIT STRING
/// }
/// TBS_Sign ::= SEQUENCE {
///     version          INTEGER,
///     eseal            [0] EXPLICIT SESeal OPTIONAL,  -- 无印章时填 DER NULL
///     timeInfo         BIT STRING,
///     dataHash         BIT STRING,                    -- Signature.xml 的杂凑值
///     propertyInfo     IA5String,                     -- 签章属性信息
///     cert             OCTET STRING,                  -- 签章人证书 DER
///     signatureAlgorithm OBJECT IDENTIFIER           -- sm2sign_with_sm3
/// }
/// </code>
/// </para>
/// 参考 ofdrw-gm 的 SES_Signature / TBS_Sign 与 ofdrw-sign 的 SESV1Container。
/// </summary>
public static class SesV1
{
    /// <summary>构建 TBS_Sign DER SEQUENCE（不含外层签名）。</summary>
    public static DerSequence BuildTbsSeq(
        int version,
        byte[] dataHash,
        byte[] timeInfoBytes,
        string propertyInfo,
        byte[] certDer,
        Asn1Encodable? seal = null)
    {
        var tbs = new Asn1EncodableVector
        {
            new DerInteger(version),
            seal ?? DerNull.Instance,
            new DerBitString(timeInfoBytes),
            new DerBitString(dataHash),
            new DerIA5String(propertyInfo),
            new DerOctetString(certDer),
            GMObjectIdentifiers.sm2sign_with_sm3
        };
        return new DerSequence(tbs);
    }

    /// <summary>构建 SES_Signature DER 编码的签章值（给定已构建的 TBS_Sign）。</summary>
    public static byte[] BuildSignedValue(DerSequence tbsSeq, byte[] sm2Signature)
    {
        var outer = new Asn1EncodableVector
        {
            tbsSeq,
            new DerBitString(sm2Signature)
        };
        return new DerSequence(outer).GetEncoded();
    }

    /// <summary>构建 SES_Signature DER 编码的签章值。</summary>
    /// <param name="version">版本（通常为 1）。</param>
    /// <param name="dataHash">待签原文（Signature.xml）的 SM3 杂凑值。</param>
    /// <param name="timeInfoBytes">签章时间信息字节（如 "yyyy-MM-dd HH:mm:ss" 的 UTF-8）。</param>
    /// <param name="propertyInfo">原文属性信息（如签名文件包内路径）。</param>
    /// <param name="certDer">签章人证书 DER 编码。</param>
    /// <param name="sm2Signature">对 TBS_Sign DER 的 SM2 签名值。</param>
    /// <param name="seal">电子印章 DER（可选，无则置为 DER NULL）。</param>
    public static byte[] BuildSignedValue(
        int version,
        byte[] dataHash,
        byte[] timeInfoBytes,
        string propertyInfo,
        byte[] certDer,
        byte[] sm2Signature,
        Asn1Encodable? seal = null)
    {
        var tbsSeq = BuildTbsSeq(version, dataHash, timeInfoBytes, propertyInfo, certDer, seal);
        return BuildSignedValue(tbsSeq, sm2Signature);
    }

    /// <summary>SES_V1 解析与验签结果。</summary>
    public sealed class ParsedSignedValue
    {
        public int Version { get; init; }
        public byte[] DataHash { get; init; } = Array.Empty<byte>();
        public byte[] TimeInfo { get; init; } = Array.Empty<byte>();
        public string PropertyInfo { get; init; } = string.Empty;
        public X509Certificate Cert { get; init; } = null!;
        public byte[] TbsDer { get; init; } = Array.Empty<byte>();
        public byte[] Signature { get; init; } = Array.Empty<byte>();
    }

    /// <summary>解析 SES_Signature DER 并做 SM2 验签（不含对原文杂凑的校验，由调用方负责）。</summary>
    /// <exception cref="SignException">结构非法或签名验证失败。</exception>
    public static ParsedSignedValue ParseAndVerify(byte[] sesSignatureDer, byte[] signatureXmlBytes)
    {
        Asn1Sequence seq;
        try
        {
            seq = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(sesSignatureDer));
        }
        catch (Exception ex)
        {
            throw new SignException("SES_Signature 结构解析失败", ex);
        }

        if (seq.Count != 2)
            throw new SignException("SES_Signature 应为包含 toSign 与 signature 的 SEQUENCE");

        var tbsSeq = Asn1Sequence.GetInstance(seq[0]);
        var signature = DerBitString.GetInstance(seq[1]).GetBytes();

        // TBS_Sign 字段
        int idx = 0;
        var version = DerInteger.GetInstance(tbsSeq[idx++]).Value.IntValue;
        // eseal 位置（可能为空/未使用），跳过
        idx++;
        var timeInfo = DerBitString.GetInstance(tbsSeq[idx++]).GetBytes();
        var dataHash = DerBitString.GetInstance(tbsSeq[idx++]).GetBytes();
        var propertyInfo = DerIA5String.GetInstance(tbsSeq[idx++]).GetString();
        var certDer = DerOctetString.GetInstance(tbsSeq[idx++]).GetOctets();
        var algOid = DerObjectIdentifier.GetInstance(tbsSeq[idx++]);

        if (!GMObjectIdentifiers.sm2sign_with_sm3.Equals(algOid))
            throw new SignException($"不支持的签名算法 OID: {algOid}");

        X509Certificate cert;
        try
        {
            cert = new X509Certificate(certDer);
        }
        catch (Exception ex)
        {
            throw new SignException("签章人证书解析失败", ex);
        }

        var tbsDer = tbsSeq.GetEncoded();

        // 校验原文杂凑
        var actualHash = GmCrypto.Sm3(signatureXmlBytes);
        if (!actualHash.AsSpan().SequenceEqual(dataHash))
            throw new SignException("Signature.xml 杂凑值与签章中记录的不一致（文件可能已被篡改）");

        // SM2 验签
        var pub = cert.GetPublicKey();
        if (!GmCrypto.Sm2Verify(tbsDer, signature, pub))
            throw new SignException("SM2 签名验证失败（签章无效或证书不匹配）");

        return new ParsedSignedValue
        {
            Version = version,
            DataHash = dataHash,
            TimeInfo = timeInfo,
            PropertyInfo = propertyInfo,
            Cert = cert,
            TbsDer = tbsDer,
            Signature = signature
        };
    }
}
