using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Core.PageDescription.Color.colorSpace;
using NetOfd.Reader;
using System.Globalization;

namespace NetOfd.Converter;

/// <summary>
/// 颜色转换（颜色空间感知）。
/// 对应 Java: org.ofdrw.converter.ColorConvert
/// <para>
/// 与 Java 的差异：OFD 规范（GB/T 33190-2016 §7.4）规定颜色值取值范围为 0~255，
/// 因此 <see cref="CmykToRgb"/> 按 0-255 计算（Java 版按 0-100，对真实 OFD 的 0-255 值会产生偏差，
/// 此处以规范为准）。
/// </para>
/// </summary>
public static class ColorConvert
{
    /// <summary>
    /// 将 <see cref="CT_Color"/> 解析为 RGB 分量（0-255）。
    /// 处理颜色空间引用（RGB/GRAY/CMYK）与调色板索引；未引用颜色空间时按默认 RGB 解释行内值。
    /// </summary>
    /// <param name="resMgt">资源管理器（用于解析 ColorSpace 引用与调色板）；可为 null</param>
    /// <param name="ctColor">颜色定义</param>
    /// <returns>长度为 3 的 RGB 数组；无法解析时返回 null</returns>
    public static int[]? Rgb(ResourceManage? resMgt, CT_Color ctColor)
    {
        if (ctColor is null) return null;
        var value = ctColor.Value;
        int? index = ctColor.Index;
        var csId = ctColor.ColorSpace;

        if (string.IsNullOrWhiteSpace(value) && index is null)
            return new[] { 0, 0, 0 };

        CT_ColorSpace? cs = null;
        if (csId is not null && resMgt is not null)
            cs = resMgt.GetColorSpace(csId);

        if (cs is null)
        {
            // 未引用（或无法解析）颜色空间时，按分量数推断：1=灰度，3=RGB，4=CMYK。
            // 有 Index 但无调色板可查 → 无法解析。
            if (index is not null) return null;
            var inline = ParseComponents(value);
            return inline is null ? new[] { 0, 0, 0 } : InterpretByChannelCount(inline);
        }

        // 已解析到颜色空间：按该空间类型解析（支持调色板索引）。
        double[]? comps = null;
        if (index is not null)
        {
            var cvText = cs.Palette?.GetColorTextByIndex(index.Value);
            comps = ParseComponents(cvText);
        }
        else if (!string.IsNullOrWhiteSpace(value))
        {
            comps = ParseComponents(value);
        }
        if (comps is null) return null;

        switch (cs.Type)
        {
            case OFDColorSpaceType.RGB:
                return comps.Length >= 3
                    ? new[] { Clamp(comps[0]), Clamp(comps[1]), Clamp(comps[2]) }
                    : new[] { 0, 0, 0 };
            case OFDColorSpaceType.GRAY:
                {
                    var g = Clamp(comps.Length >= 1 ? comps[0] : 0);
                    return new[] { g, g, g };
                }
            case OFDColorSpaceType.CMYK:
                return comps.Length >= 4
                    ? CmykToRgb((int)Clamp(comps[0]), (int)Clamp(comps[1]), (int)Clamp(comps[2]), (int)Clamp(comps[3]))
                    : new[] { 0, 0, 0 };
            default:
                return new[] { 0, 0, 0 };
        }
    }

    /// <summary>
    /// 未引用颜色空间时按分量数解释行内值：1=灰度，3=RGB，4=CMYK。
    /// </summary>
    private static int[] InterpretByChannelCount(double[] c)
    {
        return c.Length switch
        {
            1 => Gray(c[0]),
            3 => new[] { Clamp(c[0]), Clamp(c[1]), Clamp(c[2]) },
            4 => CmykToRgb((int)Clamp(c[0]), (int)Clamp(c[1]), (int)Clamp(c[2]), (int)Clamp(c[3])),
            _ => new[] { 0, 0, 0 }
        };
    }

    private static int[] Gray(double g)
    {
        var v = Clamp(g);
        return new[] { v, v, v };
    }

    /// <summary>
    /// CMYK → RGB（各分量 0-255，符合 OFD 规范）。
    /// </summary>
    public static int[] CmykToRgb(int c, int m, int y, int k)
    {
        c = Math.Clamp(c, 0, 255);
        m = Math.Clamp(m, 0, 255);
        y = Math.Clamp(y, 0, 255);
        k = Math.Clamp(k, 0, 255);

        int r = (int)(255 * (1 - c / 255.0) * (1 - k / 255.0));
        int g = (int)(255 * (1 - m / 255.0) * (1 - k / 255.0));
        int b = (int)(255 * (1 - y / 255.0) * (1 - k / 255.0));
        return new[] { r, g, b };
    }

    // ── 分量文本解析（支持 #hex / 十进制 / 浮点）──────────────
    private static double[]? ParseComponents(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var toks = text.Split(new[] { ' ', '\t', '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        if (toks.Length == 0) return null;

        // 单个 #hex 整体：#RRGGBB 或 #RGB
        if (toks.Length == 1 && toks[0].StartsWith("#"))
        {
            var hex = toks[0].Substring(1);
            if (hex.Length == 6)
                return new[] { (double)HexByte(hex, 0), HexByte(hex, 2), HexByte(hex, 4) };
            if (hex.Length == 3)
                return new[] { (double)HexNib(hex[0]), HexNib(hex[1]), HexNib(hex[2]) };
            return new[] { (double)HexAny(hex) };
        }

        var outv = new double[toks.Length];
        for (int i = 0; i < toks.Length; i++)
        {
            var t = toks[i];
            if (t.StartsWith("#"))
                outv[i] = HexAny(t.Substring(1));
            else
                outv[i] = STBase.ToDouble(t);
        }
        return outv;
    }

    private static int HexByte(string hex, int i) =>
        int.Parse(hex.Substring(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int HexNib(char c) =>
        int.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture) * 16
        + int.Parse(c.ToString(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int HexAny(string hex) =>
        hex.Length == 0 ? 0 : int.Parse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture);

    private static int Clamp(double v) =>
        (int)Math.Max(0, Math.Min(255, Math.Round(v)));
}
