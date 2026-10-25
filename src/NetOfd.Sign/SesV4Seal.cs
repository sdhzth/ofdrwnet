using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// SES_V4 电子印章（SESeal）构建器。
/// <para>
/// 结构（DER）：
/// <code>
/// SESeal ::= SEQUENCE {
///     eSealInfo   SES_SealInfo,
///     cert        OCTET STRING,          -- 印章证书
///     signAlgID   OBJECT IDENTIFIER,     -- sm2sign_with_sm3
///     signedValue BIT STRING             -- 对 eSealInfo DER 的 SM2 签名
/// }
/// SES_SealInfo ::= SEQUENCE {
///     header   SES_Header,               -- { id "ES", version 4, vid }
///     esID     IA5String,                -- 印章唯一标识
///     property SES_ESPropertyInfo,       -- 印章属性（名称、证书列表、有效期）
///     picture  SES_ESPictrueInfo         -- 印章图片
/// }
/// </code>
/// </para>
/// 参考 ofdrw-gm 的 ses.v4.SESeal。注意：生产环境请使用符合国家规范的印章制作设备。
/// </summary>
public static class SesV4Seal
{
    /// <summary>印章类型：单位印章</summary>
    public const int TypeUnit = 1;

    /// <summary>印章类型：个人印章</summary>
    public const int TypePersonal = 2;

    /// <summary>证书列表类型：证书列表（CertInfoList）</summary>
    public const int CertListType = 1;

    /// <summary>
    /// 构建 SESeal（V4/V5）DER 编码。
    /// </summary>
    /// <param name="esId">印章唯一标识</param>
    /// <param name="vid">印章版本标识</param>
    /// <param name="sealName">印章名称（印章单位/个人名称）</param>
    /// <param name="sealType">印章类型（1-单位，2-个人）</param>
    /// <param name="pictureData">印章图片二进制数据</param>
    /// <param name="pictureType">印章图片类型（如 "PNG"、"JPG"、"SVG"）</param>
    /// <param name="widthMm">印章显示宽度（mm）</param>
    /// <param name="heightMm">印章显示高度（mm）</param>
    /// <param name="sealCert">印章证书</param>
    /// <param name="sealKeyPair">印章签名密钥对</param>
    /// <param name="createDate">印章制作时间</param>
    /// <param name="validStart">印章有效期起始</param>
    /// <param name="validEnd">印章有效期截止</param>
    /// <param name="version">印章结构版本（4 或 5，默认 4）</param>
    /// <returns>SESeal DER 编码</returns>
    public static byte[] Build(
        string esId, string vid, string sealName, int sealType,
        byte[] pictureData, string pictureType, int widthMm, int heightMm,
        X509Certificate sealCert, AsymmetricCipherKeyPair sealKeyPair,
        DateTime createDate, DateTime validStart, DateTime validEnd,
        int version = 4)
    {
        // SES_Header: { id "ES", version, vid }
        var header = new DerSequence(
            new DerIA5String("ES"),
            new DerInteger(version),
            new DerIA5String(vid));

        // 印章图片
        var picture = new DerSequence(
            new DerIA5String(pictureType),
            new DerOctetString(pictureData),
            new DerInteger(widthMm),
            new DerInteger(heightMm));

        // 证书列表（仅一个印章证书）
        var certList = new DerSequence(
            new DerOctetString(sealCert.GetEncoded()));

        // SES_ESPropertyInfo
        var property = new DerSequence(
            new DerInteger(sealType),
            new DerUtf8String(sealName),
            new DerInteger(CertListType),
            certList,
            new DerGeneralizedTime(ToGeneralizedString(createDate)),
            new DerGeneralizedTime(ToGeneralizedString(validStart)),
            new DerGeneralizedTime(ToGeneralizedString(validEnd)));

        // SES_SealInfo
        var sealInfo = new DerSequence(header, new DerIA5String(esId), property, picture);
        var sealInfoDer = sealInfo.GetEncoded();

        // 对 eSealInfo DER 使用 SM2withSM3 签名
        var signedValue = GmCrypto.Sm2Sign(sealInfoDer, sealKeyPair.Private);

        // SESeal
        var seal = new DerSequence(
            sealInfo,
            new DerOctetString(sealCert.GetEncoded()),
            GMObjectIdentifiers.sm2sign_with_sm3,
            new DerBitString(signedValue));
        return seal.GetEncoded();
    }

    private static string ToGeneralizedString(DateTime dt)
        => dt.ToUniversalTime().ToString("yyyyMMddHHmmss") + "Z";
}