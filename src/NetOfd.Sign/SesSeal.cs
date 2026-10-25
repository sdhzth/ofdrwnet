using System.Globalization;
using System.Text;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// 电子印章（SESeal）解析与验签。
/// <para>
/// SES_V1 与 SES_V4 的 SESeal 结构一致：
/// <code>
/// SESeal ::= SEQUENCE {
///     eSealInfo   SES_SealInfo,
///     cert        OCTET STRING,       -- 印章证书
///     signAlgID   OBJECT IDENTIFIER,
///     signedValue BIT STRING          -- 对 eSealInfo DER 的 SM2 签名
/// }
/// SES_SealInfo ::= SEQUENCE {
///     header   SES_Header { id "ES", version INTEGER, vid },
///     esID     IA5String,
///     property SES_ESPropertyInfo,    -- 名称、类型、证书列表、有效期
///     picture  SES_ESPictrueInfo,     -- 印章图片
///     [extDatas]
/// }
/// </code>
/// 支持从 .esl / .DER 文件读取印章并用于 OFD 签章。
/// </para>
/// </summary>
public sealed class SealInfo
{
    /// <summary>印章结构版本（1 或 4）。</summary>
    public int Version { get; set; }
    /// <summary>头标识（"ES"）。</summary>
    public string HeaderId { get; set; } = string.Empty;
    /// <summary>印章版本标识。</summary>
    public string Vid { get; set; } = string.Empty;
    /// <summary>印章唯一标识。</summary>
    public string EsId { get; set; } = string.Empty;
    /// <summary>印章类型（1-单位，2-个人，3-其他）。</summary>
    public int Type { get; set; }
    /// <summary>印章名称。</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>签名证书信息类型（1-证书列表）。</summary>
    public int CertListType { get; set; }
    /// <summary>签名证书列表（DER 编码）。</summary>
    public IReadOnlyList<byte[]> Certs { get; set; } = Array.Empty<byte[]>();
    public DateTime? CreateDate { get; set; }
    public DateTime? ValidStart { get; set; }
    public DateTime? ValidEnd { get; set; }
    /// <summary>GeneralizedTime 原始字符串（保留时区，用于精确再编码）。</summary>
    public string? RawCreateDate { get; set; }
    public string? RawValidStart { get; set; }
    public string? RawValidEnd { get; set; }
    /// <summary>印章图片类型。</summary>
    public string PictureType { get; set; } = string.Empty;
    /// <summary>印章图片数据。</summary>
    public byte[] PictureData { get; set; } = Array.Empty<byte>();
    public int PictureWidth { get; set; }
    public int PictureHeight { get; set; }
    /// <summary>印章证书（DER）。</summary>
    public byte[] CertDer { get; set; } = Array.Empty<byte>();
    /// <summary>印章签名算法 OID。</summary>
    public string SignAlgId { get; set; } = string.Empty;
    /// <summary>印章签名值。</summary>
    public byte[] SignedValue { get; set; } = Array.Empty<byte>();
    /// <summary>eSealInfo 的 DER 编码（签名原文）。</summary>
    public byte[] EsealInfoDer { get; set; } = Array.Empty<byte>();

    /// <summary>印章证书解析为 X509Certificate。</summary>
    public X509Certificate Cert
    {
        get
        {
            try
            {
                return new X509Certificate(CertDer);
            }
            catch (Exception ex)
            {
                throw new SignException("印章证书解析失败", ex);
            }
        }
    }

    /// <summary>验印章自身签名（signedValue 对 eSealInfo DER 的 SM2 验签）。</summary>
    /// <returns>验签是否通过。</returns>
    public bool Verify()
    {
        if (GMObjectIdentifiers.sm2sign_with_sm3.Id != SignAlgId)
            return false;
        return GmCrypto.Sm2Verify(EsealInfoDer, SignedValue, Cert.GetPublicKey());
    }

    /// <summary>将印章重新编码为 SESeal DER（解析→修改→再编码的完整往返）。</summary>
    public byte[] ToDer()
    {
        // eSealInfo
        var header = new DerSequence(
            new DerIA5String(HeaderId),
            new DerInteger(Version),
            new DerIA5String(Vid));
        var picture = new DerSequence(
            new DerIA5String(PictureType),
            new DerOctetString(PictureData),
            new DerInteger(PictureWidth),
            new DerInteger(PictureHeight));
        var certListSeq = new Asn1EncodableVector(Certs.Count);
        foreach (var c in Certs) certListSeq.Add(new DerOctetString(c));
        var property = new DerSequence(
            new DerInteger(Type),
            new DerUtf8String(Name),
            new DerInteger(CertListType),
            new DerSequence(certListSeq),
            new DerGeneralizedTime(RawCreateDate ?? ToGt(CreateDate)),
            new DerGeneralizedTime(RawValidStart ?? ToGt(ValidStart)),
            new DerGeneralizedTime(RawValidEnd ?? ToGt(ValidEnd)));
        var sealInfo = new DerSequence(
            header,
            new DerIA5String(EsId),
            property,
            picture);
        var sealInfoDer = sealInfo.GetEncoded();

        return new DerSequence(
            sealInfo,
            new DerOctetString(CertDer),
            new DerObjectIdentifier(SignAlgId),
            new DerBitString(SignedValue)).GetEncoded();
    }

    private static string ToGt(DateTime? dt)
        => (dt ?? DateTime.Now).ToString("yyyyMMddHHmmss");
}

