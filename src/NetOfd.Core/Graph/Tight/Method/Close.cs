using System.Xml.Linq;

namespace NetOfd.Core.Graph.Tight.Method;

/// <summary>
/// 闭合：自动闭合到当前路径的起始点，并以该点为当前点。
/// 对应 Java: org.ofdrw.core.graph.tight.method.Close
/// </summary>
public sealed class Close : Command
{
    public Close() : base("Close") { }

    public Close(XElement element) : base(element) { }

    /// <inheritdoc />
    public override double[] OperandValues => Array.Empty<double>();

    /// <inheritdoc />
    public override string ToPathData() => "C";
}
