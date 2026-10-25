using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Graph.Tight.Method;

/// <summary>
/// 线段：从当前点连线到结束点，并把当前点移动到结束点。
/// 对应 Java: org.ofdrw.core.graph.tight.method.Line
/// </summary>
public sealed class Line : Command
{
    public Line() : base("Line") { }

    public Line(XElement element) : base(element) { }

    public Line(STPos point1) : this() => Point1 = point1;

    public Line(double x, double y) : this(new STPos(x, y)) { }

    /// <summary>【必选 属性】线段的结束点。</summary>
    public STPos? Point1
    {
        get
        {
            var v = GetAttr("Point1");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("Point1", value?.ToString());
    }

    /// <inheritdoc />
    public override double[] OperandValues => Point1 is { } p ? new[] { p.X, p.Y } : new double[] { 0, 0 };

    /// <inheritdoc />
    public override string ToPathData() => $"L {(Point1?.ToString() ?? "0 0")}";
}
