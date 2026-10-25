using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Reader;
using SkiaSharp;
using System.Globalization;

namespace NetOfd.Converter;

/// <summary>
/// OFD 颜色（CT_Color）到 SkiaSharp 颜色的转换工具。
/// 对应 Java: org.ofdrw.converter.ColorConvert
/// </summary>
public static class OfdColorUtil
{
    /// <summary>
    /// 将 CT_Color 转换为 SKColor。
    /// <paramref name="fallback"/> 在 color 为 null 或无法解析时使用。
    /// <para>当颜色值为空但挂接渐变底纹时，取首个颜色段近似（对齐 Java PdfboxMaker 行为）。</para>
    /// </summary>
    public static SKColor ToSKColor(CT_Color? color, SKColor fallback)
    {
        if (color is null) return fallback;
        var value = color.Value;
        if (string.IsNullOrWhiteSpace(value))
        {
            var seg = FirstSegmentColor(color);
            if (seg is not null) return ToSKColor(seg, fallback);
            return fallback;
        }

        value = value.Trim();
        if (value.StartsWith("#"))
        {
            try { return SKColor.Parse(value); }
            catch { return fallback; }
        }

        // 兼容空格或逗号分隔（本移植版写入器使用逗号分隔 "r,g,b"）
        var parts = value
            .Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(STBase.ToDouble)
            .ToArray();

        byte alpha = color.Alpha.HasValue ? Clamp(color.Alpha.Value) : (byte)255;

        switch (parts.Length)
        {
            case 1: // 灰度
                {
                    byte g = Clamp(parts[0]);
                    return new SKColor(g, g, g, alpha);
                }
            case 3: // RGB
                return new SKColor(Clamp(parts[0]), Clamp(parts[1]), Clamp(parts[2]), alpha);
            case >= 4: // CMYK（0-255）
                {
                    double c = parts[0] / 255.0, m = parts[1] / 255.0,
                           y = parts[2] / 255.0, k = parts[3] / 255.0;
                    byte r = Clamp(255 * (1 - c) * (1 - k));
                    byte g = Clamp(255 * (1 - m) * (1 - k));
                    byte b = Clamp(255 * (1 - y) * (1 - k));
                    return new SKColor(r, g, b, alpha);
                }
            default:
                return fallback;
        }
    }

    /// <summary>
    /// 将 CT_Color 转换为 SKColor，优先按颜色空间（RGB/GRAY/CMYK）与调色板索引精确解析。
    /// 当颜色引用了 ColorSpace 或含 Index 时走 <see cref="ColorConvert"/>；
    /// 否则回退到 <see cref="ToSKColor(CT_Color?, SKColor)"/> 的行内解析（#hex / 十进制 1/3/4 通道）。
    /// </summary>
    /// <param name="color">颜色定义</param>
    /// <param name="fallback">解析失败时的兜底色</param>
    /// <param name="resMgt">资源管理器（解析 ColorSpace 引用 / 调色板；可为 null）</param>
    public static SKColor ToSKColor(CT_Color? color, SKColor fallback, ResourceManage? resMgt)
    {
        if (color is null) return fallback;
        if (color.ColorSpace is not null || color.Index is not null)
        {
            var rgb = ColorConvert.Rgb(resMgt, color);
            if (rgb is not null)
            {
                byte alpha = color.Alpha.HasValue ? Clamp(color.Alpha.Value) : (byte)255;
                return new SKColor((byte)rgb[0], (byte)rgb[1], (byte)rgb[2], alpha);
            }
        }
        return ToSKColor(color, fallback);
    }

    /// <summary>尝试解析颜色，失败返回 null（用于 Fill 等“无填充”语义）。</summary>
    public static SKColor? ToSKColorOrNull(CT_Color? color)
    {
        if (color is null) return null;
        var value = color.Value;
        if (string.IsNullOrWhiteSpace(value)) return null;
        // 若解析后等于兜底值且本就为 null，则视为无颜色
        var c = ToSKColor(color, SKColors.Transparent);
        return c == SKColors.Transparent ? null : c;
    }

    /// <summary>
    /// 尝试解析颜色（颜色空间感知版），失败返回 null。
    /// 详见 <see cref="ToSKColor(CT_Color?, SKColor, ResourceManage?)"/>。
    /// </summary>
    public static SKColor? ToSKColorOrNull(CT_Color? color, ResourceManage? resMgt)
    {
        if (color is null) return null;
        var value = color.Value;
        if (string.IsNullOrWhiteSpace(value)) return null;
        var c = ToSKColor(color, SKColors.Transparent, resMgt);
        return c == SKColors.Transparent ? null : c;
    }

    private static byte Clamp(double v) =>
        (byte)Math.Max(0, Math.Min(255, (int)Math.Round(v)));

    // ── 渐变底纹着色器（AxialShd / RadialShd）────────────────────

    /// <summary>
    /// 若 <paramref name="color"/> 挂接渐变底纹（AxialShd / RadialShd），创建对应的
    /// SkiaSharp 渐变着色器；否则返回 null（调用方回退纯色）。
    /// </summary>
    /// <param name="boundary">图元 Boundary（可为 null）。着色器坐标按 OFD 规范属于图元局部坐标系，
    /// 渲染矩阵为 <c>Scale(PPM)·Translate(Boundary)·CTM</c>，故此处需将绝对坐标减去 Boundary 偏移，
    /// 使其与相对化后的 AbbreviatedData 路径处于同一局部坐标系（否则渐变会被 Boundary 重复平移）。</param>
    public static SKShader? TryCreateShader(CT_Color? color, ResourceManage? resMgt, STBox? boundary = null)
    {
        if (color is null) return null;

        var axial = color.GetAxialShd();
        if (axial is not null)
            return CreateAxialShader(axial, resMgt, boundary);

        var radial = color.GetRadialShd();
        if (radial is not null)
            return CreateRadialShader(radial, resMgt, boundary);

        return null;
    }

