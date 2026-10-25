using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.Versions;

namespace NetOfd.Core.BasicStructure.OFD;

/// <summary>
/// DocBody - OFD 包中每个文档的入口描述。
/// 包含文档元数据、根文档路径、签名路径等。
/// 对应 Java: org.ofdrw.core.basicStructure.ofd.DocBody
/// </summary>
public class DocBody : OFDElement
{
    public DocBody() : base("DocBody") { }
    public DocBody(XElement element) : base(element) { }

    /// <summary>文档元数据</summary>
    public DocInfo? DocInfo
    {
        get
        {
            var e = GetChildElement("DocInfo");
            return e is null ? null : new DocInfo(e);
        }
        set
        {
            if (value is null) GetChildElement("DocInfo")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建 DocInfo</summary>
    public DocInfo ObtainDocInfo()
    {
        var di = DocInfo;
        if (di is not null) return di;
        di = new DocInfo();
        SetOFDEntity(di);
        return di;
    }

    /// <summary>文档根文件路径（相对于 OFD 包根目录），指向 Document.xml</summary>
    public STLoc? DocRoot
    {
        get
        {
            var v = GetChildText("DocRoot");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("DocRoot", value?.Value);
    }

    /// <summary>签名入口文件路径</summary>
    public STLoc? Signatures
    {
        get
        {
            var v = GetChildText("Signatures");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("Signatures", value?.Value);
    }

    /// <summary>版本结构列表（可选，第 19 章）</summary>
    public Versions.Versions? Versions
    {
        get
        {
            var e = GetChildElement("Versions");
            return e is null ? null : new Versions.Versions(e);
        }
        set
        {
            if (value is null) GetChildElement("Versions")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建版本结构列表</summary>
    public Versions.Versions ObtainVersions()
    {
        var v = Versions;
        if (v is not null) return v;
        v = new Versions.Versions();
        SetOFDEntity(v);
        return v;
    }
}
