using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using NetOfd.Sign.Sm2Strut;
using NetOfd.Sign.Sm2Strut.Builder;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// GB/T 35275 数字签名容器（GM/T 0099-2020 7.2.2 b）。
/// <para>
/// 处理流程：对原文计算 SM3 摘要 → 使用 SM2 私钥对摘要签名 →
/// 封装为 ContentInfo(SignedData) 结构（遵循 GB/T 35275）。
/// </para>
/// 参考 ofdrw-sign 的 GBT35275DSContainer。
/// </summary>
public sealed class GBT35275DSContainer : ISignContainer
{
    private readonly X509Certificate _cert;
    private readonly AsymmetricCipherKeyPair _keyPair;

    /// <summary>是否将文件摘要值先进行 Base64 编码再签名（兼容非规范实现）。</summary>
    public bool EnableFileHashBase64 { get; set; }

    public GBT35275DSContainer(X509Certificate cert, AsymmetricCipherKeyPair keyPair)
    {
        _cert = cert ?? throw new ArgumentException("签名使用证书(cert)不能为空");
        _keyPair = keyPair ?? throw new ArgumentException("签名使用私钥(keyPair)不能为空");
    }

    public byte[]? Seal => null;

    public string SignType => "Sign";

    public byte[] Sign(byte[] data, string propertyInfo)
    {
        // d) 计算被签文件的摘要值
        byte[] plaintext = GmCrypto.Sm3(data);
        if (EnableFileHashBase64)
            plaintext = System.Text.Encoding.UTF8.GetBytes(Convert.ToBase64String(plaintext));

        // e) 使用签名者私钥对摘要值计算签名
        var signature = GmCrypto.Sm2Sign(plaintext, _keyPair.Private);

        var signedData = SignedDataBuilder.SignedData(plaintext, signature, _cert);
        var contentInfo = new ContentInfo(OIDs.SignedData, signedData);
        return contentInfo.ToAsn1Object().GetEncoded();
    }
}