using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// 《GB/T 38540-2020 信息安全技术 安全电子签章密码技术规范》电子签章数据生成扩展容器（SES_V5）。
/// 参考 ofdrw-sign 的 SESV5Container。V5 与 V4 的 ASN.1 结构一致，仅版本号不同。
/// </summary>
public sealed class SesV5Container : SesV4Container
{
    /// <summary>创建 SES_V5 签章容器。</summary>
    /// <param name="keyPair">签名使用的 SM2 私钥（密钥对）。</param>
    /// <param name="sealDer">电子印章 SESeal（DER 编码，必填）。</param>
    /// <param name="signCert">签章用户证书。</param>
    /// <param name="timeStampHook">时间戳钩子（可选）。</param>
    public SesV5Container(
        AsymmetricCipherKeyPair keyPair, byte[] sealDer, X509Certificate signCert,
        TimeStampHook? timeStampHook = null)
        : base(keyPair, sealDer, signCert, 5, timeStampHook)
    {
    }

    /// <summary>创建 SES_V5 签章容器。</summary>
    /// <param name="keyPair">签名使用的 SM2 私钥（密钥对）。</param>
    /// <param name="seal">电子印章 SESeal（ASN.1 对象，必填）。</param>
    /// <param name="signCert">签章用户证书。</param>
    /// <param name="timeStampHook">时间戳钩子（可选）。</param>
    public SesV5Container(
        AsymmetricCipherKeyPair keyPair, Org.BouncyCastle.Asn1.Asn1Encodable seal, X509Certificate signCert,
        TimeStampHook? timeStampHook = null)
        : base(keyPair, seal, signCert, 5, timeStampHook)
    {
    }
}