using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;

namespace NetOfd.Reader.Model;

/// <summary>
/// 页面模板实体，包含模板信息（CT_TemplatePage）与模板页对象（Page）。
/// 对应 Java: org.ofdrw.reader.model.TemplatePageEntity
/// </summary>
public class TemplatePageEntity
{
    /// <summary>模板信息</summary>
    private readonly CTTemplatePage _tplInfo;

    /// <summary>模板页对象</summary>
    public PageObject Page { get; }

    /// <summary>模板页绘制顺序</summary>
    public LayerType Order { get; private set; }

    public TemplatePageEntity(CTTemplatePage tplInfo, PageObject page)
    {
        _tplInfo = tplInfo;
        Page = page;
        Order = tplInfo.ZOrder;
    }

    public TemplatePageEntity(LayerType order, PageObject page)
    {
        _tplInfo = new CTTemplatePage();
        Page = page;
        Order = order;
    }

    /// <summary>模板页 ID</summary>
    public STID? ID => _tplInfo.ID;

    /// <summary>模板页名称（可为 null）</summary>
    public string? TemplatePageName => _tplInfo.Name;

    /// <summary>模板页默认绘制顺序</summary>
    public LayerType ZOrder => Order;

    /// <summary>设置新的绘制顺序</summary>
    public void SetOrder(LayerType order)
    {
        Order = order;
    }

    /// <summary>模板页内容文件路径（相对 Doc 目录）</summary>
    public STLoc? BaseLoc => _tplInfo.BaseLoc;

    /// <summary>模板信息</summary>
    public CTTemplatePage TplInfo => _tplInfo;
}