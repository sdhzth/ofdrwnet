using System.Xml.Linq;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.Doc;

/// <summary>
/// CT_CommonData - 文档公共数据。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.CT_CommonData
/// </summary>
public class CTCommonData : OFDElement
{
    public CTCommonData() : base("CommonData") { }
    public CTCommonData(XElement element) : base(element) { }

    /// <summary>最大单元 ID（用于 ID 分配）</summary>
    public STID MaxUnitID
    {
        get
        {
            var v = GetChildText("MaxUnitID");
            return v is null ? new STID(1) : STID.Parse(v);
        }
        set => SetChildText("MaxUnitID", value.Value.ToString());
    }

    /// <summary>页面区域（页面大小定义）</summary>
    public CT_PageArea? PageArea
    {
        get
        {
            var e = GetChildElement("PageArea");
            return e is null ? null : new CT_PageArea(e);
        }
        set
        {
            if (value is null) GetChildElement("PageArea")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建 PageArea</summary>
    public CT_PageArea ObtainPageArea()
    {
        var pa = PageArea;
        if (pa is not null) return pa;
        pa = new CT_PageArea();
        SetOFDEntity(pa);
        return pa;
    }

    /// <summary>公共资源列表（PublicRes 路径）</summary>
    public List<STLoc> PublicResList
    {
        get => GetChildElements("PublicRes")
            .Select(e => new STLoc(e.Value))
            .ToList();
    }

    public CTCommonData AddPublicRes(STLoc loc)
    {
        var e = new XElement(OfdNs + "PublicRes") { Value = loc.Value };
        _el.Add(e);
        return this;
    }

    /// <summary>文档资源列表（DocumentRes 路径）</summary>
    public List<STLoc> DocumentResList
    {
        get => GetChildElements("DocumentRes")
            .Select(e => new STLoc(e.Value))
            .ToList();
    }

    public CTCommonData AddDocumentRes(STLoc loc)
    {
        var e = new XElement(OfdNs + "DocumentRes") { Value = loc.Value };
        _el.Add(e);
        return this;
    }

    /// <summary>默认颜色空间 ID</summary>
    public STRefID? DefaultCS
    {
        get
        {
            var v = GetChildText("DefaultCS");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetChildText("DefaultCS", value?.Value.ToString());
    }

    /// <summary>模板页列表</summary>
    public List<CTTemplatePage> GetTemplatePages()
        => GetChildElements("TemplatePage").Select(e => new CTTemplatePage(e)).ToList();

    /// <summary>添加模板页</summary>
    public CTCommonData AddTemplatePage(CTTemplatePage tpl)
    {
        AddOFDEntity(tpl);
        return this;
    }
}
