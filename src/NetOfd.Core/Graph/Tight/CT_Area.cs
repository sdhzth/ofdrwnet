using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.Graph.PathObj;
using NetOfd.Core.Graph.Tight.Method;

namespace NetOfd.Core.Graph.Tight;

/// <summary>
/// 区域中的一条分路径（Area），由起始点 + 绘制指令序列构成，路径闭合。
/// 图 49 区域结构
/// 对应 Java: org.ofdrw.core.graph.tight.CT_Area
/// </summary>
public sealed class CT_Area : OFDElement
{
    public CT_Area() : base("Area") { }

    public CT_Area(XElement element) : base(element) { }

    /// <summary>【必选 属性】定义自绘制图形的起始点坐标。</summary>
    public STPos? Start
    {
        get
        {
            var v = GetAttr("Start");
            return v is null ? null : STPos.Parse(v);
        }
        set => SetAttr("Start", value?.ToString());
    }

    /// <summary>【必选】追加一条绘制指令（移动/画线/圆弧等）。</summary>
    public CT_Area AddCommand(Command command)
    {
        if (command is not null) AddOFDEntity(command);
        return this;
    }

    /// <summary>连续绘制，等同 <see cref="AddCommand"/>。</summary>
    public CT_Area Next(Command command) => AddCommand(command);

    /// <summary>
    /// 【必选】获取绘制指令序列（顺序决定绘制图形）。遇到未知指令名抛 <see cref="ArgumentException"/>。
    /// 对应 Java: CT_Area#getCommands
    /// </summary>
    public List<Command> GetCommands()
    {
        var res = new List<Command>();
        foreach (var child in _el.Elements())
        {
            // 兼容 ofd: 前缀与无命名空间两种写法
            switch (child.Name.LocalName)
            {
                case "Move":
                    res.Add(new Move(child));
                    break;
                case "Line":
                    res.Add(new Line(child));
                    break;
                case "QuadraticBezier":
                    res.Add(new QuadraticBezier(child));
                    break;
                case "CubicBezier":
                    res.Add(new CubicBezier(child));
                    break;
                case "Arc":
                    res.Add(new Arc(child));
                    break;
                case "Close":
                    res.Add(new Close(child));
                    break;
                default:
                    throw new ArgumentException($"未知的绘制指令类型：{child.Name.LocalName}");
            }
        }
        return res;
    }

    /// <summary>
    /// 转换为紧缩轮廓数据：起始点写作 <c>S x y</c>，其后依次拼接各指令片段。
    /// 用于把 XML 负载型描述转回 <see cref="AbbreviatedData"/>。
    /// </summary>
    public AbbreviatedData ToAbbreviatedData()
    {
        var data = new AbbreviatedData();
        var start = Start;
        if (start is not null) data.S(start);
        foreach (var cmd in GetCommands())
        {
            var opt = cmd switch
            {
                Move => "M",
                Line => "L",
                QuadraticBezier => "Q",
                CubicBezier => "B",
                Arc => "A",
                Close => "C",
                _ => (string?)null
            };
            if (opt is null) continue;
            _ = opt switch
            {
                "M" => data.M(cmd.OperandValues[0], cmd.OperandValues[1]),
                "L" => data.L(cmd.OperandValues[0], cmd.OperandValues[1]),
                "Q" => data.Q(cmd.OperandValues[0], cmd.OperandValues[1],
                              cmd.OperandValues[2], cmd.OperandValues[3]),
                "B" => data.B(cmd.OperandValues[0], cmd.OperandValues[1],
                              cmd.OperandValues[2], cmd.OperandValues[3],
                              cmd.OperandValues[4], cmd.OperandValues[5]),
                "A" => data.A(cmd.OperandValues[0], cmd.OperandValues[1], cmd.OperandValues[2],
                              (int)cmd.OperandValues[3], (int)cmd.OperandValues[4],
                              cmd.OperandValues[5], cmd.OperandValues[6]),
                _ => data.C()
            };
        }
        return data.Flush();
    }
}