/// <summary>电子印章解析器。</summary>
public static class SesSeal
{
    /// <summary>解析 SESeal DER 数据。</summary>
    /// <exception cref="SignException">结构非法。</exception>
    public static SealInfo Parse(byte[] sealDer)
    {
        Asn1Sequence seq;
        try
        {
            seq = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(sealDer));
        }
        catch (Exception ex)
        {
            throw new SignException("电子印章(SESeal)结构解析失败", ex);
        }

        if (seq.Count < 4)
            throw new SignException("电子印章(SESeal)结构不完整");

        // eSealInfo
        var sealInfoSeq = Asn1Sequence.GetInstance(seq[0]);
        var sealInfoDer = sealInfoSeq.GetEncoded();

        // header: { id "ES", version INTEGER, vid }
        var headerSeq = Asn1Sequence.GetInstance(sealInfoSeq[0]);
        var headerId = DerIA5String.GetInstance(headerSeq[0]).GetString();
        var version = DerInteger.GetInstance(headerSeq[1]).Value.IntValue;
        var vid = headerSeq.Count > 2 ? DerIA5String.GetInstance(headerSeq[2]).GetString() : string.Empty;

        // esID
        var esId = DerIA5String.GetInstance(sealInfoSeq[1]).GetString();

        // property
        var propSeq = Asn1Sequence.GetInstance(sealInfoSeq[2]);
        var type = DerInteger.GetInstance(propSeq[0]).Value.IntValue;
        var name = DerUtf8String.GetInstance(propSeq[1]).GetString();
        var certListType = DerInteger.GetInstance(propSeq[2]).Value.IntValue;
        var certs = new List<byte[]>();
        if (certListType == 1)
        {
            var certListSeq = Asn1Sequence.GetInstance(propSeq[3]);
            for (int i = 0; i < certListSeq.Count; i++)
                certs.Add(DerOctetString.GetInstance(certListSeq[i]).GetOctets());
        }
        var createRaw = DerGeneralizedTime.GetInstance(propSeq[4]).TimeString;
        var validStartRaw = DerGeneralizedTime.GetInstance(propSeq[5]).TimeString;
        var validEndRaw = DerGeneralizedTime.GetInstance(propSeq[6]).TimeString;
        var createDate = ParseGeneralizedTime(createRaw);
        var validStart = ParseGeneralizedTime(validStartRaw);
        var validEnd = ParseGeneralizedTime(validEndRaw);

        // picture
        var pictureSeq = Asn1Sequence.GetInstance(sealInfoSeq[3]);
        var pictureType = DerIA5String.GetInstance(pictureSeq[0]).GetString();
        var pictureData = DerOctetString.GetInstance(pictureSeq[1]).GetOctets();
        var pictureW = DerInteger.GetInstance(pictureSeq[2]).Value.IntValue;
        var pictureH = DerInteger.GetInstance(pictureSeq[3]).Value.IntValue;

        // cert / signAlgID / signedValue
        var certDer = DerOctetString.GetInstance(seq[1]).GetOctets();
        var signAlgId = DerObjectIdentifier.GetInstance(seq[2]).Id;
        var signedValue = DerBitString.GetInstance(seq[3]).GetBytes();

        return new SealInfo
        {
            Version = version,
            HeaderId = headerId,
            Vid = vid,
            EsId = esId,
            Type = type,
            Name = name,
            CertListType = certListType,
            Certs = certs,
            CreateDate = createDate,
            ValidStart = validStart,
            ValidEnd = validEnd,
            RawCreateDate = createRaw,
            RawValidStart = validStartRaw,
            RawValidEnd = validEndRaw,
            PictureType = pictureType,
            PictureData = pictureData,
            PictureWidth = pictureW,
            PictureHeight = pictureH,
            CertDer = certDer,
            SignAlgId = signAlgId,
            SignedValue = signedValue,
            EsealInfoDer = sealInfoDer
        };
    }

    /// <summary>从文件读取并解析 SESeal（支持 .esl / .DER）。</summary>
    public static SealInfo ParseFile(string path)
        => Parse(System.IO.File.ReadAllBytes(path));

    /// <summary>将印章导出为图片字节（返回印章图片原始数据）。</summary>
    public static byte[] ExportPicture(SealInfo seal)
        => seal.PictureData;

    // 重新编码时按 UTC+Z 形式输出，保证字节稳定
    private static string ToGt(DateTime? dt)
        => (dt ?? DateTime.Now).ToUniversalTime().ToString("yyyyMMddHHmmss") + "Z";

    // GeneralizedTime 宽容解析：去掉时区与毫秒
    private static DateTime? ParseGeneralizedTime(string s)
    {
        try
        {
            if (string.IsNullOrEmpty(s)) return null;
            // 形如 20200425153000[.fff][Z|+0800|-0800]
            var clean = System.Text.RegularExpressions.Regex.Replace(
                s.Trim(), @"(Z|z|[+-]\d{2}:?\d{2})\s*$", "");
            clean = clean.Replace("T", " ");
            var dot = clean.IndexOf('.');
            if (dot > 0) clean = clean[..dot];
            clean = clean.Replace(" ", "").Replace("-", "").Replace(":", "").Trim();
            if (clean.Length == 14 &&
                DateTime.TryParseExact(clean, "yyyyMMddHHmmss", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
            if (clean.Length == 8 &&
                DateTime.TryParseExact(clean, "yyyyMMdd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out dt))
                return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        }
        catch
        {
            // 忽略无法解析的时间字段
        }
        return null;
    }
}