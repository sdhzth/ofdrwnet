using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicType;
using NetOfd.Reader.Model;

namespace NetOfd.Reader;

/// <summary>
/// 页面信息，包含页面大小、页面对象、模板引用与绝对路径等元数据。
/// 对应 Java: org.ofdrw.reader.PageInfo
/// </summary>
public class PageInfo
{
    /// <summary>页面物理大小</summary>
    public STBox? Size { get; private set; }

    /// <summary>页面对象</summary>
    public PageObject? Obj { get; private set; }

    /// <summary>页面在 OFD 中的对象 ID</summary>
    public STID? Id { get; private set; }

    /// <summary>页码（从 1 起）</summary>
    public int Index { get; private set; }

    /// <summary>页面使用的模板页列表</summary>
    public List<TemplatePageEntity> Templates { get; private set; } = new();

    /// <summary>页面文件在文档中的绝对路径</summary>
    public STLoc? PageAbsLoc { get; private set; }

    /// <summary>Page_N 目录中的 N（序号）</summary>
    public int PageN { get; private set; }

    public PageInfo() { }

    public PageInfo SetSize(STBox size)
    {
        Size = size;
        return this;
    }

    public PageInfo SetObj(PageObject obj)
    {
        Obj = obj;
        return this;
    }

    public PageInfo SetId(STID? id)
    {
        Id = id;
        return this;
    }

    public PageInfo SetIndex(int index)
    {
        Index = index;
        return this;
    }

    public PageInfo SetPageAbsLoc(STLoc pageAbsLoc)
    {
        PageAbsLoc = pageAbsLoc;
        return this;
    }

    public PageInfo SetPageN(int pageN)
    {
        PageN = pageN;
        return this;
    }

    public PageInfo SetTemplates(List<TemplatePageEntity> templates)
    {
        Templates = templates;
        return this;
    }

    /// <summary>
    /// 获取按 Z 序（含页面主体）排序的页面列表。
    /// 模板页按绘制顺序排序，最后追加页面主体，返回所有参与绘制的页面对象。
    /// </summary>
    public List<PageObject> GetOrderRelatedPageList()
    {
        var res = new List<TemplatePageEntity>(Templates);
        if (Obj is not null)
            res.Add(new TemplatePageEntity(LayerType.Body, Obj));
        res.Sort((a, b) => ((int)a.ZOrder).CompareTo((int)b.ZOrder));
        return res.Select(t => t.Page).ToList();
    }

    /// <summary>
    /// 获取页面所有图层（含模板页图层），按绘制顺序排列。
    /// </summary>
    public List<CTLayer> GetAllLayer()
    {
        var layerList = new List<CTLayer>();
        foreach (var page in GetOrderRelatedPageList())
        {
            if (page.Content is not null)
                layerList.AddRange(page.Content.GetLayersInZOrder());
        }
        return layerList;
    }
}