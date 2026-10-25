using System;
using System.Text;
using NetOfd.Core.Crypto;
using Org.BouncyCastle.Crypto.Digests;

namespace NetOfd.Crypto;

/// <summary>
/// OFD 用户口令加密器（GM/T 0099-2020 方案 1.1.1 口令方式加密-国密）。
/// <para>密钥派生规则依据 GB/T 32918.3-2016 5.4.3 密钥派生函数：
/// 使用 SM3-KDF 将用户口令扩展为 16 字节密钥加密密钥(KEK)，再用 SM4-CBC 加密文件加密密钥(FEK)。</para>
/// 对应 Java: org.ofdrw.crypto.enryptor.UserPasswordEncryptor
/// </summary>
public class UserPasswordEncryptor : IUserFekEncryptor
{
    private readonly string _username;
    private readonly string? _userType;
    private readonly byte[] _fKek;

    public UserPasswordEncryptor(string username, string? userType, string password)
    {
        if (string.IsNullOrEmpty(username)) throw new ArgumentException("用户名称(username)为空");
        if (string.IsNullOrEmpty(password)) throw new ArgumentException("加密口令(password)为空");
        _username = username;
        _userType = userType;
        _fKek = ExtendKey(password);
    }

    public UserPasswordEncryptor(string username, string password) : this(username, null, password) { }

    /// <summary>将口令通过密钥派生函数生成 KEK（SM3 + KDF，派生 16 字节）。</summary>
    private static byte[] ExtendKey(string password)
    {
        var md = new SM3Digest();
        return Kdf.Extend(Encoding.UTF8.GetBytes(password), 16);
    }

    public UserInfo Encrypt(byte[] fek, byte[] iv)
    {
        var wk = Sm4Cbc.Encrypt(fek, _fKek, iv);
        var userInfo = new UserInfo().SetUserName(_username);
        if (_userType is not null) userInfo.SetUserType(_userType);
        userInfo.SetIVValue(iv);
        userInfo.SetEncryptedWK(wk);
        return userInfo;
    }

    public byte[]? UserCert() => null;

    public string EncryptCaseId() => ProtectionCaseID.EncryptGMPassword.Id();
}