    /// <summary>轴向渐变（线性）着色器。颜色沿 StartPoint→EndPoint 轴线渐变。</summary>
    private static SKShader? CreateAxialShader(CT_AxialShd shd, ResourceManage? resMgt, STBox? boundary = null)
    {
        var segments = shd.GetSegments();
        if (segments.Count == 0) return null;
        var start = shd.StartPoint;
        var end = shd.EndPoint;
        if (start is null || end is null) return null;
        if (Math.Abs(end.X - start.X) < 1e-9 && Math.Abs(end.Y - start.Y) < 1e-9) return null; // 零长度轴线

        var (colors, positions) = BuildColorStops(segments, resMgt);
        if (colors.Length == 0) return null;

        double ox = boundary?.TopLeftX ?? 0d, oy = boundary?.TopLeftY ?? 0d;
        var tileMode = MapTileMode(shd.MapType);
        return SKShader.CreateLinearGradient(
            new SKPoint((float)(start.X - ox), (float)(start.Y - oy)),
            new SKPoint((float)(end.X - ox), (float)(end.Y - oy)),
            colors, positions, tileMode);
    }

    /// <summary>
    /// 径向渐变着色器。
    /// <para>OFD 的 RadialShd 从起始椭圆（圆心 StartPoint，半径 StartRadius）渐变到结束椭圆
    /// （圆心 EndPoint，半径 EndRadius），且圆心可能不同 —— Skia 用双圆圆锥渐变
    /// （TwoPointConical）表达最贴切。椭圆离心率 Eccentricity 非 0 或圆心重合时退化为
    /// 单圆心径向渐变近似（Java ofdrw 同样仅近似处理）。</para>
    /// </summary>
    private static SKShader? CreateRadialShader(CT_RadialShd shd, ResourceManage? resMgt, STBox? boundary = null)
    {
        var segments = shd.GetSegments();
        if (segments.Count == 0) return null;
        var start = shd.StartPoint;
        var end = shd.EndPoint;
        if (start is null || end is null) return null;

        var (colors, positions) = BuildColorStops(segments, resMgt);
        if (colors.Length == 0) return null;

        var tileMode = MapTileMode(shd.MapType);
        float sr = (float)shd.StartRadius;
        float er = (float)shd.EndRadius;

        // 两端半径相同且圆心相同 → 退化为无渐变，返回 null 让调用方回退
        bool sameCenter = Math.Abs(end.X - start.X) < 1e-9 && Math.Abs(end.Y - start.Y) < 1e-9;
        if (sameCenter && Math.Abs(er - sr) < 1e-9 && colors.Length > 1)
            return null;

        // 着色器坐标减 Boundary 偏移，与相对化路径处于同一局部坐标系（详见 TryCreateShader）
        double ox = boundary?.TopLeftX ?? 0d, oy = boundary?.TopLeftY ?? 0d;
        var sPt = new SKPoint((float)(start.X - ox), (float)(start.Y - oy));
        var ePt = new SKPoint((float)(end.X - ox), (float)(end.Y - oy));

        try
        {
            // 双圆心两圆圆锥渐变：精确表达 OFD 的“起始圆→结束圆”径向渐变语义
            return SKShader.CreateTwoPointConicalGradient(sPt, sr, ePt, er, colors, positions, tileMode);
        }
        catch
        {
            // 参数不合法（如 start 圆内含 end 圆等极端情形）时退化为单圆心径向渐变
            return SKShader.CreateRadialGradient(ePt, Math.Max(er, sr), colors, positions, tileMode);
        }
    }

    /// <summary>把段列表转换为 SkiaSharp 需要的颜色数组与位置数组（0..1）。</summary>
    private static (SKColor[] colors, float[] positions) BuildColorStops(List<Segment> segments, ResourceManage? resMgt)
    {
        if (segments.Count == 0) return (Array.Empty<SKColor>(), Array.Empty<float>());
        var colors = new SKColor[segments.Count];
        var pos = new float[segments.Count];
        for (int i = 0; i < segments.Count; i++)
        {
            var c = segments[i].Color;
            colors[i] = ToSKColor(c, SKColors.Black, resMgt);
            double? p = segments[i].Position;
            pos[i] = p.HasValue
                ? (float)Math.Max(0, Math.Min(1, p.Value))
                : (float)i / Math.Max(1, segments.Count - 1); // 缺省按等距
        }
        return (colors, pos);
    }

    /// <summary>MapType → SkiaSharp 平铺模式（Direct → Clamp 延展两端色）。</summary>
    private static SKShaderTileMode MapTileMode(MapType mapType) => mapType switch
    {
        MapType.Repeat => SKShaderTileMode.Repeat,
        MapType.Reflect => SKShaderTileMode.Mirror,
        _ => SKShaderTileMode.Clamp,
    };

    /// <summary>取渐变首个颜色段（对齐 Java PdfboxMaker 仅用首段颜色的近似策略）。</summary>
    private static CT_Color? FirstSegmentColor(CT_Color color)
    {
        var cluster = color.GetColorCluster();
        if (cluster is not CT_Shd shd) return null;
        var segs = shd.GetSegments();
        if (segs.Count == 0) return null;
        var segColor = segs[0].Color;
        // 防递归：段颜色自身又为空且带渐变时不再下钻，视为无色
        return segColor is not null && !string.IsNullOrWhiteSpace(segColor.Value) ? segColor : null;
    }
}
