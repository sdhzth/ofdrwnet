using NetOfd.Core.PageDescription.Color;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 渐变颜色段构造辅助：把 CSS 颜色字符串转换为 OFD <see cref="CT_Color"/>。
/// </summary>
internal static class GradientColorUtil
{
    /// <summary>
    /// 解析颜色字符串为 CT_Color（含 Alpha 通道时一并设置）。
    /// 无法解析时返回 null（与 Java NamedColor 行为一致）。
    /// </summary>
    public static CT_Color? ToCtColor(string? color)
    {
        var rgb = NamedColor.Rgb(color);
        if (rgb is null || rgb.Length < 3) return null;

        var c = CT_Color.FromRgb(rgb[0], rgb[1], rgb[2]);
        if (rgb.Length > 3) c.Alpha = rgb[3];
        return c;
    }
}
