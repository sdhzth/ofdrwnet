using NetOfd.Core.PageDescription.Color;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// Canvas 放射状（径向）渐变，API 与 HTML Canvas 的 createRadialGradient 兼容。
/// <para>
/// 用法：
/// <code>
/// var g = ctx.CreateRadialGradient(50, 50, 0, 50, 50, 40)
///            .AddColorStop(0, "white")
///            .AddColorStop(1, "#0000FF");
/// ctx.FillStyle = g;
/// ctx.FillRect(0, 0, 100, 100);
/// </code>
/// </para>
/// 对应 Java: org.ofdrw.layout.element.canvas.CanvasRadialGradient
/// </summary>
public sealed class CanvasRadialGradient
{
    /// <summary>底层 OFD 径向渐变底纹（GB/T 33190 §8.3.4.2）。</summary>
    public CT_RadialShd RadialShd { get; }

    /// <summary>
    /// 创建放射状渐变：由起始圆 (x0,y0,r0) 渐变到结束圆 (x1,y1,r1)。
    /// </summary>
    public CanvasRadialGradient(double x0, double y0, double r0, double x1, double y1, double r1)
    {
        RadialShd = new CT_RadialShd
        {
            StartPoint = new NetOfd.Core.BasicType.STPos(x0, y0),
            StartRadius = r0,
            EndPoint = new NetOfd.Core.BasicType.STPos(x1, y1),
            EndRadius = r1,
            // 与 Java 一致：径向渐变默认向起始点方向延伸
            Extend = Extend.Start
        };
    }

    /// <summary>
    /// 添加渐变颜色段。
    /// </summary>
    /// <param name="offset">渐变位置，取值范围 [0,1]。</param>
    /// <param name="color">颜色值，支持 #RGB / #RRGGBB / #RRGGBBAA / rgb() / rgba() / CSS 颜色名。</param>
    public CanvasRadialGradient AddColorStop(double offset, string color)
    {
        var c = GradientColorUtil.ToCtColor(color);
        if (c is null) return this; // 与 Java 一致：无法解析的颜色直接忽略
        RadialShd.AddSegment(new Segment(offset, c));
        return this;
    }

    /// <summary>设置椭圆离心率 [0,1]，0 表示圆。</summary>
    public CanvasRadialGradient SetEccentricity(double eccentricity)
    {
        RadialShd.Eccentricity = eccentricity;
        return this;
    }

    /// <summary>设置椭圆倾斜角度（度）。</summary>
    public CanvasRadialGradient SetAngle(double angle)
    {
        RadialShd.Angle = angle;
        return this;
    }

    /// <summary>设置渐变绘制方式（Direct / Repeat / Reflect）。</summary>
    public CanvasRadialGradient SetMapType(MapType mapType)
    {
        RadialShd.MapType = mapType;
        return this;
    }

    /// <summary>设置轴线延长线方向是否继续绘制渐变。</summary>
    public CanvasRadialGradient SetExtend(Extend extend)
    {
        RadialShd.Extend = extend;
        return this;
    }
}
