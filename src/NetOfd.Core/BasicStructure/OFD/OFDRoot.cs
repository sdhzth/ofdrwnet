using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.OFD;

/// <summary>
/// OFD 根元素，对应 OFD.xml 的根节点。
/// 对应 Java: org.ofdrw.core.basicStructure.ofd.OFD
/// </summary>
public class OFDRoot : OFDElement
{
    public const string Version = "1.2";
    public const string DocType = "OFD";

    public OFDRoot() : base("OFD")
    {
        _el.SetAttributeValue("Version", Version);
        _el.SetAttributeValue("DocType", DocType);
        // 设置 xmlns 声明
        if (_el.Name.Namespace == XNamespace.None)
            _el.Name = OfdNs + "OFD";
        _el.SetAttributeValue(XNamespace.Xmlns + "ofd", OfdNs.NamespaceName);
    }

    public OFDRoot(XElement element) : base(element) { }

    /// <summary>版本号</summary>
    public string? OfdVersion
    {
        get => GetAttr("Version");
        set => SetAttr("Version", value);
    }

    /// <summary>文档类型</summary>
    public string? OfdDocType
    {
        get => GetAttr("DocType");
        set => SetAttr("DocType", value);
    }

    /// <summary>添加 DocBody</summary>
    public OFDRoot AddDocBody(DocBody docBody)
    {
        AddOFDEntity(docBody);
        return this;
    }

    /// <summary>获取所有 DocBody</summary>
    public List<DocBody> GetDocBodies()
    {
        return GetChildElements("DocBody")
            .Select(e => new DocBody(e))
            .ToList();
    }

    /// <summary>获取第一个 DocBody</summary>
    public DocBody? GetFirstDocBody()
    {
        var e = GetChildElement("DocBody");
        return e is null ? null : new DocBody(e);
    }

    /// <summary>获取或创建第一个 DocBody</summary>
    public DocBody ObtainDocBody()
    {
        var e = GetChildElement("DocBody");
        if (e is not null) return new DocBody(e);
        var body = new DocBody();
        AddOFDEntity(body);
        return body;
    }
}
