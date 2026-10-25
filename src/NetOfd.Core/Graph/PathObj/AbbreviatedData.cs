using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.Graph.Tight.Method;

namespace NetOfd.Core.Graph.PathObj;

/// <summary>
/// 图形轮廓数据（紧缩格式）：由一系列「操作符 + 操作数」构成，
/// 形如 <c>S 0 0 M 10 10 L 20 20 C</c>。
/// <para>
/// 元素文本即紧缩串；本类负责在「紧缩串 ↔ <see cref="OptVal"/> 队列」之间双向转换，
/// 并提供流式构造 API（S/M/L/Q/B/A/C）。
/// </para>
/// 对应 Java: org.ofdrw.core.graph.pathObj.AbbreviatedData
/// </summary>
/// <remarks>
/// 与 Java 的差异：Java 用 <c>toString()</c> 输出紧缩串，C# 基类 <see cref="OFDElement.ToString()"/>
/// 已固定输出 XML，故此处改以 <see cref="ToDataString"/> / <see cref="Value"/> 输出紧缩串，
/// 避免破坏 XML 语义。
/// </remarks>
public sealed class AbbreviatedData : OFDElement
{
    private readonly List<OptVal> _dataQueue;

    /// <summary>创建空的轮廓数据。</summary>
    public AbbreviatedData() : base("AbbreviatedData")
    {
        _dataQueue = new List<OptVal>();
    }

    /// <summary>以既有 XML 元素构造，解析其文本为操作队列。</summary>
    public AbbreviatedData(XElement element) : base(element)
    {
        _dataQueue = ParseData(GetText() ?? string.Empty);
    }

    /// <summary>以操作队列构造，并立即刷新到元素文本。</summary>
    public AbbreviatedData(IEnumerable<OptVal> list) : this()
    {
        _dataQueue.AddRange(list);
        Flush();
    }

    /// <summary>底层的操作符/操作数队列（只读视图）。</summary>
    public IReadOnlyList<OptVal> RawOptVal => _dataQueue;

    /// <summary>操作数量。</summary>
    public int Size => _dataQueue.Count;

    /// <summary>
    /// 元素文本形式的紧缩串；写入会重新解析操作队列。
    /// </summary>
    public string? Value
    {
        get => GetText();
        set
        {
            _dataQueue.Clear();
            if (!string.IsNullOrWhiteSpace(value))
                _dataQueue.AddRange(ParseData(value));
            SetText(_dataQueue.Count == 0 ? null : ToDataString());
        }
    }

    // ────────────────────────────────────────────────────────
    //  解析 / 序列化
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 解析紧缩串为操作队列。多个空格视为一个；无法解析的数字按 0 处理。
    /// 对应 Java: AbbreviatedData#parse
    /// </summary>
    public static List<OptVal> ParseData(string? dataStr)
    {
        var res = new List<OptVal>();
        if (string.IsNullOrEmpty(dataStr)) return res;

        var arr = dataStr.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        string? opt = null;
        var values = new List<string>();

        foreach (var s in arr)
        {
            if (s.Length == 0) continue;
            switch (s)
            {
                case "S":
                case "M":
                case "L":
                case "Q":
                case "B":
                case "A":
                case "C":
                case "CM":
                    if (opt is not null) res.Add(new OptVal(opt, values));
                    values = new List<string>();
                    opt = s;
                    break;
                default:
                    values.Add(s);
                    break;
            }
        }
        if (opt is not null) res.Add(new OptVal(opt, values));
        return res;
    }

    /// <summary>
    /// 序列化为紧缩串（各操作符间以空格分隔；空队列返回空串）。
    /// 对应 Java: AbbreviatedData#toString
    /// </summary>
    public string ToDataString()
    {
        if (_dataQueue.Count == 0) return "";
        return string.Join(" ", _dataQueue.Select(o => o.ToString()));
    }

    /// <summary>将当前操作队列写回元素文本。</summary>
    public AbbreviatedData Flush()
    {
        SetText(_dataQueue.Count == 0 ? null : ToDataString());
        return this;
    }

    // ────────────────────────────────────────────────────────
    //  流式构造（长名 API）
    // ────────────────────────────────────────────────────────

    /// <summary>定义自绘制图形边线的起始点（S）。</summary>
    public AbbreviatedData DefineStart(double x, double y)
    {
        _dataQueue.Add(new OptVal("S", new[] { x, y }));
        return this;
    }

    /// <summary>当前点移动到目标点（M）。</summary>
    public AbbreviatedData MoveTo(double x, double y)
    {
        _dataQueue.Add(new OptVal("M", new[] { x, y }));
        return this;
    }

    /// <summary>从当前点连线到目标点（L），并把当前点移动到目标点。</summary>
    public AbbreviatedData LineTo(double x, double y)
    {
        _dataQueue.Add(new OptVal("L", new[] { x, y }));
        return this;
    }

    /// <summary>二次贝塞尔曲线（Q）：(x1,y1) 为控制点，(x2,y2) 为结束点。</summary>
    public AbbreviatedData QuadraticBezierCurve(double x1, double y1, double x2, double y2)
    {
        _dataQueue.Add(new OptVal("Q", new[] { x1, y1, x2, y2 }));
        return this;
    }

