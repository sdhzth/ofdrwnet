using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// 《GB/T 38540-2020 信息安全技术 安全电子签章密码技术规范》电子签章数据生成扩展容器（SES_V4）。
/// 参考 ofdrw-sign 的 SESV4Container。
/// <para>V5 与 V4 的 ASN.1 结构一致，仅版本号不同，见 <see cref="SesV5Container"/>。</para>
/// </summary>
public class SesV4Container : ISignContainer
{
    private readonly AsymmetricCipherKeyPair _keyPair;
    private readonly byte[] _sealDer;
    private readonly X509Certificate _cert;
    private readonly int _version;
    private readonly TimeStampHook? _timeStampHook;

    /// <summary>签章时间（格式 "yyyy-MM-dd HH:mm:ss"，默认当前时间）。</summary>
    public string? SignatureDateTime { get; set; }

    /// <summary>创建 SES_V4/V5 签章容器。</summary>
    /// <param name="keyPair">签名使用的 SM2 私钥（密钥对）。</param>
    /// <param name="sealDer">电子印章 SESeal（DER 编码，必填）。</param>
    /// <param name="signCert">签章用户证书。</param>
    /// <param name="version">签章版本（4 或 5）。</param>
    /// <param name="timeStampHook">时间戳钩子（可选）。</param>
    public SesV4Container(
        AsymmetricCipherKeyPair keyPair, byte[] sealDer, X509Certificate signCert,
        int version = 4, TimeStampHook? timeStampHook = null)
    {
        if (version != 4 && version != 5)
            throw new ArgumentException("签章版本(version)仅支持 4 或 5");
        _keyPair = keyPair ?? throw new ArgumentException("签名使用私钥(keyPair)不能为空");
        _sealDer = sealDer ?? throw new ArgumentException("电子印章(seal)不能为空");
        _cert = signCert ?? throw new ArgumentException("签章用户证书(signCert)不能为空");
        _version = version;
        _timeStampHook = timeStampHook;
    }

    /// <summary>创建 SES_V4/V5 签章容器。</summary>
    /// <param name="keyPair">签名使用的 SM2 私钥（密钥对）。</param>
    /// <param name="seal">电子印章 SESeal（ASN.1 对象，必填）。</param>
    /// <param name="signCert">签章用户证书。</param>
    /// <param name="version">签章版本（4 或 5）。</param>
    /// <param name="timeStampHook">时间戳钩子（可选）。</param>
    public SesV4Container(
        AsymmetricCipherKeyPair keyPair, Asn1Encodable seal, X509Certificate signCert,
        int version = 4, TimeStampHook? timeStampHook = null)
        : this(keyPair, seal.GetEncoded(), signCert, version, timeStampHook)
    {
    }

    /// <summary>电子印章 DER。</summary>
    public byte[]? Seal => _sealDer;

    public string SignType => "Seal";

    /// <summary>对待签名数据进行电子签章（SES_V4/V5 结构）。</summary>
    public byte[] Sign(byte[] data, string propertyInfo)
    {
        var dataHash = GmCrypto.Sm3(data);
        var dt = SignatureDateTime ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        string gt = DateTime.TryParse(dt, out var d) ? d.ToUniversalTime().ToString("yyyyMMddHHmmss") + "Z" : dt;
        var tbsSeq = SesV4.BuildTbsSeq(_version, _sealDer, dataHash, gt, propertyInfo);
        var tbsDer = tbsSeq.GetEncoded();
        var sm2Sig = GmCrypto.Sm2Sign(tbsDer, _keyPair.Private);
        byte[]? token = _timeStampHook?.Invoke(sm2Sig);
        return SesV4.BuildSignedValue(tbsSeq, _cert.GetEncoded(), sm2Sig, token);
    }
}