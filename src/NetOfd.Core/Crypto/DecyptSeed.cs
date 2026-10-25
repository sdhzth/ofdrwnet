using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 密钥描述文件（GM/T 0099-2020 附录 C.2 图 C.1）。
/// 采用 XML 形式描述方案、算法、多人/多角色/多密码或证书等关键解密信息。
/// 对应 Java: org.ofdrw.core.crypto.encryt.DecyptSeed
/// </summary>
public class DecyptSeed : OFDElement
{
    public DecyptSeed() : base("DecyptSeed") { }
    public DecyptSeed(XElement element) : base(element) { }

    /// <summary>【必选 属性】加密操作标识，应与解密入口描述中的一致</summary>
    public DecyptSeed SetID(string id)
    {
        if (string.IsNullOrEmpty(id)) throw new System.ArgumentException("id 为空");
        SetAttr("ID", id);
        return this;
    }

    public string? GetID() => GetAttr("ID");

    /// <summary>【必选 属性】加密保护方案标识，参见附录 A</summary>
    public DecyptSeed SetEncryptCaseId(ProtectionCaseID id)
    {
        SetAttr("EncryptCaseId", id.Id());
        return this;
    }

    public DecyptSeed SetEncryptCaseId(string? id)
    {
        SetAttr("EncryptCaseId", id);
        return this;
    }

    public string? GetEncryptCaseId() => GetAttr("EncryptCaseId");

    /// <summary>【必选】增加可解密该次操作的用户</summary>
    public DecyptSeed AddUserInfo(UserInfo userInfo)
    {
        if (userInfo is null) throw new System.ArgumentNullException(nameof(userInfo));
        AddOFDEntity(userInfo);
        return this;
    }

    public System.Collections.Generic.List<UserInfo> GetUserInfos() =>
        GetChildElements("UserInfo").Select(e => new UserInfo(e)).ToList();

    /// <summary>【必选】设置扩展参数节点</summary>
    public DecyptSeed SetExtendParams(ExtendParams extendParams)
    {
        if (extendParams is null) throw new System.ArgumentNullException(nameof(extendParams));
        SetOFDEntity(extendParams);
        return this;
    }

    public ExtendParams GetExtendParams()
    {
        var e = GetChildElement("ExtendParams");
        if (e is null)
        {
            var empty = new ExtendParams();
            SetExtendParams(empty);
            return empty;
        }
        return new ExtendParams(e);
    }
}
