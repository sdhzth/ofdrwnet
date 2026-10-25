using System;
using NetOfd.Core.BasicType;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 画布状态
/// </summary>
public class CanvasState
{
    /// <summary>
    /// 上下文路径数据
    /// </summary>
    public PathBuilder? Path;

    /// <summary>
    /// 变换矩阵
    /// </summary>
    public double[]? Ctm;

    /// <summary>
    /// 字体设置
    /// </summary>
    public FontSetting? FontSetting;

    /// <summary>
    /// 透明度，0.0-1.0
    /// </summary>
    public double? GlobalAlpha;

    /// <summary>
    /// 裁剪区域
    /// </summary>
    public PathBuilder? ClipArea;

    /// <summary>
    /// 虚线模式（mm）
    /// </summary>
    public double[]? LineDash;

    /// <summary>
    /// 虚线偏移量（mm）
    /// </summary>
    public double LineDashOffset;

    /// <summary>
    /// 填充颜色
    /// </summary>
    public object? FillStyle = "#000000";

    /// <summary>
    /// 描边颜色
    /// </summary>
    public object? StrokeStyle = "#000000";

    public CanvasState()
    {
        FontSetting = new FontSetting(1, "SimSun");
    }

    public CanvasState Clone()
    {
        var clone = new CanvasState
        {
            Path = Path?.Clone(),
            Ctm = Ctm != null ? (double[])Ctm.Clone() : null,
            FontSetting = FontSetting != null
                ? new FontSetting(FontSetting.Size, FontSetting.Family)
                    { Bold = FontSetting.Bold, Italic = FontSetting.Italic }
                : null,
            GlobalAlpha = GlobalAlpha,
            ClipArea = ClipArea?.Clone(),
            FillStyle = FillStyle,
            StrokeStyle = StrokeStyle,
            LineDash = LineDash != null ? (double[])LineDash.Clone() : null,
            LineDashOffset = LineDashOffset
        };
        return clone;
    }
}

/// <summary>
/// 字体设置
/// </summary>
public class FontSetting
{
    /// <summary>
    /// 字号
    /// </summary>
    public double Size;

    /// <summary>
    /// 字体名称
    /// </summary>
    public string Family;

    /// <summary>
    /// 是否粗体
    /// </summary>
    public bool Bold;

    /// <summary>
    /// 是否斜体
    /// </summary>
    public bool Italic;

    public FontSetting(double size, string family)
    {
        Size = size;
        Family = family;
    }
}

/// <summary>
/// 路径构造器
/// </summary>
public class PathBuilder
{
    /// <summary>
    /// 操作码和参数列表
    /// </summary>
    private List<PathOp> _ops = new();

    public PathOp LastOp => _ops.Count > 0 ? _ops[^1] : default;

    public void MoveTo(double x, double y)
    {
        _ops.Add(new PathOp(OpType.M, x, y));
    }

    public void LineTo(double x, double y)
    {
        _ops.Add(new PathOp(OpType.L, x, y));
    }

    public void QuadraticBezier(double cpx, double cpy, double x, double y)
    {
        _ops.Add(new PathOp(OpType.Q, cpx, cpy, x, y));
    }

    public void CubicBezier(double cp1x, double cp1y, double cp2x, double cp2y, double x, double y)
    {
        _ops.Add(new PathOp(OpType.C, cp1x, cp1y, cp2x, cp2y, x, y));
    }

    public void Arc(double rx, double ry, double angle, bool largeArc, bool sweep, double x, double y)
    {
        _ops.Add(new PathOp(OpType.A, rx, ry, angle, largeArc ? 1 : 0, sweep ? 1 : 0, x, y));
    }

    public void Close()
    {
        _ops.Add(new PathOp(OpType.Z));
    }

    public IReadOnlyList<PathOp> Operations => _ops;

    /// <summary>
    /// 用 6 元素仿射变换矩阵变换所有路径操作，返回变换后的新路径。
    /// </summary>
    public PathBuilder Transformed(double[] ctm)
    {
        var result = new PathBuilder();
        foreach (var op in _ops)
        {
            double[] args = op.Args;
            switch (op.Type)
            {
                case OpType.M:
                case OpType.L:
                    result._ops.Add(new PathOp(op.Type,
                        TransformPoint(ctm, args[0], args[1]).X,
                        TransformPoint(ctm, args[0], args[1]).Y));
                    break;
                case OpType.Q:
                    var q1 = TransformPoint(ctm, args[0], args[1]);
                    var q2 = TransformPoint(ctm, args[2], args[3]);
                    result._ops.Add(new PathOp(op.Type, q1.X, q1.Y, q2.X, q2.Y));
                    break;
                case OpType.C:
                    var c1 = TransformPoint(ctm, args[0], args[1]);
                    var c2 = TransformPoint(ctm, args[2], args[3]);
                    var c3 = TransformPoint(ctm, args[4], args[5]);
                    result._ops.Add(new PathOp(op.Type, c1.X, c1.Y, c2.X, c2.Y, c3.X, c3.Y));
                    break;
                case OpType.A:
                    // 圆弧：变换半径与端点（近似，忽略旋转分量）
                    var a1 = TransformPoint(ctm, args[5], args[6]);
                    double scale = Math.Max(Math.Abs(ctm[0]), Math.Abs(ctm[3]));
                    result._ops.Add(new PathOp(op.Type,
                        args[0] * scale, args[1] * scale, args[2], args[3], args[4],
                        a1.X, a1.Y));
                    break;
                case OpType.Z:
                    result._ops.Add(new PathOp(OpType.Z));
                    break;
            }
        }
        return result;
    }

    private static (double X, double Y) TransformPoint(double[] ctm, double x, double y)
        => (ctm[0] * x + ctm[2] * y + ctm[4],
            ctm[1] * x + ctm[3] * y + ctm[5]);

    public PathBuilder Clone() => _ops.Count == 0
        ? new PathBuilder()
        : new PathBuilder { _ops = new List<PathOp>(_ops) };
}

public readonly struct PathOp
{
    public OpType Type { get; }
    public double[] Args { get; }

    public PathOp(OpType type, params double[] args)
    {
        Type = type;
        Args = args;
    }

    public string ToAbbreviatedData()
    {
        return Type switch
        {
            OpType.M => $"M {STBase.Fmt(Args[0])} {STBase.Fmt(Args[1])}",
            OpType.L => $"L {STBase.Fmt(Args[0])} {STBase.Fmt(Args[1])}",
            OpType.Q => $"Q {STBase.Fmt(Args[0])} {STBase.Fmt(Args[1])} {STBase.Fmt(Args[2])} {STBase.Fmt(Args[3])}",
            // GB/T 33190：三次贝塞尔 = B（6 参数）；闭合 = C（0 参数）
            OpType.C => $"B {STBase.Fmt(Args[0])} {STBase.Fmt(Args[1])} {STBase.Fmt(Args[2])} {STBase.Fmt(Args[3])} {STBase.Fmt(Args[4])} {STBase.Fmt(Args[5])}",
            OpType.A => $"A {STBase.Fmt(Args[0])} {STBase.Fmt(Args[1])} {STBase.Fmt(Args[2])} {Args[3]:F0} {Args[4]:F0} {STBase.Fmt(Args[5])} {STBase.Fmt(Args[6])}",
            OpType.Z => "C",
            _ => ""
        };
    }
}

public enum OpType
{
    M, L, Q, C, A, Z
}
