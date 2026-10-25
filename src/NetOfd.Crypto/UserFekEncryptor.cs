using NetOfd.Core.Crypto;

namespace NetOfd.Crypto;

/// <summary>
/// 用户 FEK（文件加密密钥）加密器接口。
/// 将对称的文件加密密钥(FEK)按用户指定的保护方案包装为包装密钥(EncryptedWK)。
/// 对应 Java: org.ofdrw.crypto.enryptor.UserFEKEncryptor
/// </summary>
public interface IUserFekEncryptor
{
    /// <summary>使用 FEK 与 IV 加密，生成可解密的 UserInfo（含包装密钥）。</summary>
    UserInfo Encrypt(byte[] fek, byte[] iv);

    /// <summary>加密时使用的证书（DER 编码）；口令加密时返回 null。</summary>
    byte[]? UserCert();

    /// <summary>加密保护方案标识（GM/T 0099-2020 附录 A.1）。</summary>
    string EncryptCaseId();
}

/// <summary>
/// 用户 FEK（文件加密密钥）解密器接口。
/// 对应 Java: org.ofdrw.crypto.decryptor.UserFEKDecryptor
/// </summary>
public interface IUserFekDecryptor
{
    /// <summary>从 UserInfo 中恢复 FEK 与 IV。</summary>
    DecryptResult Decrypt(UserInfo userInfo);

    /// <summary>加密保护方案标识（GM/T 0099-2020 附录 A.1）。</summary>
    string EncryptCaseId();

    /// <summary>目标用户名；null 时匹配第一个 UserInfo。</summary>
    string? GetUsername();
}
