namespace NetOfd.Core.PageDescription.Text;

/// <summary>
/// 文字对象的粗细值（GB/T 33190-2016 §11.3 表 45）。
/// <para>可选值为 100、200、300、400、500、600、700、800、900，默认 400。</para>
/// 对应 Java: org.ofdrw.core.text.text.Weight
/// </summary>
public enum Weight
{
    /// <summary>100</summary>
    W100 = 100,

    /// <summary>200</summary>
    W200 = 200,

    /// <summary>300</summary>
    W300 = 300,

    /// <summary>400（默认值）</summary>
    W400 = 400,

    /// <summary>500</summary>
    W500 = 500,

    /// <summary>600</summary>
    W600 = 600,

    /// <summary>700</summary>
    W700 = 700,

    /// <summary>800</summary>
    W800 = 800,

    /// <summary>900</summary>
    W900 = 900
}

/// <summary>
/// <see cref="Weight"/> 的解析/格式化辅助（与 XML 属性字符串互转）。
/// </summary>
public static class WeightUtil
{
    /// <summary>解析 XML 属性值；空值视为 400。非法粗细值抛出 <see cref="FormatException"/>。</summary>
    public static Weight Parse(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0) s = "400";
        return s switch
        {
            "100" => Weight.W100,
            "200" => Weight.W200,
            "300" => Weight.W300,
            "400" => Weight.W400,
            "500" => Weight.W500,
            "600" => Weight.W600,
            "700" => Weight.W700,
            "800" => Weight.W800,
            "900" => Weight.W900,
            _ => throw new FormatException("错误的文字对象的粗细值：" + value)
        };
    }

    /// <summary>按粗细数值解析；非法值抛出 <see cref="FormatException"/>。</summary>
    public static Weight Parse(int weight) => Parse(weight.ToString());

    /// <summary>粗细值数值。</summary>
    public static int Value(Weight weight) => (int)weight;

    /// <summary>转为 XML 属性值（粗细数字，如 <c>400</c>）。</summary>
    public static string ToXml(Weight weight) => ((int)weight).ToString();
}
