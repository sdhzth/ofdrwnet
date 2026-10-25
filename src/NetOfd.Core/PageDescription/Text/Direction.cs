namespace NetOfd.Core.PageDescription.Text;

/// <summary>
/// 方向角度（GB/T 33190-2016 §11.3 文字定位 表 47）。
/// <para>
/// 用于 <c>ReadDirection</c>（阅读方向）与 <c>CharDirection</c>（字符方向）属性，
/// 可选旋转角度为 0、90、180、270。
/// </para>
/// 对应 Java: org.ofdrw.core.text.text.Direction
/// </summary>
public enum Direction
{
    /// <summary>旋转 0 度（默认，横排）。</summary>
    Angle0 = 0,

    /// <summary>旋转 90 度（竖排，自上而下）。</summary>
    Angle90 = 90,

    /// <summary>旋转 180 度。</summary>
    Angle180 = 180,

    /// <summary>旋转 270 度（竖排，自下而上）。</summary>
    Angle270 = 270
}

/// <summary>
/// <see cref="Direction"/> 的解析/格式化辅助（与 XML 属性字符串互转）。
/// </summary>
public static class DirectionUtil
{
    /// <summary>解析 XML 属性值；空值视为 0 度。非法角度抛出 <see cref="FormatException"/>。</summary>
    public static Direction Parse(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0) s = "0";
        return s switch
        {
            "0" => Direction.Angle0,
            "90" => Direction.Angle90,
            "180" => Direction.Angle180,
            "270" => Direction.Angle270,
            _ => throw new FormatException("未知旋转角度：" + value)
        };
    }

    /// <summary>按角度数值解析；非法角度抛出 <see cref="FormatException"/>。</summary>
    public static Direction Parse(int angle) => Parse(angle.ToString());

    /// <summary>角度数值。</summary>
    public static int Angle(Direction direction) => (int)direction;

    /// <summary>转为 XML 属性值（角度数字，如 <c>90</c>）。</summary>
    public static string ToXml(Direction direction) => ((int)direction).ToString();
}
