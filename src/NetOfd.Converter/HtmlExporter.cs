using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Reader;
using SkiaSharp;
using System.Text;

namespace NetOfd.Converter;

/// <summary>
/// 基于 SVG 的 OFD → HTML 导出器（对应 Java ofdrw-converter 的 <c>HTMLExporter</c>）。
/// <para>
/// 输出单个 HTML 文件，每页一个 <c>&lt;div&gt;</c>：
/// 底层是 <see cref="OfdSvgMaker"/> 渲染出的矢量图形，
/// 上层叠加一个<b>透明但可选中</b>的文本层（每个 TextCode 一个绝对定位的 <c>span</c>），
/// 使导出的页面既保真又能复制 / 检索文字——这是 Java 版 <c>HtmlMaker</c> 文本层的同类做法。
/// </para>
/// <para>
/// 用法：
/// <code>
/// using (var exporter = new HtmlExporter("input.ofd", "out.html"))
///     exporter.Export();
/// </code>
/// 注意必须释放（或调用 <see cref="Close"/>），HTML 的结尾标签在释放时才写出。
/// </para>
/// </summary>
public class HtmlExporter : IOfdExporter
{
    private readonly OFDReader _reader;
    private readonly bool _ownsReader;
    private readonly OfdSvgMaker _svgMaker;
    private readonly TextWriter _out;
    private readonly bool _ownsWriter;
    private bool _headerWritten;
    private bool _disposed;

    /// <summary>页面渲染宽度（CSS 像素），默认 1000。决定 SVG 的 PPM。</summary>
    public int ScreenWidth { get; set; } = 1000;

    /// <summary>是否生成可选中的文本层，默认 <c>true</c>。</summary>
    public bool TextLayer { get; set; } = true;

    /// <summary>
    /// 调试用：把文本层显示为红色可见文字（默认 <c>false</c>，即透明）。
    /// </summary>
    public bool VisibleTextLayer { get; set; }

    /// <summary>文档标题（写入 <c>&lt;title&gt;</c>），默认“文件预览”。</summary>
    public string Title { get; set; } = "文件预览";

    /// <summary>从文件路径创建。</summary>
    public HtmlExporter(string ofdPath, string htmlPath)
    {
        if (string.IsNullOrWhiteSpace(htmlPath))
            throw new ArgumentException("导出 HTML 文件路径为空", nameof(htmlPath));

        string full = Path.GetFullPath(htmlPath);
        string? parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
        {
            if (File.Exists(parent))
                throw new ArgumentException($"已经存在同名文件: {parent}", nameof(htmlPath));
            Directory.CreateDirectory(parent);
        }

        _reader = new OFDReader(ofdPath);
        _ownsReader = true;
        _svgMaker = new OfdSvgMaker(_reader);
        _out = new StreamWriter(File.Create(full), new UTF8Encoding(false));
        _ownsWriter = true;
    }

