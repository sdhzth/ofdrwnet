using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription;

/// <summary>
/// CT_GraphicUnit - 所有可视页面内容的抽象基类。
/// 对应 Java: org.ofdrw.core.pageDescription.CT_GraphicUnit
/// </summary>
public abstract class CTGraphicUnit : OFDElement
{
    protected CTGraphicUnit(string localName) : base(localName) { }
    protected CTGraphicUnit(XElement element) : base(element) { }

    // ── ID ──────────────────────────────────────────────────
    public STID? ObjID
    {
        get
        {
            long v = GetLongAttr("ID", -1);
            return v < 0 ? null : new STID(v);
        }
        set => SetAttr("ID", value?.Value.ToString());
    }

    // ── Boundary（包围盒）───────────────────────────────────
    /// <summary>对象边界框（必须属性）</summary>
    public STBox? Boundary
    {
        get
        {
            var v = GetAttr("Boundary");
            return v is null ? null : STBox.Parse(v);
        }
        set => SetAttr("Boundary", value?.ToString());
    }

    // ── CTM（坐标变换矩阵）──────────────────────────────────
    /// <summary>坐标变换矩阵（6 元素 CTM，OFD 属性）</summary>
    /// <remarks>
    /// 依据 GB/T 33190，<c>CTM</c> 是 <c>CT_GraphicUnit</c> 的<b>属性</b>（与 <c>Boundary</c> 同级），
    /// 不是子元素。早期版本误当成子元素 <c>&lt;CTM&gt;</c> 读写，导致带 CTM 的图元在渲染/
    /// 转换时丢失变换。此处修正为属性，并可与 Archive 的 <c>AttrTrim("CTM")</c> 读取保持一致。
    /// </remarks>
    public STArray? CTM
    {
        get
        {
            var v = GetAttr("CTM");
            return v is null ? null : STArray.Parse(v);
        }
        set => SetAttr("CTM", value?.ToString());
    }

    // ── 可见性 ──────────────────────────────────────────────
    public bool Visible
    {
        get => GetBoolAttr("Visible", true);
        set => SetBoolAttr("Visible", value);
    }

    // ── 绘制参数引用 ─────────────────────────────────────────
    public STRefID? DrawParam
    {
        get
        {
            var v = GetAttr("DrawParam");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("DrawParam", value?.Value.ToString());
    }

    // ── 透明度 ───────────────────────────────────────────────
    /// <summary>
    /// 对象不透明度（0-255，缺省 255 表示完全不透明）。
    /// 最终有效透明度 = 图层 DrawParam α × 图元 DrawParam α × 本属性 α（累积乘积）。
    /// </summary>
    public double? Alpha
    {
        get
        {
            var v = GetAttr("Alpha");
            return v is null ? null : STBase.ToDouble(v);
        }
        set => SetAttr("Alpha", value?.ToString());
    }

    // ── LineWidth ────────────────────────────────────────────
    /// <summary>
    /// 线宽（mm）。GB/T 33190 与 Java CT_GraphicUnit 均为**属性**形式（addAttribute("LineWidth", ...)）。
    /// 写一律落属性；读回退历史私有写法 <c>&lt;LineWidth&gt;</c> 子元素（旧版 NetOfd 产物）以兼容。
    /// </summary>
    public double? LineWidth
    {
        get
        {
            var v = GetAttr("LineWidth");
            if (v is null) v = GetChildText("LineWidth");   // 读兼容：旧 NetOfd 子元素写法
            return v is null ? null : STBase.ToDouble(v);
        }
        set
        {
            // 保证表达单一：写新值时清理旧子元素；置 null 时同步移除
            GetChildElement("LineWidth")?.Remove();
            SetAttr("LineWidth", value.HasValue ? STBase.Fmt(value.Value) : null);
        }
    }

    /// <summary>线宽原始字符串（读兼容入口，供 DrawParam 合并判断是否显式设置）。</summary>
    internal string? RawLineWidthAttr => GetAttr("LineWidth") ?? GetChildText("LineWidth");

    // ── 剪切区域 ─────────────────────────────────────────────
    /// <summary>设置裁剪区域（覆盖式）；传 null 移除。</summary>
    public void SetClips(PageDescription.CT_Clips? clips)
    {
        GetChildElement("Clips")?.Remove();
        if (clips is not null) SetOFDEntity(clips);
    }

    /// <summary>读取裁剪区域；未设置返回 null。</summary>
    public PageDescription.CT_Clips? GetClips()
        => GetChildElement("Clips") is { } e ? new PageDescription.CT_Clips(e) : null;

    // ── 颜色 ─────────────────────────────────────────────────
    public Color.CT_Color? StrokeColor
    {
        get
        {
            var e = GetChildElement("StrokeColor");
            return e is null ? null : new Color.CT_Color(e);
        }
        set
        {
            if (value is null) GetChildElement("StrokeColor")?.Remove();
            else
            {
                value._el.Name = OfdNs + "StrokeColor";
                SetOFDEntity(value);
            }
        }
    }

    public Color.CT_Color? FillColor
    {
        get
        {
            var e = GetChildElement("FillColor");
            return e is null ? null : new Color.CT_Color(e);
        }
        set
        {
            if (value is null) GetChildElement("FillColor")?.Remove();
            else
            {
                value._el.Name = OfdNs + "FillColor";
                SetOFDEntity(value);
            }
        }
    }
}
