using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Graph.Tight.Method;

/// <summary>
/// 圆弧（图 56 圆弧结构）。
/// 对应 Java: org.ofdrw.core.graph.tight.method.Arc
/// </summary>
public sealed class Arc : Command
{
    public Arc() : base("Arc") { }

    public Arc(XElement element) : base(element) { }

    /// <summary>
    /// 以 (长短轴, 旋转角, 是否大弧, 是否顺时针, 结束点) 构造。
    /// large / sweep 非 0 即视为 true（与 Java 一致）。
    /// </summary>
    public Arc(double rx, double ry, double angle, double large, double sweep, double x, double y)
        : this()
    {
        EllipseSize = new STArray(new[] { rx, ry });
        RotationAngle = angle;
        LargeArc = large != 0;
        SweepDirection = sweep != 0;
        EndPoint = new STPos(x, y);
    }

    /// <summary>
    /// 【必选 属性】弧线方向是否顺时针。
    /// true - 由圆弧起始点到结束点是顺时针；false - 逆时针。
    /// </summary>
    public bool SweepDirection
    {
        get => GetBoolAttr("SweepDirection", false);
        set => SetBoolAttr("SweepDirection", value);
    }

    /// <summary>
    /// 【必选 属性】是否是大圆弧。
    /// true - 对应度数大于 180° 的弧；false - 小于 180° 的弧。
    /// </summary>
    public bool LargeArc
    {
        get => GetBoolAttr("LargeArc", false);
        set => SetBoolAttr("LargeArc", value);
    }

    /// <summary>
    /// 【必选 属性】按 EllipseSize 绘制的椭圆在当前坐标系下旋转的角度，
    /// 正值为顺时针，负值为逆时针。大于 360° 时以 360° 取模。
    /// </summary>
    public double RotationAngle
    {
        get => GetDoubleAttr("RotationAngle", 0) % 360.0d;
        set => SetDoubleAttr("RotationAngle", value % 360.0d);
    }

    /// <summary>
    /// 【必选 属性】长短轴，形如 <c>200 100</c> 的 2 元数组，较大者为长轴。
    /// </summary>
    public STArray? EllipseSize
    {
        get
        {
            var v = GetAttr("EllipseSize");
            return v is null ? null : STArray.Parse(v);
        }
        set => SetAttr("EllipseSize", value?.ToString());
    }

    /// <summary>【必选 属性】圆弧结束点，同时是下一个路径的起点。</summary>
    public STPos? EndPoint
    {
        get
        {
            var v = GetAttr("EndPoint");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("EndPoint", value?.ToString());
    }

    /// <inheritdoc />
    public override double[] OperandValues
    {
        get
        {
            var sz = EllipseSize;
            double rx = sz is { Count: > 0 } ? sz[0] : 0;
            double ry = sz is { Count: > 1 } ? sz[1] : 0;
            var ep = EndPoint;
            return new[]
            {
                rx, ry, RotationAngle,
                LargeArc ? 1d : 0d,
                SweepDirection ? 1d : 0d,
                ep?.X ?? 0, ep?.Y ?? 0
            };
        }
    }

    /// <inheritdoc />
    public override string ToPathData()
    {
        var sz = EllipseSize;
        string ellipse = sz?.Count switch
        {
            null or 0 => "0 0",
            1 => $"{STBase.Fmt(sz[0])} 0",
            _ => $"{STBase.Fmt(sz[0])} {STBase.Fmt(sz[1])}"
        };
        return $"A {ellipse} {STBase.Fmt(RotationAngle)} " +
               $"{(LargeArc ? 1 : 0)} {(SweepDirection ? 1 : 0)} " +
               $"{(EndPoint?.ToString() ?? "0 0")}";
    }
}