    /// <summary>从流创建（两个流均由调用方负责关闭）。</summary>
    public HtmlExporter(Stream ofdInput, Stream htmlOutput)
    {
        if (htmlOutput is null) throw new ArgumentNullException(nameof(htmlOutput));
        _reader = new OFDReader(ofdInput);
        _ownsReader = true;
        _svgMaker = new OfdSvgMaker(_reader);
        _out = new StreamWriter(htmlOutput, new UTF8Encoding(false), leaveOpen: true);
        _ownsWriter = true;
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _reader.GetPageCount();

    /// <inheritdoc/>
    public void Export(params int[] indexes)
    {
        EnsureHeader();
        var targets = ExportPageSelector.Normalize(indexes, _reader.GetPageCount());
        try
        {
            foreach (int index in targets)
                _out.Write(MakePageDiv(index));
            _out.Flush();
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("HTML 转换异常", e);
        }
    }

    /// <summary>
    /// 生成单页的 HTML 片段（不含文档头尾），便于嵌入到已有页面。
    /// </summary>
    public string MakePageDiv(int pageIndex)
    {
        var (wmm, hmm) = _svgMaker.GetPageSize(pageIndex);
        // 以 ScreenWidth 反推 PPM，使页面宽度正好等于 ScreenWidth 像素
        double ppm = ScreenWidth / Math.Max(wmm, 0.0001);
        _svgMaker.SetPPM(ppm);

        int wpx = (int)Math.Round(wmm * ppm);
        int hpx = (int)Math.Round(hmm * ppm);
        string svg = _svgMaker.MakePage(pageIndex);

        var sb = new StringBuilder();
        sb.Append("<div class=\"ofd-page\" style=\"position:relative;background:#fff;overflow:hidden;")
          .Append($"width:{wpx}px;height:{hpx}px;box-shadow:0 1px 4px rgba(0,0,0,.2);margin:10px auto;\">");

        // 视觉层：SVG
        sb.Append("<div class=\"ofd-canvas\" style=\"position:absolute;left:0;top:0;\">")
          .Append(svg)
          .Append("</div>");

        // 文本层：透明可选中
        if (TextLayer)
            sb.Append(BuildTextLayer(pageIndex, ppm));

        sb.Append("</div>");
        return sb.ToString();
    }

    // ── 文本层 ──────────────────────────────────────────────
    private string BuildTextLayer(int pageIndex, double ppm)
    {
        var page = _reader.GetPage(pageIndex);
        var content = page?.Content;
        if (content is null) return string.Empty;

        string color = VisibleTextLayer ? "rgba(255,0,0,.75)" : "transparent";
        var sb = new StringBuilder();
        sb.Append("<div class=\"ofd-text\" style=\"position:absolute;left:0;top:0;")
          .Append($"color:{color};white-space:pre;\">");

        foreach (var layer in content.GetLayersInZOrder())
        {
            foreach (var obj in layer.GetGraphicObjects())
            {
                if (obj is not CTText text) continue;
                AppendTextSpans(sb, text, ppm);
            }
        }

        sb.Append("</div>");
        return sb.ToString();
    }

    private static void AppendTextSpans(StringBuilder sb, CTText text, double ppm)
    {
        // device = Scale(ppm) · Translate(boundary) · CTM  —— 与 OfdPageRenderer.RenderTo 保持一致
        var m = SKMatrix.CreateScale((float)ppm, (float)ppm);
        if (text.Boundary is { } b)
            m = SKMatrix.Concat(m, SKMatrix.CreateTranslation((float)b.TopLeftX, (float)b.TopLeftY));
        if (text.CTM is { Count: >= 6 } ctm)
            m = SKMatrix.Concat(m, new SKMatrix
            {
                ScaleX = (float)ctm[0],
                SkewY = (float)ctm[1],
                SkewX = (float)ctm[2],
                ScaleY = (float)ctm[3],
                TransX = (float)ctm[4],
                TransY = (float)ctm[5],
                Persp2 = 1,
            });

        // 字号：Size 视为毫米（与 ConverterOptions.TextSizeScale 的默认约定一致），
        // 再乘以矩阵的纵向缩放分量换算为像素
        double vScale = Math.Sqrt(Math.Abs(m.ScaleY * m.ScaleY + m.SkewY * m.SkewY));
        double fontPx = text.Size * vScale;
        if (fontPx <= 0) fontPx = 1;

        foreach (var tc in text.GetTextCodes())
        {
            string s = tc.Text ?? "";
            if (s.Trim().Length == 0) continue;

            var p = m.MapPoint((float)tc.X, (float)tc.Y);
            // span 以基线定位：top 需要上移一个字号高度
            double top = p.Y - fontPx;
            sb.Append("<span style=\"position:absolute;")
              .Append($"left:{Fmt(p.X)}px;top:{Fmt(top)}px;font-size:{Fmt(fontPx)}px;line-height:{Fmt(fontPx)}px;\">")
              .Append(HtmlEscape(s))
              .Append("</span>");
        }
    }

    // ── HTML 骨架 ───────────────────────────────────────────
    private void EnsureHeader()
    {
        if (_headerWritten) return;
        _headerWritten = true;
        _out.Write(
            "<!DOCTYPE html>\n<html lang=\"zh-CN\">\n<head>\n" +
            "  <meta charset=\"UTF-8\">\n" +
            "  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
            $"  <title>{HtmlEscape(Title)}</title>\n" +
            "  <style>\n" +
            "    body { margin:0; background:#E6E8EB; }\n" +
            "    .ofd-doc { display:flex; flex-direction:column; align-items:center; padding:10px 0; }\n" +
            "    .ofd-page svg { display:block; }\n" +
            "    .ofd-text span { transform-origin: 0 0; }\n" +
            "  </style>\n" +
            "</head>\n<body>\n<div class=\"ofd-doc\">\n");
    }

    private void WriteFooter()
    {
        EnsureHeader();
        _out.Write("\n</div>\n</body>\n</html>");
    }

    private static string Fmt(double v)
        => v.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);

    private static string HtmlEscape(string s)
        => s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    public void Close() => Dispose();

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            try { WriteFooter(); _out.Flush(); }
            catch (ObjectDisposedException) { /* 输出流已被外部关闭，忽略 */ }

            _svgMaker.Dispose();
            if (_ownsWriter) _out.Dispose();
            if (_ownsReader) _reader.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
