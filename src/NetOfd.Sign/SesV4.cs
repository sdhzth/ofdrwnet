using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// SES_V4 / SES_V5（GM/T 0031-2014 / GB/T 38540-2020 安全电子签章密码技术规范 V4/V5）
/// 电子签章数据编解码与验签。
/// <para>
/// 结构（DER）：
/// <code>
/// SES_Signature ::= SEQUENCE {
///     toSign         TBS_Sign,
///     cert           OCTET STRING,
///     signatureAlgID OBJECT IDENTIFIER,
///     signature      BIT STRING,
///     timeStamp      [0] EXPLICIT BIT STRING OPTIONAL
/// }
/// TBS_Sign ::= SEQUENCE {
///     version      INTEGER,             -- 4 或 5
///     eseal        SESeal,              -- 电子印章（必填）
///     timeInfo     GeneralizedTime,
///     dataHash     BIT STRING,          -- Signature.xml 的杂凑值
///     propertyInfo IA5String,           -- 签章属性信息
///     extDatas     [0] EXPLICIT OPTIONAL
/// }
/// </code>
/// </para>
/// 参考 ofdrw-gm 的 ses.v4 / ses.v5 与 ofdrw-sign 的 SESV4Container / SESV5Container。
/// V5 与 V4 的 ASN.1 结构一致，仅版本号不同。
/// </summary>
public static class SesV4
{
    /// <summary>构建 TBS_Sign（V4/V5）DER SEQUENCE。</summary>
    /// <param name="version">签章版本（4 或 5）。</param>
    /// <param name="esealDer">电子印章 SESeal DER 编码（必填）。</param>
    /// <param name="dataHash">Signature.xml 的 SM3 杂凑值。</param>
    /// <param name="timeInfo">签章时间（GeneralizedTime 格式字符串）。</param>
    /// <param name="propertyInfo">签章属性信息（如签名文件包内路径）。</param>
    public static DerSequence BuildTbsSeq(int version, byte[] esealDer, byte[] dataHash, string timeInfo, string propertyInfo)
    {
        var tbs = new Asn1EncodableVector
        {
            new DerInteger(version),
            Asn1Object.FromByteArray(esealDer),
            new DerGeneralizedTime(timeInfo),
            new DerBitString(dataHash),
            new DerIA5String(propertyInfo)
        };
        return new DerSequence(tbs);
    }

    /// <summary>构建 SES_Signature（V4）DER 编码的签章值。</summary>
    /// <param name="tbsSeq">已构建的 TBS_Sign（V4）序列。</param>
    /// <param name="certDer">签章人证书 DER 编码。</param>
    /// <param name="sm2Signature">对 TBS_Sign DER 的 SM2 签名值。</param>
    /// <param name="timeStamp">时间戳令牌 DER（可选，写入 [0] 标签）。</param>
    public static byte[] BuildSignedValue(DerSequence tbsSeq, byte[] certDer, byte[] sm2Signature, byte[]? timeStamp = null)
    {
        var outer = new Asn1EncodableVector
        {
            tbsSeq,
            new DerOctetString(certDer),
            GMObjectIdentifiers.sm2sign_with_sm3,
            new DerBitString(sm2Signature)
        };
        if (timeStamp is { Length: > 0 })
            outer.Add(new DerTaggedObject(true, 0, new DerBitString(timeStamp)));
        return new DerSequence(outer).GetEncoded();
    }

    /// <summary>构建 SES_Signature（V4/V5）DER 编码的签章值。</summary>
    /// <param name="version">签章版本（4 或 5）。</param>
    /// <param name="esealDer">电子印章 SESeal DER 编码。</param>
    /// <param name="dataHash">Signature.xml 的 SM3 杂凑值。</param>
    /// <param name="timeInfo">签章时间（GeneralizedTime 格式字符串）。</param>
    /// <param name="propertyInfo">签章属性信息。</param>
    /// <param name="certDer">签章人证书 DER 编码。</param>
    /// <param name="sm2Signature">对 TBS_Sign DER 的 SM2 签名值。</param>
    /// <param name="timeStamp">时间戳令牌 DER（可选）。</param>
    public static byte[] BuildSignedValue(
        int version, byte[] esealDer, byte[] dataHash, string timeInfo, string propertyInfo,
        byte[] certDer, byte[] sm2Signature, byte[]? timeStamp = null)
    {
        var tbs = BuildTbsSeq(version, esealDer, dataHash, timeInfo, propertyInfo);
        return BuildSignedValue(tbs, certDer, sm2Signature, timeStamp);
    }

    /// <summary>SES_V4 解析与验签结果。</summary>
    public sealed class ParsedSignedValue
    {
        public int Version { get; init; }
        public byte[] EsealDer { get; init; } = Array.Empty<byte>();
        public string TimeInfo { get; init; } = string.Empty;
        public byte[] DataHash { get; init; } = Array.Empty<byte>();
        public string PropertyInfo { get; init; } = string.Empty;
        public X509Certificate Cert { get; init; } = null!;
        public byte[] TbsDer { get; init; } = Array.Empty<byte>();
        public byte[] Signature { get; init; } = Array.Empty<byte>();
    }

    /// <summary>解析 SES_Signature（V4）DER 并做 SM2 验签（不含对原文杂凑的校验，由调用方负责）。</summary>
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
            throw new SignException("SES_Signature(V4) 结构解析失败", ex);
        }

        if (seq.Count < 4)
            throw new SignException("SES_Signature(V4) 结构不完整");

        var tbsSeq = Asn1Sequence.GetInstance(seq[0]);
        var certDer = DerOctetString.GetInstance(seq[1]).GetOctets();
        var algOid = DerObjectIdentifier.GetInstance(seq[2]);
        var signature = DerBitString.GetInstance(seq[3]).GetBytes();

        if (!GMObjectIdentifiers.sm2sign_with_sm3.Equals(algOid))
            throw new SignException($"不支持的签名算法 OID: {algOid}");

        // TBS_Sign(V4) 字段：version, eseal, timeInfo, dataHash, propertyInfo[, extDatas]
        int idx = 0;
        var version = DerInteger.GetInstance(tbsSeq[idx++]).Value.IntValue;
        var esealDer = tbsSeq[idx++].GetEncoded();
        var timeInfo = DerGeneralizedTime.GetInstance(tbsSeq[idx++]).TimeString;
        var dataHash = DerBitString.GetInstance(tbsSeq[idx++]).GetBytes();
        var propertyInfo = DerIA5String.GetInstance(tbsSeq[idx++]).GetString();

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
            EsealDer = esealDer,
            TimeInfo = timeInfo,
            DataHash = dataHash,
            PropertyInfo = propertyInfo,
            Cert = cert,
            TbsDer = tbsDer,
            Signature = signature
        };
    }
}