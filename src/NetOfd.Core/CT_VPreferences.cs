using System.Linq;
using NetOfd.Core.BasicStructure.Doc.VPreferences;
using NetOfd.Core.BasicStructure.Doc.VPreferences.Zoom;
using ZoomModeNode = NetOfd.Core.BasicStructure.Doc.VPreferences.Zoom.ZoomMode;
using ZoomNode = NetOfd.Core.BasicStructure.Doc.VPreferences.Zoom.Zoom;

namespace NetOfd.Core;

/// <summary>
/// 文档首选项（VPreferences），控制 PDF 查看器的初始显示状态。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.CT_VPreferences
/// </summary>
public class CT_VPreferences : OFDElement
{
    /// <summary>
    /// 无参构造函数，创建一个空的首选项节点。
    /// </summary>
    public CT_VPreferences()
        : base("VPreferences")
    {
    }

    /// <summary>
    /// 是否在标题栏显示文档标题
    /// <para>
    /// 默认值 true
    /// </para>
    /// </summary>
    public bool DisplayDocTitle
    {
        get
        {
            var el = _el.Element(OfdNs + "DisplayDocTitle");
            return el != null && Boolean.Parse(el.Value);
        }
        set => _el.Element(OfdNs + "DisplayDocTitle")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 是否在标题栏显示文档标题（兼容旧版属性名）
    /// <para>
    /// 默认值 true
    /// </para>
    /// </summary>
    public bool DisplayDocumentTitle
    {
        get
        {
            var el = _el.Element(OfdNs + "DisplayDocumentTitle");
            return el != null && Boolean.Parse(el.Value);
        }
        set => _el.Element(OfdNs + "DisplayDocumentTitle")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 是否显示文档大纲
    /// <para>
    /// 默认值 true
    /// </para>
    /// </summary>
    public bool DisplayDocMap
    {
        get
        {
            var el = _el.Element(OfdNs + "DisplayDocMap");
            return el != null && Boolean.Parse(el.Value);
        }
        set => _el.Element(OfdNs + "DisplayDocMap")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 非全屏页面显示模式
    /// <para>
    /// 0 - 单页模式，1 - 连续页模式，2 - 概览模式等
    /// 默认值 0
    /// </para>
    /// </summary>
    public int NonFullScreenPageMode
    {
        get
        {
            var el = _el.Element(OfdNs + "NonFullScreenPageMode");
            return el != null ? int.Parse(el.Value) : 0;
        }
        set => _el.Element(OfdNs + "NonFullScreenPageMode")?.SetValue(value.ToString());
    }

    /// <summary>
    /// 鼠标工具类型
    /// <para>
    /// 默认值 Arrow
    /// </para>
    /// </summary>
    public string CursorTool
    {
        get
        {
            var el = _el.Element(OfdNs + "CursorTool");
            return el != null ? el.Value : "Arrow";
        }
        set => _el.Element(OfdNs + "CursorTool")?.SetValue(value);
    }

    /// <summary>
    /// 缩放比例
    /// <para>
    /// 默认值 1.0
    /// </para>
    /// </summary>
    public double Zoom
    {
        get
        {
            var el = _el.Element(OfdNs + "Zoom");
            return el != null ? double.Parse(el.Value) : 1.0;
        }
        set => _el.Element(OfdNs + "Zoom")?.SetValue(value.ToString("G"));
    }

    /// <summary>
    /// 页面中心点的 X 坐标（以页面宽度的一半为基准）
    /// <para>
    /// 默认值 0.5（相对于页面宽度）
    /// </para>
    /// </summary>
    public double CenterX
    {
        get
        {
            var el = _el.Element(OfdNs + "CenterX");
            return el != null ? double.Parse(el.Value) : 0.5;
        }
        set => _el.Element(OfdNs + "CenterX")?.SetValue(value.ToString("G"));
    }

    /// <summary>
    /// 页面中心点的 Y 坐标（以页面高度的一半为基准）
    /// <para>
    /// 默认值 0.5（相对于页面高度）
    /// </para>
    /// </summary>
    public double CenterY
    {
        get
        {
            var el = _el.Element(OfdNs + "CenterY");
            return el != null ? double.Parse(el.Value) : 0.5;
        }
        set => _el.Element(OfdNs + "CenterY")?.SetValue(value.ToString("G"));
    }

    /// <summary>
    /// 是否以中心点为原点显示
    /// <para>
    /// 默认值 false
    /// </para>
    /// </summary>
    public bool fromCenter
    {
        get
        {
            var el = _el.Element(OfdNs + "fromCenter");
            return el != null && Boolean.Parse(el.Value);
        }
        set => _el.Element(OfdNs + "fromCenter")?.SetValue(value.ToString().ToLower());
    }

    // ── 7.5 表 9 视图首选项属性 ───────────────────────────────

    /// <summary>
    /// 窗口模式，可选值见 <see cref="PageMode"/>；默认 <see cref="PageMode.None"/>。
    /// 对应 Java: CT_VPreferences#getPageMode
    /// </summary>
    public PageMode PageMode
    {
        get => (GetChildText("PageMode") ?? string.Empty).ToPageMode();
        set => SetChildText("PageMode", value.ToString());
    }

    /// <summary>
    /// 页面布局模式，可选值见 <see cref="PageLayout"/>；默认 <see cref="PageLayout.OnePage"/>。
    /// 对应 Java: CT_VPreferences#getPageLayout
    /// </summary>
    public PageLayout PageLayout
    {
        get => (GetChildText("PageLayout") ?? string.Empty).ToPageLayout();
        set => SetChildText("PageLayout", value.ToString());
    }

    /// <summary>
    /// 标题栏显示模式；默认 <see cref="TabDisplay.FileName"/>。
    /// 对应 Java: CT_VPreferences#getTabDisplay
    /// </summary>
    public TabDisplay TabDisplay
    {
        get => (GetChildText("TabDisplay") ?? string.Empty).ToTabDisplay();
        set => SetChildText("TabDisplay", value.ToString());
    }

    /// <summary>是否隐藏工具栏，默认 false。</summary>
    public bool HideToolbar
    {
        get => GetBoolChild("HideToolbar");
        set => SetChildText("HideToolbar", value.ToString().ToLowerInvariant());
    }

    /// <summary>是否隐藏菜单栏，默认 false。</summary>
    public bool HideMenubar
    {
        get => GetBoolChild("HideMenubar");
        set => SetChildText("HideMenubar", value.ToString().ToLowerInvariant());
    }

    /// <summary>是否隐藏主窗口之外的其他窗口组件，默认 false。</summary>
    public bool HideWindowUI
    {
        get => GetBoolChild("HideWindowUI");
        set => SetChildText("HideWindowUI", value.ToString().ToLowerInvariant());
    }

    /// <summary>自动缩放模式；不存在 ZoomMode 节点时返回 null。</summary>
    public ZoomModeType? ZoomModeValue
    {
        get
        {
            var el = GetChildElement("ZoomMode");
            return el is null ? null : new ZoomModeNode(el).ModeType;
        }
    }

    /// <summary>
    /// 设置文档自动缩放模式（互斥：先移除已有的 Zoom / ZoomMode 节点）。
    /// 对应 Java: CT_VPreferences#setZoomMode
    /// </summary>
    public CT_VPreferences SetZoomMode(ZoomModeType mode)
    {
        RemoveZoomNodes();
        AddOFDEntity(ZoomModeNode.GetInstance(mode));
        return this;
    }

    /// <summary>
    /// 设置文档缩放率（互斥：先移除已有的 Zoom / ZoomMode 节点）。
    /// 对应 Java: CT_VPreferences#setZoom(double)
    /// </summary>
    public CT_VPreferences SetZoomScale(double zoom)
    {
        RemoveZoomNodes();
        AddOFDEntity(new ZoomNode(zoom));
        return this;
    }

    /// <summary>
    /// 获取缩放处理方式：<see cref="ZoomModeNode"/>（自动）或 <see cref="ZoomNode"/>（缩放率），
    /// 二者都不存在时返回 null。对应 Java: CT_VPreferences#getZoomScale
    /// </summary>
    public ZoomScale? GetZoomScale()
    {
        var zm = GetChildElement("ZoomMode");
        if (zm is not null) return new ZoomModeNode(zm);

        var z = GetChildElement("Zoom");
        return z is not null ? new ZoomNode(z) : null;
    }

    private bool GetBoolChild(string localName) =>
        bool.TryParse(GetChildText(localName)?.Trim(), out var b) && b;

    /// <summary>移除所有 Zoom / ZoomMode 子节点（二者互斥）。</summary>
    private void RemoveZoomNodes()
    {
        foreach (var e in GetChildElements("Zoom").Concat(GetChildElements("ZoomMode")).ToList())
            e.Remove();
    }
}