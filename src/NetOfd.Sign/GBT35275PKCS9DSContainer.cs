using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using NetOfd.Sign.Sm2Strut;
using NetOfd.Sign.Sm2Strut.Builder;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// 根据 GM/T 0099-2020 的数字签名容器（PKCS#7 CMS 兼容格式）。
/// <para>
/// 与 <see cref="GBT35275DSContainer"/> 不同，本容器签名的对象为 SignerInfo 中的
/// authenticatedAttributes 字段（PKCS#9 结构，包含签名时间与原文杂凑值）。该格式为数科验证兼容格式。
/// </para>
/// 参考 ofdrw-sign 的 GBT35275PKCS9DSContainer。
/// </summary>
public sealed class GBT35275PKCS9DSContainer : ISignContainer
{
    private readonly AsymmetricCipherKeyPair _keyPair;
    private readonly X509Certificate _cert;

    /// <summary>创建数字签名容器（签名值数据遵循 GB/T 35275）。</summary>
    /// <param name="cert">SM2 签名证书，应符合 GB/T 20518。</param>
    /// <param name="keyPair">SM2 签名密钥对（私钥）。</param>
    public GBT35275PKCS9DSContainer(X509Certificate cert, AsymmetricCipherKeyPair keyPair)
    {
        _cert = cert ?? throw new ArgumentException("签名使用证书（cert）不能为空");
        _keyPair = keyPair ?? throw new ArgumentException("签名使用私钥（keyPair）不能为空");
    }

    /// <summary>数字签名不提供印章。</summary>
    public byte[]? Seal => null;

    public string SignType => "Sign";

    /// <summary>对待签名数据签名（签名对象为 PKCS#9 属性集合）。</summary>
    public byte[] Sign(byte[] data, string propertyInfo)
    {
        var signedData = PKCS9SignedDataBuilder.SignedData(data, _keyPair, _cert);
        var contentInfo = new ContentInfo(OIDs.SignedData, signedData);
        return contentInfo.ToAsn1Object().GetEncoded();
    }
}