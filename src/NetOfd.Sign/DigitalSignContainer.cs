using Org.BouncyCastle.Crypto;

namespace NetOfd.Sign;

/// <summary>
/// 数字签名容器：使用 SM2withSM3 对数据直接签名。
/// 参考 ofdrw-sign 的 DigitalSignContainer（该实现已废弃，建议使用遵循 GB/T 35275 的
/// <see cref="GBT35275DSContainer"/>）。
/// </summary>
public sealed class DigitalSignContainer : ISignContainer
{
    private readonly AsymmetricCipherKeyPair _keyPair;

    public DigitalSignContainer(AsymmetricCipherKeyPair keyPair)
    {
        _keyPair = keyPair ?? throw new ArgumentException("签名使用私钥(keyPair)不能为空");
    }

    public byte[]? Seal => null;

    public string SignType => "Sign";

    public byte[] Sign(byte[] data, string propertyInfo)
        => GmCrypto.Sm2Sign(data, _keyPair.Private);
}