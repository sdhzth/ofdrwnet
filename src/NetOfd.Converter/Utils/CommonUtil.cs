using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using SkiaSharp;
using System.Globalization;

namespace NetOfd.Converter.Utils;

/// <summary>
/// 转换工具集合：单位换算、颜色、变换矩阵、临时文件。
/// <para>
/// 对应 Java <c>org.ofdrw.converter.utils.CommonUtil</c>，但<b>只移植与底层渲染库无关的部分</b>：
/// Java 原类中 <c>convertPDColor</c>（PDFBox <c>PDColor</c>）、<c>converJbig2</c>、
/// <c>getImageMatrixFromOfd</c>（ujmp <c>Matrix</c>）、<c>toPFMatrix</c> 均为 PDFBox / iText /
/// ujmp 专属，netofd 走 SkiaSharp 渲染路径，不适用；<c>convertOfdColor</c> 已被 Java 自身标注
/// <c>@Deprecated</c>；<c>convertV1SignTime</c> 的能力已由
/// <c>NetOfd.Sign.OfdValidator.TryParseTimeText</c> 覆盖（且更完整，支持 <c>+0800</c> 偏移），
/// 均不移植。
/// </para>
/// </summary>
public static class CommonUtil
{
    /// <summary>PDF 与打印的基准分辨率（每英寸点数）。</summary>
    public const double DefaultDpi = 72.0;

    /// <summary>一英寸等于的毫米数。</summary>
    public const double MmPerInch = 25.4;

    // ── 单位换算 ────────────────────────────────────────────

    /// <summary>毫米转像素：<c>mm × dpi / 25.4</c>。</summary>
    /// <param name="mm">毫米长度</param>
    /// <param name="dpi">分辨率，如 72 / 200 / 300</param>
    public static double MillimetersToPixel(double mm, double dpi) => mm * dpi / MmPerInch;

    /// <summary>像素转毫米：<c>px × 25.4 / dpi</c>。</summary>
    /// <param name="px">像素长度</param>
    /// <param name="dpi">分辨率</param>
    public static double PixelToMillimeters(double px, double dpi) => px * MmPerInch / dpi;

    /// <summary>指定 DPI 下每毫米对应的像素数（等价于 <see cref="MillimetersToPixel"/>(1, dpi)）。</summary>
    /// <param name="dpi">每英寸像素数，如 200、300</param>
    public static double DpiToPpm(int dpi) => (0.01 / 0.254) * dpi;

    /// <summary>以 <see cref="DefaultDpi"/>（72 DPI）把毫米长度换算为像素。</summary>
    /// <param name="len">毫米长度</param>
    public static double ConverterDpi(double len) => MillimetersToPixel(len, DefaultDpi);

    /// <summary>把 double 序列转为 float 数组（SkiaSharp 多数 API 使用 float）。</summary>
    /// <param name="source">源序列，为 null 时返回空数组</param>
    public static float[] DoubleArrayToFloatArray(IEnumerable<double>? source)
        => source?.Select(v => (float)v).ToArray() ?? Array.Empty<float>();

    // ── 页面区域 ────────────────────────────────────────────

    /// <summary>
    /// 取页面有效区域，优先级 PhysicalBox → ApplicationBox → ContentBox；
    /// 三者都缺失时回退为 <c>(0, 0, width, height)</c>。
    /// </summary>
    /// <param name="pageArea">页面区域定义，可为 null</param>
    /// <param name="width">回退宽度（mm）</param>
    /// <param name="height">回退高度（mm）</param>
    public static STBox GetPageBox(CT_PageArea? pageArea, double width, double height)
    {
        if (pageArea is not null)
        {
            if (pageArea.PhysicalBox is { } pb) return pb;
            if (pageArea.ApplicationBox is { } ab) return ab;
            if (pageArea.ContentBox is { } cb) return cb;
        }
        return new STBox(0, 0, width, height);
    }

    // ── 颜色 ────────────────────────────────────────────────

    /// <summary>
    /// OFD 颜色数组转 CSS 颜色字符串（<c>#rrggbb</c> 或 <c>rgb(r,g,b)</c>）；无法解析返回 null。
    /// <para>
    /// 支持：以 <c>#</c> 开头的十六进制（原样返回）、1 通道灰度、3 通道 RGB、4 通道 CMYK
    /// （经 <see cref="ColorConvert.CmykToRgb"/> 换算，与渲染路径同一套算法）。
    /// 各通道按 0~255 取值并做钳制。
    /// </para>
    /// </summary>
    /// <param name="colorArray">颜色数组，可为 null</param>
    public static string? ConvertOfdColorToHtml(STArray? colorArray)
        => ConvertOfdColorToHtml(colorArray?.ToString());

