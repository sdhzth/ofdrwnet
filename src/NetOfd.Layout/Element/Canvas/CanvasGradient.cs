using NetOfd.Core.PageDescription.Color;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// Canvas 线性（轴向）渐变，API 与 HTML Canvas 的 createLinearGradient 兼容。
/// <para>
/// 用法：
/// <code>
/// var g = ctx.CreateLinearGradient(0, 0, 100, 100)
///            .AddColorStop(0, "#FF0000")
///            .AddColorStop(1, "blue");
/// ctx.FillStyle = g;
/// ctx.FillRect(0, 0, 100, 100);
/// </code>
/// </para>
/// 对应 Java: org.ofdrw.layout.element.canvas.CanvasGradient
/// </summary>
public sealed class CanvasGradient
{
    /// <summary>底层 OFD 轴向渐变底纹（GB/T 33190 §8.3.4.2）。</summary>
    public CT_AxialShd AxialShd { get; }

    /// <summary>创建线性渐变：(x0,y0) → (x1,y1) 为渐变轴。</summary>
    public CanvasGradient(double x0, double y0, double x1, double y1)
    {
        AxialShd = new CT_AxialShd(x0, y0, x1, y1);
    }

    /// <summary>
    /// 添加渐变颜色段。
    /// </summary>
    /// <param name="offset">渐变位置，取值范围 [0,1]，用于确定 StartPoint 与 EndPoint 之间的位置。</param>
    /// <param name="color">颜色值，支持 #RGB / #RRGGBB / #RRGGBBAA / rgb() / rgba() / CSS 颜色名。</param>
    public CanvasGradient AddColorStop(double offset, string color)
    {
        var c = GradientColorUtil.ToCtColor(color);
        if (c is null) return this; // 与 Java 一致：无法解析的颜色直接忽略
        AxialShd.AddSegment(new Segment(offset, c));
        return this;
    }

    /// <summary>设置渐变绘制方式（Direct / Repeat / Reflect）。</summary>
    public CanvasGradient SetMapType(MapType mapType)
    {
        AxialShd.MapType = mapType;
        return this;
    }

    /// <summary>设置渐变区间长度（仅 MapType 非 Direct 时有意义）。</summary>
    public CanvasGradient SetMapUnit(double mapUnit)
    {
        AxialShd.MapUnit = mapUnit;
        return this;
    }

    /// <summary>设置轴线延长线方向是否继续绘制渐变。</summary>
    public CanvasGradient SetExtend(Extend extend)
    {
        AxialShd.Extend = extend;
        return this;
    }
}
