using System;
using NetOfd.Core.Crypto;
using NetOfd.Sign.Sm2Strut;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;

namespace NetOfd.Crypto;

/// <summary>
/// OFD 用户证书加密器（GM/T 0099-2020 方案 1.1.2 证书方式加密-国密）。
/// <para>使用用户证书中的 SM2 公钥以 C1C3C2 模式加密文件加密密钥(FEK)，生成包装密钥。</para>
/// 对应 Java: org.ofdrw.crypto.enryptor.UserCertEncryptor
/// </summary>
public class UserCertEncryptor : IUserFekEncryptor
{
    private readonly string _username;
    private readonly string? _userType;
    private readonly X509Certificate _cert;

    public UserCertEncryptor(string username, string? userType, X509Certificate certificate)
    {
        if (string.IsNullOrEmpty(username)) throw new ArgumentException("用户名称(username)为空");
        if (certificate is null) throw new ArgumentNullException(nameof(certificate));
        _username = username;
        _userType = userType;
        _cert = certificate;
    }

    public UserCertEncryptor(string username, X509Certificate certificate) : this(username, null, certificate) { }

    public UserInfo Encrypt(byte[] fek, byte[] iv)
    {
        var pub = (ECPublicKeyParameters)_cert.GetPublicKey();
        var engine = new SM2Engine(SM2Engine.Mode.C1C3C2);
        engine.Init(true, new ParametersWithRandom(pub, new SecureRandom()));
        byte[] c1c3c2 = engine.ProcessBlock(fek, 0, fek.Length);

        // EncryptedWK 以 ASN.1 DER 编码的 SM2Cipher 结构存储（GB/T 35276 §7.2，对齐 Java ofdrw）
        var sm2Cipher = Sm2Cipher.FromC1C3C2(c1c3c2);

        var userInfo = new UserInfo().SetUserName(_username);
        if (_userType is not null) userInfo.SetUserType(_userType);
        userInfo.SetIVValue(iv);
        userInfo.SetEncryptedWK(sm2Cipher.GetEncoded());
        return userInfo;
    }

    public byte[]? UserCert() => _cert.GetEncoded();

    public string EncryptCaseId() => ProtectionCaseID.EncryptGMCert.Id();
}