    /// <summary>
    /// OFD 颜色原始串转 CSS 颜色字符串；无法解析返回 null。
    /// </summary>
    /// <param name="value">颜色值，形如 <c>#FF0000</c> / <c>255 0 0</c> / <c>0 255 255 0</c></param>
    public static string? ConvertOfdColorToHtml(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        string v = value.Trim();
        if (v.StartsWith('#')) return v;   // 已是 CSS 十六进制

        var parts = v.Split(new[] { ' ', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                     .Select(STBase.ToDouble)
                     .ToArray();

        return parts.Length switch
        {
            1 => Rgb(parts[0], parts[0], parts[0]),          // 灰度
            3 => Rgb(parts[0], parts[1], parts[2]),          // RGB
            >= 4 => Rgb4(ColorConvert.CmykToRgb(             // CMYK
                        Clamp255(parts[0]), Clamp255(parts[1]),
                        Clamp255(parts[2]), Clamp255(parts[3]))),
            _ => null,
        };
    }

    /// <summary>
    /// CT_Color 转 CSS 颜色字符串（含 Alpha 时输出 <c>rgba()</c>）。
    /// <para>
    /// 复用 <see cref="OfdColorUtil"/> 的解析结果，与 SkiaSharp 渲染路径保持同一真源，
    /// 因此颜色空间引用、调色板索引、渐变首段等情形都能得到与渲染一致的颜色。
    /// </para>
    /// </summary>
    /// <param name="color">颜色定义，可为 null</param>
    public static string? ConvertColorToHtml(CT_Color? color)
    {
        if (color is null) return null;
        var c = OfdColorUtil.ToSKColor(color, SKColors.Black);
        return c.Alpha == 255
            ? $"rgb({c.Red},{c.Green},{c.Blue})"
            : $"rgba({c.Red},{c.Green},{c.Blue},{Fmt(c.Alpha / 255.0)})";
    }

    // ── 变换矩阵 ────────────────────────────────────────────

    /// <summary>
    /// 把 6 元素变换矩阵（CTM）转为 SVG / CSS 的 <c>matrix()</c> 串。
    /// <para>
    /// 平移分量 e、f 会按 <see cref="ConverterDpi"/> 从毫米换算为像素（与 Java 原实现一致）。
    /// </para>
    /// </summary>
    /// <param name="ctms">6 元素 CTM（a b c d e f）</param>
    /// <param name="cssComma">
    /// false（默认）输出 SVG 风格空格分隔 <c>matrix(a b c d e f)</c>；
    /// true 输出 CSS 风格逗号分隔 <c>matrix(a, b, c, d, e, f)</c>。
    /// </param>
    public static string MatrixFrom(STArray ctms, bool cssComma = false)
    {
        ArgumentNullException.ThrowIfNull(ctms);
        if (ctms.Count < 6)
            throw new ArgumentException($"变换矩阵需要 6 个元素，实际 {ctms.Count} 个", nameof(ctms));

        double a = ctms[0], b = ctms[1], c = ctms[2];
        double d = ctms[3], e = ConverterDpi(ctms[4]), f = ConverterDpi(ctms[5]);

        return cssComma
            ? $"matrix({Fmt(a)}, {Fmt(b)}, {Fmt(c)}, {Fmt(d)}, {Fmt(e)}, {Fmt(f)})"
            : $"matrix({Fmt(a)} {Fmt(b)} {Fmt(c)} {Fmt(d)} {Fmt(e)} {Fmt(f)})";
    }

    // ── 临时文件 ────────────────────────────────────────────

    /// <summary>在系统临时目录下生成一个不重复的文件路径（不含扩展名，文件尚未创建）。</summary>
    public static string GenerateTempFilePath()
        => Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));

    // ── 内部 ────────────────────────────────────────────────

    private static string Rgb(double r, double g, double b)
        => $"rgb({Clamp255(r)},{Clamp255(g)},{Clamp255(b)})";

    private static string Rgb4(int[] rgb) => Rgb(rgb[0], rgb[1], rgb[2]);

    private static int Clamp255(double v) => (int)Math.Max(0, Math.Min(255, Math.Round(v)));

    private static string Fmt(double v) => v.ToString("0.######", CultureInfo.InvariantCulture);
}
