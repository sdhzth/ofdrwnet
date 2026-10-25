using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 可解密该次操作的用户（GM/T 0099-2020 附录 C.1）。
/// 对应 Java: org.ofdrw.core.crypto.encryt.UserInfo
/// </summary>
public class UserInfo : OFDElement
{
    /// <summary>用户角色类型：文档管理员</summary>
    public const string UserTypeOwner = "Owner";

    /// <summary>用户角色类型：普通用户（默认值）</summary>
    public const string UserTypeUser = "User";

    public UserInfo() : base("UserInfo") { }
    public UserInfo(XElement element) : base(element) { }

    /// <summary>【可选】用户名称</summary>
    public UserInfo SetUserName(string? userName)
    {
        SetAttr("UserName", userName);
        return this;
    }

    public string? GetUserName() => GetAttr("UserName");

    /// <summary>【可选】用户角色类型（Owner / User，默认 User）</summary>
    public UserInfo SetUserType(string? userType)
    {
        SetAttr("UserType", userType);
        return this;
    }

    public string GetUserType() => GetAttr("UserType") ?? UserTypeUser;

    /// <summary>【可选】用户的加解密公钥证书（DER 编码），加密方案 1.1.2 时必选</summary>
    public UserInfo SetUserCert(byte[]? userCert)
    {
        if (userCert is null) GetChildElement("UserCert")?.Remove();
        else SetChildText("UserCert", Convert.ToBase64String(userCert));
        return this;
    }

    public byte[]? GetUserCert()
    {
        var e = GetChildElement("UserCert");
        return e is null ? null : Convert.FromBase64String(e.Value.Trim());
    }

    /// <summary>【必选】文件对称加密的包装密钥（Base64）</summary>
    public UserInfo SetEncryptedWK(byte[] encryptedWk)
    {
        if (encryptedWk is null) throw new System.ArgumentNullException(nameof(encryptedWk));
        SetChildText("EncryptedWK", Convert.ToBase64String(encryptedWk));
        return this;
    }

    public byte[] GetEncryptedWK()
    {
        var e = GetChildElement("EncryptedWK");
        if (e is null) throw new System.InvalidOperationException("EncryptedWK 不存在");
        return Convert.FromBase64String(e.Value.Trim());
    }

    /// <summary>【可选】初始化向量值，默认 16 个 0</summary>
    public UserInfo SetIVValue(byte[]? ivValue)
    {
        if (ivValue is null) GetChildElement("IVValue")?.Remove();
        else SetChildText("IVValue", Convert.ToBase64String(ivValue));
        return this;
    }

    public byte[] GetIVValue()
    {
        var e = GetChildElement("IVValue");
        return e is null ? new byte[16] : Convert.FromBase64String(e.Value.Trim());
    }
}
