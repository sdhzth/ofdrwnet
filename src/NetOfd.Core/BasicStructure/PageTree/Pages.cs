using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.PageTree;

/// <summary>
/// Pages - 页面树根节点，包含所有页面引用。
/// 对应 Java: org.ofdrw.core.basicStructure.pageTree.Pages
/// </summary>
public class Pages : OFDElement
{
    public Pages() : base("Pages") { }
    public Pages(XElement element) : base(element) { }

    /// <summary>添加页面节点</summary>
    public Pages AddPage(Page page)
    {
        AddOFDEntity(page);
        return this;
    }

    /// <summary>获取所有页面节点</summary>
    public List<Page> GetPages()
    {
        return GetChildElements("Page")
            .Select(e => new Page(e))
            .ToList();
    }

    /// <summary>按索引获取页面（0-based）</summary>
    public Page? GetPageByIndex(int index)
    {
        var list = GetPages();
        return index >= 0 && index < list.Count ? list[index] : null;
    }

    /// <summary>页面数量</summary>
    public int Count => GetChildElements("Page").Count();
}
