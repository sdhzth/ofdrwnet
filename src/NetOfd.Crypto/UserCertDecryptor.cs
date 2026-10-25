using NetOfd.Core.Crypto;
using NetOfd.Sign.Sm2Strut;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;

namespace NetOfd.Crypto;

/// <summary>
/// OFD 用户证书解密器（GM/T 0099-2020 方案 1.1.2）。
/// 使用 SM2 私钥以 C1C3C2 模式解密包装密钥，恢复文件加密密钥(FEK)。
/// 对应 Java: org.ofdrw.crypto.decryptor.UserCertDecryptor
/// </summary>
public class UserCertDecryptor : IUserFekDecryptor
{
    private readonly string? _username;
    private readonly ECPrivateKeyParameters _privateKey;

    public UserCertDecryptor(string? username, ECPrivateKeyParameters privateKey)
    {
        if (privateKey is null) throw new ArgumentNullException(nameof(privateKey));
        _username = username;
        _privateKey = privateKey;
    }

    public UserCertDecryptor(ECPrivateKeyParameters privateKey) : this(null, privateKey) { }

    public DecryptResult Decrypt(UserInfo userInfo)
    {
        byte[] encryptedWK = userInfo.GetEncryptedWK();
        byte[] iv = userInfo.GetIVValue();

        // EncryptedWK 为 ASN.1 DER 编码的 SM2Cipher 结构（GB/T 35276 §7.2）；
        // 兼容旧版本直接存储的裸 C1C3C2 格式
        byte[] c1c3c2;
        try
        {
            var asn1 = Asn1Object.FromByteArray(encryptedWK);
            c1c3c2 = asn1 is Asn1Sequence
                ? Sm2Cipher.GetInstance(asn1).ConvertC1C3C2()
                : encryptedWK;
        }
        catch (IOException)
        {
            // 无法按 ASN.1 解析：视为旧版裸 C1C3C2 格式
            c1c3c2 = encryptedWK;
        }

        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(false, _privateKey);
        byte[] fek = engine.ProcessBlock(c1c3c2, 0, c1c3c2.Length);
        return new DecryptResult(fek, iv);
    }

    public string EncryptCaseId() => ProtectionCaseID.EncryptGMCert.Id();

    public string? GetUsername() => _username;
}
