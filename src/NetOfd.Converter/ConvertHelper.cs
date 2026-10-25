namespace NetOfd.Converter;

/// <summary>
/// OFD 转换门面：一行代码完成 OFD → PDF / HTML / 纯文本 / 图像 / SVG。
/// <para>
/// 内部只是各 Exporter 的薄封装（构造 → Export → Dispose），不改变任何转换行为，
/// 只为常见"给路径、拿结果"场景省去样板代码。需要精细控制（DPI、并行度、
/// 复用 reader）时请直接使用对应的 <see cref="PdfExporter"/> / <see cref="ImageExporter"/> 等类型。
/// </para>
/// <para>
/// 页码约定与 <see cref="IOfdExporter.Export(int[])"/> 一致：<b>从 0 起</b>，可不传（全部页）、
/// 可乱序、可重复。
/// </para>
/// 对应 Java: org.ofdrw.converter.ConvertHelper（Java 版仅覆盖 toPdf/toHtml，此处按 netofd 现有导出器补全全部格式）
/// </summary>
public static class ConvertHelper
{
    // ────────────────────────────────────────────────────────
    //  PDF
    // ────────────────────────────────────────────────────────

    /// <summary>OFD 文件 → PDF 文件。</summary>
    public static void ToPdf(string ofdPath, string pdfPath, params int[] indexes)
    {
        using var exporter = new PdfExporter(ofdPath, pdfPath);
        exporter.Export(indexes);
    }

    /// <summary>OFD 文件 → PDF 流（输出流由调用方关闭）。</summary>
    public static void ToPdf(string ofdPath, Stream output, params int[] indexes)
    {
        using var exporter = new PdfExporter(ofdPath, output);
        exporter.Export(indexes);
    }

    /// <summary>OFD 流 → PDF 文件（输入流由调用方关闭）。</summary>
    public static void ToPdf(Stream input, string pdfPath, params int[] indexes)
    {
        using var exporter = new PdfExporter(input, pdfPath);
        exporter.Export(indexes);
    }

    /// <summary>OFD 流 → PDF 流（两个流均由调用方关闭）。</summary>
    public static void ToPdf(Stream input, Stream output, params int[] indexes)
    {
        using var exporter = new PdfExporter(input, output);
        exporter.Export(indexes);
    }

    /// <summary>OFD 文件 → PDF 字节数组（便于 Web 下载 / 内存传递）。</summary>
    public static byte[] ToPdfBytes(string ofdPath, params int[] indexes)
    {
        using var ms = new MemoryStream();
        using (var exporter = new PdfExporter(ofdPath, ms))
        {
            exporter.Export(indexes);
        }
        return ms.ToArray();
    }

    /// <summary>OFD 流 → PDF 字节数组（输入流由调用方关闭）。</summary>
    public static byte[] ToPdfBytes(Stream input, params int[] indexes)
    {
        using var ms = new MemoryStream();
        using (var exporter = new PdfExporter(input, ms))
        {
            exporter.Export(indexes);
        }
        return ms.ToArray();
    }

    // ────────────────────────────────────────────────────────
    //  HTML
    // ────────────────────────────────────────────────────────

    /// <summary>OFD 文件 → HTML 文件。</summary>
    public static void ToHtml(string ofdPath, string htmlPath, params int[] indexes)
    {
        using var exporter = new HtmlExporter(ofdPath, htmlPath);
        exporter.Export(indexes);
    }

    /// <summary>OFD 流 → HTML 流（两个流均由调用方关闭）。</summary>
    public static void ToHtml(Stream input, Stream output, params int[] indexes)
    {
        using var exporter = new HtmlExporter(input, output);
        exporter.Export(indexes);
    }

    // ────────────────────────────────────────────────────────
    //  纯文本
    // ────────────────────────────────────────────────────────

    /// <summary>OFD 文件 → 纯文本文件。</summary>
    public static void ToText(string ofdPath, string txtPath, params int[] indexes)
    {
        using var exporter = new TextExporter(ofdPath, txtPath);
        exporter.Export(indexes);
    }

    /// <summary>OFD 流 → 纯文本流（两个流均由调用方关闭）。</summary>
    public static void ToText(Stream input, Stream output, params int[] indexes)
    {
        using var exporter = new TextExporter(input, output);
        exporter.Export(indexes);
    }

    // ────────────────────────────────────────────────────────
    //  图像 / SVG（输出到目录，返回生成文件列表）
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// OFD 文件 → 图像文件序列（每页一个文件，形如 <c>0.png</c>、<c>1.png</c>）。
    /// </summary>
    /// <param name="ofdPath">OFD 文件路径</param>
    /// <param name="outDir">输出目录（不存在则创建）</param>
    /// <param name="imageType">图像格式，如 png / jpeg</param>
    /// <param name="ppm">每毫米像素数，默认 15</param>
    /// <param name="indexes">页码（0 起），为空表示全部</param>
    /// <returns>生成的图像文件路径列表</returns>
    public static List<string> ToImage(string ofdPath, string outDir,
        string imageType = "png", double ppm = 15, params int[] indexes)
    {
        using var exporter = new ImageExporter(ofdPath, outDir, imageType, ppm);
        return exporter.Export(indexes);
    }

    /// <summary>
    /// OFD 文件 → SVG 文件序列（每页一个文件，形如 <c>0.svg</c>、<c>1.svg</c>）。
    /// </summary>
    public static List<string> ToSvg(string ofdPath, string outDir,
        double ppm = 15, params int[] indexes)
    {
        using var exporter = new SvgExporter(ofdPath, outDir, ppm);
        exporter.Export(indexes);
        return exporter.GetSvgFilePaths();
    }
}
