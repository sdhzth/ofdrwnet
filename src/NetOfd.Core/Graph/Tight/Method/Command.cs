using System.Xml.Linq;

namespace NetOfd.Core.Graph.Tight.Method;

/// <summary>
/// 路径绘制指令基类（表 37 图形对象描述方法）。
/// 对应 Java: org.ofdrw.core.graph.tight.method.Command
/// </summary>
public abstract class Command : OFDElement
{
    /// <summary>以既有 XML 元素构造。</summary>
    protected Command(XElement element) : base(element) { }

    /// <summary>以元素名构造。</summary>
    protected Command(string name) : base(name) { }

    /// <summary>
    /// 生成紧缩路径片段（如 <c>M 10 10</c>、<c>C</c>）。
    /// 对应 Java: Command 各子类的 toString()
    /// </summary>
    public abstract string ToPathData();

    /// <summary>
    /// 与本指令等价的操作数序列（不含操作符），用于转换为 <see cref="PathObj.OptVal"/>。
    /// </summary>
    public abstract double[] OperandValues { get; }
}
