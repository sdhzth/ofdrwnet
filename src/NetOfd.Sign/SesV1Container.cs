using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// 《GM/T 0031-2014 安全电子签章密码技术规范》电子签章数据生成扩展容器（SES_V1）。
/// 参考 ofdrw-sign 的 SESV1Container。
/// </summary>
public class SesV1Container : ISignContainer
{
    private readonly AsymmetricCipherKeyPair _keyPair;
    private readonly Asn1Encodable? _seal;
    private readonly X509Certificate _cert;

    /// <summary>签章时间（格式 "yyyy-MM-dd HH:mm:ss"，默认当前时间）。</summary>
    public string? SignatureDateTime { get; set; }

    /// <summary>创建 SES_V1 签章容器。</summary>
    /// <param name="keyPair">签名使用的 SM2 私钥（密钥对）。</param>
    /// <param name="seal">电子印章（SESeal，可选，SES_V1 中非必填）。</param>
    /// <param name="signCert">签章用户证书。</param>
    public SesV1Container(AsymmetricCipherKeyPair keyPair, Asn1Encodable? seal, X509Certificate signCert)
    {
        _keyPair = keyPair ?? throw new ArgumentException("签名使用私钥(keyPair)不能为空");
        _seal = seal;
        _cert = signCert ?? throw new ArgumentException("签章用户证书(signCert)不能为空");
    }

    /// <summary>电子印章 DER（无印章时为 null）。</summary>
    public byte[]? Seal => _seal?.GetEncoded();

    public string SignType => "Seal";

    /// <summary>对待签名数据进行电子签章（SES_V1 结构）。</summary>
    public byte[] Sign(byte[] data, string propertyInfo)
    {
        var dataHash = GmCrypto.Sm3(data);
        var dt = SignatureDateTime ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        var tbsSeq = SesV1.BuildTbsSeq(
            version: 1,
            dataHash: dataHash,
            timeInfoBytes: System.Text.Encoding.UTF8.GetBytes(dt),
            propertyInfo: propertyInfo,
            certDer: _cert.GetEncoded(),
            seal: _seal);
        var tbsDer = tbsSeq.GetEncoded();
        var sm2Sig = GmCrypto.Sm2Sign(tbsDer, _keyPair.Private);
        return SesV1.BuildSignedValue(tbsSeq, sm2Sig);
    }
}