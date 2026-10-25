using System.Globalization;

namespace NetOfd.Core.BasicType;

/// <summary>
/// OFD 基础简单类型的抽象基类，提供数值格式化工具方法。
/// 对应 Java: org.ofdrw.core.basicType.STBase
/// </summary>
public abstract class STBase
{
    private const double Epsilon = 1e-9;

    /// <summary>
    /// 将 double 格式化为 OFD 要求的字符串：整数无小数点，最多3位小数，去掉末尾零。
    /// </summary>
    public static string Fmt(double d)
    {
        if (Math.Abs(d) < Epsilon) return "0";

        // 尝试以整数输出
        if (Math.Abs(d - Math.Round(d)) < Epsilon)
            return ((long)Math.Round(d)).ToString(CultureInfo.InvariantCulture);

        // 最多 3 位小数，去掉末尾零
        string s = d.ToString("0.###", CultureInfo.InvariantCulture);
        return s;
    }

    /// <summary>
    /// 安全地将字符串解析为 double
    /// </summary>
    public static double ToDouble(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0.0;
        s = s.Trim();
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double v))
            return v;
        return 0.0;
    }

    /// <summary>
    /// 安全地将字符串解析为 int
    /// </summary>
    public static int ToInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0;
        s = s.Trim();
        if (int.TryParse(s, out int v)) return v;
        // 支持浮点字符串转 int
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return (int)d;
        return 0;
    }

    /// <summary>
    /// 安全地将字符串解析为 long
    /// </summary>
    public static long ToLong(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return 0L;
        s = s.Trim();
        if (long.TryParse(s, out long v)) return v;
        if (double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            return (long)d;
        return 0L;
    }

    /// <inheritdoc/>
    public abstract override string ToString();
}
