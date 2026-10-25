using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.PageObj;

/// <summary>
/// Page.xml 根元素（页面对象）。
/// 对应 Java: org.ofdrw.core.basicStructure.pageObj.Page
/// </summary>
public class PageObject : OFDElement
{
    public PageObject() : base("Page") { }
    public PageObject(XElement element) : base(element) { }

    /// <summary>页面区域（覆盖文档默认大小）</summary>
    public Doc.CT_PageArea? Area
    {
        get
        {
            var e = GetChildElement("Area");
            return e is null ? null : new Doc.CT_PageArea(e);
        }
        set
        {
            // 清理任何旧页面区域元素（兼容 <Area> 与历史上误写为 <PageArea> 的情况）
            _el.Elements()
                .Where(x => x.Name.LocalName == "Area" || x.Name.LocalName == "PageArea")
                .Remove();
            if (value is null) return;

            // CT_PageArea 自身根名为 "PageArea"（用于 Document/CommonData 上下文），
            // 但 GB/T 33190 规定在 <Page> 内页面区域元素名为 <Area>。
            // 这里克隆 value 的子树并重命名后再写入，不修改 value 本体的元素名，
            // 以便同一 CT_PageArea 实例在其它上下文中仍可正常写出 <PageArea>。
            var areaEl = new XElement(OfdNs + "Area");
            foreach (var a in value.Element.Attributes())
                areaEl.Add(new XAttribute(a));
            foreach (var c in value.Element.Elements())
                areaEl.Add(new XElement(c));
            _el.Add(areaEl);
        }
    }

    /// <summary>页面资源列表</summary>
    public List<STLoc> PageResList
    {
        get => GetChildElements("PageRes")
            .Select(e => new STLoc(e.Value))
            .ToList();
    }

    public PageObject AddPageRes(STLoc loc)
    {
        var e = new XElement(OfdNs + "PageRes") { Value = loc.Value };
        _el.Add(e);
        return this;
    }

    /// <summary>页面模板引用列表</summary>
    public List<CTTemplate> GetTemplates()
        => GetChildElements("Template").Select(e => new CTTemplate(e)).ToList();

    /// <summary>添加页面模板引用</summary>
    public PageObject AddTemplate(CTTemplate template)
    {
        AddOFDEntity(template);
        return this;
    }

    /// <summary>页面内容（图层集合）</summary>
    public Content? Content
    {
        get
        {
            var e = GetChildElement("Content");
            return e is null ? null : new Content(e);
        }
        set
        {
            if (value is null) GetChildElement("Content")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建 Content</summary>
    public Content ObtainContent()
    {
        var c = Content;
        if (c is not null) return c;
        c = new Content();
        SetOFDEntity(c);
        return c;
    }
}
