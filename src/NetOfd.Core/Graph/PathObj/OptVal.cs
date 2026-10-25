using NetOfd.Core.BasicType;
using NetOfd.Core.Graph.Tight.Method;

namespace NetOfd.Core.Graph.PathObj;

/// <summary>
/// 紧缩路径数据中的「操作符 + 操作数」单元。
/// 操作符取值：S（起始点）/ M（移动）/ L（直线）/ Q（二次贝塞尔）/
/// B（三次贝塞尔）/ A（圆弧）/ C（闭合）。
/// 对应 Java: org.ofdrw.core.graph.pathObj.OptVal
/// </summary>
public sealed class OptVal
{
    /// <summary>操作符（S/M/L/Q/B/A/C）</summary>
    public string Opt { get; set; }

    /// <summary>操作数序列</summary>
    public double[] Values { get; set; }

    /// <summary>仅含操作符（如 C 闭合指令），操作数为空。</summary>
    public OptVal(string opt)
    {
        Opt = opt;
        Values = Array.Empty<double>();
    }

    /// <summary>以数值数组构造。</summary>
    public OptVal(string opt, double[]? values)
    {
        Opt = opt;
        Values = values ?? Array.Empty<double>();
    }

    /// <summary>
    /// 以字符串序列构造；无法解析的数字按 0 处理（与 Java 一致）。
    /// </summary>
    public OptVal(string opt, IEnumerable<string>? valueStrList)
    {
        Opt = opt;
        if (valueStrList is null)
        {
            Values = Array.Empty<double>();
            return;
        }

        var list = valueStrList.ToList();
        var arr = new double[list.Count];
        for (int i = 0; i < list.Count; i++)
        {
            arr[i] = double.TryParse(list[i], out double d) ? d : 0;
        }
        Values = arr;
    }

    /// <summary>
    /// 在数组长度不足时补 0 到期待长度 <paramref name="num"/>。
    /// 对应 Java: OptVal#filling
    /// </summary>
    public static double[] Filling(double[]? arr, int num)
    {
        if (arr is null) return new double[num];
        if (arr.Length < num)
        {
            var copy = new double[num];
            Array.Copy(arr, copy, arr.Length);
            return copy;
        }
        return arr;
    }

    /// <summary>
    /// 转换为非紧缩（XML 结构）绘制指令；无法识别的操作符返回 null。
    /// 对应 Java: OptVal#toCmd
    /// </summary>
    public Command? ToCmd()
    {
        switch (Opt)
        {
            case "S":
            case "M":
                {
                    var arr = Filling(Values, 2);
                    return new Move(arr[0], arr[1]);
                }
            case "L":
                {
                    var arr = Filling(Values, 2);
                    return new Line(arr[0], arr[1]);
                }
            case "Q":
                {
                    var arr = Filling(Values, 4);
                    return new QuadraticBezier(arr[0], arr[1], arr[2], arr[3]);
                }
            case "B":
                {
                    var arr = Filling(Values, 6);
                    return new CubicBezier(arr[0], arr[1], arr[2], arr[3], arr[4], arr[5]);
                }
            case "A":
                {
                    var arr = Filling(Values, 7);
                    return new Arc(arr[0], arr[1], arr[2], arr[3], arr[4], arr[5], arr[6]);
                }
            case "C":
                return new Close();
            default:
                return null;
        }
    }

    /// <summary>
    /// 按操作符期待的操作数个数补齐（不足补 0）。
    /// 对应 Java: OptVal#expectValues
    /// </summary>
    public double[] ExpectValues() => Opt switch
    {
        "S" or "M" or "L" => Filling(Values, 2),
        "Q" => Filling(Values, 4),
        "B" => Filling(Values, 6),
        "A" => Filling(Values, 7),
        _ => Array.Empty<double>()
    };

    /// <summary>拷贝本单元（操作数数组深拷贝）。</summary>
    public OptVal Clone() => new(Opt, (double[])Values.Clone());

    /// <summary>
    /// 序列化为紧缩片段（如 <c>M 10 10</c>）；无法识别的操作符返回空串。
    /// 对应 Java: OptVal#toString
    /// </summary>
    public override string ToString()
    {
        switch (Opt)
        {
            case "S":
            case "M":
            case "L":
                {
                    var a = Filling(Values, 2);
                    return $"{Opt} {STBase.Fmt(a[0])} {STBase.Fmt(a[1])}";
                }
            case "Q":
                {
                    var a = Filling(Values, 4);
                    return $"Q {STBase.Fmt(a[0])} {STBase.Fmt(a[1])} {STBase.Fmt(a[2])} {STBase.Fmt(a[3])}";
                }
            case "B":
                {
                    var a = Filling(Values, 6);
                    return $"B {STBase.Fmt(a[0])} {STBase.Fmt(a[1])} {STBase.Fmt(a[2])} " +
                           $"{STBase.Fmt(a[3])} {STBase.Fmt(a[4])} {STBase.Fmt(a[5])}";
                }
            case "A":
                {
                    var a = Filling(Values, 7);
                    return $"A {STBase.Fmt(a[0])} {STBase.Fmt(a[1])} {STBase.Fmt(a[2])} " +
                           $"{STBase.Fmt(a[3])} {STBase.Fmt(a[4])} {STBase.Fmt(a[5])} {STBase.Fmt(a[6])}";
                }
            case "C":
                return "C";
            default:
                return "";
        }
    }
}
