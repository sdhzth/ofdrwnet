using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Graph.Tight.Method;

/// <summary>
/// 三次贝塞尔曲线（图 53）。
/// 公式：B(t) = (1-t)³P0 + 3t(1-t)²P1 + 3t²(1-t)P2 + t³P3，t ∈ [0,1]
/// 对应 Java: org.ofdrw.core.graph.tight.method.CubicBezier
/// </summary>
public sealed class CubicBezier : Command
{
    public CubicBezier() : base("CubicBezier") { }

    public CubicBezier(XElement element) : base(element) { }

    public CubicBezier(STPos point1, STPos point2, STPos point3) : this()
    {
        Point1 = point1;
        Point2 = point2;
        Point3 = point3;
    }

    public CubicBezier(double x1, double y1, double x2, double y2, double x3, double y3)
        : this(new STPos(x1, y1), new STPos(x2, y2), new STPos(x3, y3)) { }

    /// <summary>【必选 属性】第一个控制点。</summary>
    public STPos? Point1
    {
        get
        {
            var v = GetAttr("Point1");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("Point1", value?.ToString());
    }

    /// <summary>【必选 属性】第二个控制点。</summary>
    public STPos? Point2
    {
        get
        {
            var v = GetAttr("Point2");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("Point2", value?.ToString());
    }

    /// <summary>【必选 属性】结束点，同时是下一路径的起点。</summary>
    public STPos? Point3
    {
        get
        {
            var v = GetAttr("Point3");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("Point3", value?.ToString());
    }

    /// <inheritdoc />
    public override double[] OperandValues => new[]
    {
        Point1?.X ?? 0, Point1?.Y ?? 0,
        Point2?.X ?? 0, Point2?.Y ?? 0,
        Point3?.X ?? 0, Point3?.Y ?? 0
    };

    /// <inheritdoc />
    public override string ToPathData() =>
        $"B {(Point1?.ToString() ?? "0 0")} {(Point2?.ToString() ?? "0 0")} {(Point3?.ToString() ?? "0 0")}";
}