    /// <summary>三次贝塞尔曲线（B）：两个控制点 + 结束点。</summary>
    public AbbreviatedData CubicBezierCurve(double x1, double y1,
                                           double x2, double y2,
                                           double x3, double y3)
    {
        _dataQueue.Add(new OptVal("B", new[] { x1, y1, x2, y2, x3, y3 }));
        return this;
    }

    /// <summary>
    /// 圆弧（A）。large/sweep 只接受 0 或 1，否则抛 <see cref="FormatException"/>。
    /// </summary>
    public AbbreviatedData ArcTo(double rx, double ry, double angle,
                                 int large, int sweep, double x, double y)
    {
        if (large != 0 && large != 1) throw new FormatException("large 只接受 0 或 1");
        if (sweep != 0 && sweep != 1) throw new FormatException("sweep 只接受 0 或 1");
        _dataQueue.Add(new OptVal("A", new[] { rx, ry, angle, large, sweep, x, y }));
        return this;
    }

    /// <summary>子路径自动闭合（C）：当前点与子路径起始点直连。</summary>
    public AbbreviatedData ClosePath()
    {
        _dataQueue.Add(new OptVal("C"));
        return this;
    }

    // ────────────────────────────────────────────────────────
    //  流式构造（紧缩短名 API，与 Java 同名方法对应）
    // ────────────────────────────────────────────────────────

    /// <summary>S：定义起始点。</summary>
    public AbbreviatedData S(double x, double y) => DefineStart(x, y);

    /// <summary>S：定义起始点。</summary>
    public AbbreviatedData S(STPos start) => DefineStart(start.X, start.Y);

    /// <summary>M：移动到目标点。</summary>
    public AbbreviatedData M(double x, double y) => MoveTo(x, y);

    /// <summary>M：移动到目标点。</summary>
    public AbbreviatedData M(STPos target) => MoveTo(target.X, target.Y);

    /// <summary>L：连线到目标点。</summary>
    public AbbreviatedData L(double x, double y) => LineTo(x, y);

    /// <summary>L：连线到目标点。</summary>
    public AbbreviatedData L(STPos p) => LineTo(p.X, p.Y);

    /// <summary>Q：二次贝塞尔曲线。</summary>
    public AbbreviatedData Q(double x1, double y1, double x2, double y2)
        => QuadraticBezierCurve(x1, y1, x2, y2);

    /// <summary>Q：二次贝塞尔曲线。</summary>
    public AbbreviatedData Q(STPos control, STPos target)
        => QuadraticBezierCurve(control.X, control.Y, target.X, target.Y);

    /// <summary>B：三次贝塞尔曲线。</summary>
    public AbbreviatedData B(double x1, double y1, double x2, double y2, double x3, double y3)
        => CubicBezierCurve(x1, y1, x2, y2, x3, y3);

    /// <summary>B：三次贝塞尔曲线。</summary>
    public AbbreviatedData B(STPos control1, STPos control2, STPos target)
        => CubicBezierCurve(control1.X, control1.Y, control2.X, control2.Y, target.X, target.Y);

    /// <summary>A：圆弧。</summary>
    public AbbreviatedData A(double rx, double ry, double angle, int large, int sweep, double x, double y)
        => ArcTo(rx, ry, angle, large, sweep, x, y);

    /// <summary>A：圆弧。</summary>
    public AbbreviatedData A(double rx, double ry, double angle, int large, int sweep, STPos target)
        => ArcTo(rx, ry, angle, large, sweep, target.X, target.Y);

    /// <summary>C：闭合子路径。</summary>
    public AbbreviatedData C() => ClosePath();

    // ────────────────────────────────────────────────────────
    //  Misc
    // ────────────────────────────────────────────────────────

    /// <summary>追加另一条轮廓数据的全部操作。</summary>
    public AbbreviatedData Append(AbbreviatedData? another)
    {
        if (another is null) return this;
        _dataQueue.AddRange(another._dataQueue);
        return this;
    }

    /// <summary>撤销上一步操作。</summary>
    public AbbreviatedData Undo()
    {
        if (_dataQueue.Count > 0) _dataQueue.RemoveAt(_dataQueue.Count - 1);
        return this;
    }

    /// <summary>转换为非紧缩（XML 结构）绘制指令序列；无法识别的操作符被跳过。</summary>
    public List<Command> ToCommands()
    {
        var res = new List<Command>(_dataQueue.Count);
        foreach (var item in _dataQueue)
        {
            var cmd = item.ToCmd();
            if (cmd is not null) res.Add(cmd);
        }
        return res;
    }

    /// <summary>
    /// 由非紧缩绘制指令序列构造紧缩数据。
    /// 对应 Java: 逆向于 OptVal#toCmd
    /// </summary>
    public static AbbreviatedData FromCommands(IEnumerable<Command> commands)
    {
        var data = new AbbreviatedData();
        foreach (var cmd in commands)
        {
            var opt = cmd.Element.Name.LocalName switch
            {
                "Move" => "M",
                "Line" => "L",
                "QuadraticBezier" => "Q",
                "CubicBezier" => "B",
                "Arc" => "A",
                "Close" => "C",
                _ => (string?)null
            };
            if (opt is null) continue;
            data._dataQueue.Add(new OptVal(opt, cmd.OperandValues));
        }
        return data.Flush();
    }
}
