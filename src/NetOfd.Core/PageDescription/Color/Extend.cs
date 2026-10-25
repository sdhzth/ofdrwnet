using System.Globalization;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 渐变轴线延长线方向是否继续绘制（GB/T 33190 §8.3.4.2 表 29）。
/// <list type="bullet">
/// <item>0 - 不向两侧继续绘制渐变（默认值）</item>
/// <item>1 - 起始点方向继续绘制渐变</item>
/// <item>2 - 结束点方向继续绘制渐变</item>
/// <item>3 - 两侧方向均继续绘制渐变</item>
/// </list>
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.Extend
/// </summary>
public enum Extend
{
    /// <summary>0：不向两侧继续绘制渐变（默认）</summary>
    None = 0,

    /// <summary>1：起始点方向继续绘制渐变</summary>
    Start = 1,

    /// <summary>2：结束点方向继续绘制渐变</summary>
    End = 2,

    /// <summary>3：两侧方向均继续绘制渐变</summary>
    Both = 3
}

/// <summary>
/// <see cref="Extend"/> 的解析/格式化辅助（与 XML 属性整数值互转）。
/// </summary>
public static class ExtendUtil
{
    /// <summary>解析 XML 属性值，非法或空值视为 <see cref="Extend.None"/>（0）。</summary>
    public static Extend Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Extend.None;
        return int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
               && System.Enum.IsDefined(typeof(Extend), n)
            ? (Extend)n
            : Extend.None;
    }

    /// <summary>转为 XML 属性值（0-3 整数字符串）。</summary>
    public static string ToXml(Extend extend) => ((int)extend).ToString(CultureInfo.InvariantCulture);
}
