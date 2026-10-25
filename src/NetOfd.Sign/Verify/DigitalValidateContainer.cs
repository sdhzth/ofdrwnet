using Org.BouncyCastle.Crypto;

namespace NetOfd.Sign.Verify;

/// <summary>
/// 数字签名验证容器（旧版裸 SM2 签名值，需要外部提供验证公钥）。
/// </summary>
/// <remarks>
/// 已废弃：OFD 的数字签名应符合《GB/T 35275》，请使用 <see cref="Gbt35275ValidateContainer"/>。
/// 对应 Java: org.ofdrw.sign.verify.container.DigitalValidateContainer（@Deprecated）
/// </remarks>
[Obsolete("OFD 的数字签名应符合 GB/T 35275，请使用 Gbt35275ValidateContainer")]
public class DigitalValidateContainer : ISignedDataValidateContainer
{
    /// <summary>验证使用的公钥。</summary>
    public AsymmetricKeyParameter PublicKey { get; }

    public DigitalValidateContainer(AsymmetricKeyParameter publicKey)
    {
        if (publicKey == null)
            throw new ArgumentException("验证使用的公钥参数(pk)不能为空");
        PublicKey = publicKey;
    }

    /// <summary>使用证书中的公钥创建验证容器。</summary>
    public DigitalValidateContainer(Org.BouncyCastle.X509.X509Certificate certificate)
        : this(certificate.GetPublicKey())
    {
    }

    public void Validate(string type, string alg, byte[] tbsContent, byte[] signedValue)
    {
        if (!string.Equals(type, "Sign", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("签名类型(type)必须是 Sign，不支持电子印章验证");

        if (!GmCrypto.Sm2Verify(tbsContent, signedValue, PublicKey))
            throw new InvalidSignedValueException("签名值不一致");
    }
}