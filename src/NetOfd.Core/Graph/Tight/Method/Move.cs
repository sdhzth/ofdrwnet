using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Graph.Tight.Method;

/// <summary>
/// 移动节点：将当前绘制点移动到新位置。
/// 对应 Java: org.ofdrw.core.graph.tight.method.Move
/// </summary>
public sealed class Move : Command
{
    public Move() : base("Move") { }

    public Move(XElement element) : base(element) { }

    public Move(STPos point1) : this() => Point1 = point1;

    public Move(double x, double y) : this(new STPos(x, y)) { }

    /// <summary>【必选 属性】移动后新的当前绘制点。</summary>
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
    public override string ToPathData() => $"M {(Point1?.ToString() ?? "0 0")}";
}
