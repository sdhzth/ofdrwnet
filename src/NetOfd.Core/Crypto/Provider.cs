using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 加密组件的相关信息（GM/T 0099-2020 附录 C.3 表 C.2）。
/// 对应 Java: org.ofdrw.core.crypto.encryt.Provider（扩展自 signatures.sig.Provider）
/// </summary>
public class Provider : OFDElement
{
    public Provider() : base("Provider") { }
    public Provider(XElement element) : base(element) { }

    /// <summary>【必选】加密组件名称</summary>
    public Provider SetProviderName(string name)
    {
        if (name is null) throw new System.ArgumentNullException(nameof(name));
        SetAttr("Name", name);
        return this;
    }

    public string? GetProviderName() => GetAttr("Name");

    /// <summary>【可选】提供方机构名称</summary>
    public Provider SetCompany(string? company)
    {
        SetAttr("Company", company);
        return this;
    }

    public string? GetCompany() => GetAttr("Company");

    /// <summary>【可选】组件版本</summary>
    public Provider SetVersion(string? version)
    {
        SetAttr("Version", version);
        return this;
    }

    public string? GetVersion() => GetAttr("Version");
}
