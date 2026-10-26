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

    /// <summary>
    /// 在指定位置插入页面节点（0-based）。
    /// <para>
    /// 对应 Java: <c>Pages.elements().add(index, page)</c>。
    /// 文档中页面的先后顺序由本列表决定，与物理目录 <c>Page_N</c> 的编号无关。
    /// </para>
    /// <para>
    /// 插入位置越界时自动钳制：小于 0 取 0（插到最前），大于等于当前页数取当前页数（追加末尾）。
    /// </para>
    /// </summary>
    /// <param name="index">插入位置（0-based，0 表示插到最前）</param>
    /// <param name="page">页面节点</param>
    public Pages InsertPage(int index, Page page)
    {
        var children = GetChildElements("Page").ToList();
        int pos = index < 0 ? 0 : (index > children.Count ? children.Count : index);

        if (pos >= children.Count)
        {
            // 追加到末尾
            AddOFDEntity(page);
        }
        else
        {
            // 插到第 pos 个 <Page> 之前
            children[pos].AddBeforeSelf(page._el);
        }
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
