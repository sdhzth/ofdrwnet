using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Graph.Tight.Method;

/// <summary>
/// 二次贝塞尔曲线（图 52）。
/// 公式：B(t) = (1-t)²P0 + 2t(1-t)P1 + t²P2，t ∈ [0,1]
/// 对应 Java: org.ofdrw.core.graph.tight.method.QuadraticBezier
/// </summary>
public sealed class QuadraticBezier : Command
{
    public QuadraticBezier() : base("QuadraticBezier") { }

    public QuadraticBezier(XElement element) : base(element) { }

    public QuadraticBezier(STPos point1, STPos point2) : this()
    {
        Point1 = point1;
        Point2 = point2;
    }

    public QuadraticBezier(double x1, double y1, double x2, double y2)
        : this(new STPos(x1, y1), new STPos(x2, y2)) { }

    /// <summary>【必选 属性】控制点。</summary>
    public STPos? Point1
    {
        get
        {
            var v = GetAttr("Point1");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("Point1", value?.ToString());
    }

    /// <summary>【必选 属性】结束点。</summary>
    public STPos? Point2
    {
        get
        {
            var v = GetAttr("Point2");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("Point2", value?.ToString());
    }

    /// <inheritdoc />
    public override double[] OperandValues => new[]
    {
        Point1?.X ?? 0, Point1?.Y ?? 0,
        Point2?.X ?? 0, Point2?.Y ?? 0
    };

    /// <inheritdoc />
    public override string ToPathData() =>
        $"Q {(Point1?.ToString() ?? "0 0")} {(Point2?.ToString() ?? "0 0")}";
}
