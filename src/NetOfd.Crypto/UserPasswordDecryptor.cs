using System;
using System.Text;
using NetOfd.Core.Crypto;

namespace NetOfd.Crypto;

/// <summary>
/// OFD 用户口令解密器（GM/T 0099-2020 方案 1.1.1）。
/// 使用口令经 SM3-KDF 派生 KEK，再用 SM4-CBC 解密 EncryptedWK 得到 FEK。
/// 对应 Java: org.ofdrw.crypto.decryptor.UserPasswordDecryptor
/// </summary>
public class UserPasswordDecryptor : IUserFekDecryptor
{
    private readonly string? _username;
    private readonly byte[] _kek;

    public UserPasswordDecryptor(string? username, string password)
    {
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("口令(password)不能为空");
        _username = username;
        _kek = Kdf.Extend(Encoding.UTF8.GetBytes(password), 16);
    }

    public UserPasswordDecryptor(string password) : this(null, password) { }

    public DecryptResult Decrypt(UserInfo userInfo)
    {
        byte[] encryptedWK = userInfo.GetEncryptedWK();
        byte[] iv = userInfo.GetIVValue();
        byte[] fek = Sm4Cbc.Decrypt(encryptedWK, _kek, iv);
        return new DecryptResult(fek, iv);
    }

    public string EncryptCaseId() => ProtectionCaseID.EncryptGMPassword.Id();

    public string? GetUsername() => _username;
}
