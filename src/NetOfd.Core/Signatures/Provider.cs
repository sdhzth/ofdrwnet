using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 创建签名时所用的签章组件提供者信息（GB/T 33190-2016 §18.2.1 图 86 表 67）。
/// 对应 Java: org.ofdrw.core.signatures.sig.Provider
/// </summary>
public class Provider : OFDElement
{
    public Provider() : base("Provider") { }

    public Provider(XElement element) : base(element) { }

    /// <summary>以提供者名称创建。</summary>
    public Provider(string providerName) : this() => ProviderName = providerName;

    /// <summary>【必选 属性】签章组件提供者名称。</summary>
    public string? ProviderName
    {
        get => GetAttr("ProviderName");
        set => SetAttr("ProviderName", value);
    }

    /// <summary>【可选 属性】签章组件版本。</summary>
    public string? Version
    {
        get => GetAttr("Version");
        set => SetAttr("Version", value);
    }

    /// <summary>【可选 属性】签章组件制造商。</summary>
    public string? Company
    {
        get => GetAttr("Company");
        set => SetAttr("Company", value);
    }

    /// <summary>【可选 属性 OFD 2.0】签章组件接口协议版本（GMT0099 表 B.2）。</summary>
    public string? ProtocolVer
    {
        get => GetAttr("ProtocolVer");
        set => SetAttr("ProtocolVer", value);
    }

    /// <summary>
    /// 【可选 OFD 2.0】签章组件扩展信息（<c>ExtendData</c> 子元素，Base64 编码）。
    /// 未设置时返回 null（Java 版返回空数组）。
    /// </summary>
    public byte[]? ExtendData
    {
        get
        {
            string? t = GetChildText("ExtendData");
            return string.IsNullOrWhiteSpace(t) ? null : Convert.FromBase64String(t.Trim());
        }
        set
        {
            if (value is null || value.Length == 0)
            {
                GetChildElement("ExtendData")?.Remove();
                return;
            }
            SetChildText("ExtendData", Convert.ToBase64String(value));
        }
    }
}
