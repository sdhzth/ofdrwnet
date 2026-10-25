namespace NetOfd.Converter;

/// <summary>
/// OFD → PDF 导出器（对应 Java ofdrw-converter 的 <c>PDFExporterIText</c>/<c>PDFExporterPDFBox</c>）。
/// <para>
/// 用法：
/// <code>
/// using var exporter = new PdfExporter("input.ofd", "output.pdf");
/// exporter.Export();                 // 导出全部页
/// // 或
/// exporter.Export(0, 2);             // 仅导出第 1、3 页
/// </code>
/// </para>
/// </summary>
public class PdfExporter : IOfdExporter
{
    private readonly OfdPdfMaker _maker;
    private readonly string? _outPath;
    private readonly Stream? _outStream;
    private bool _disposed;
    /// <summary>从文件路径创建导出器。</summary>
    /// <param name="ofdPath">待转换 OFD 文件</param>
    /// <param name="pdfPath">PDF 输出文件路径</param>
    public PdfExporter(string ofdPath, string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF 输出路径为空", nameof(pdfPath));
        _outPath = Path.GetFullPath(pdfPath);
        _maker = new OfdPdfMaker(ofdPath);
    }

    /// <summary>从流创建导出器（输入流由调用方负责关闭）。</summary>
    public PdfExporter(Stream ofdInput, string pdfPath)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            throw new ArgumentException("PDF 输出路径为空", nameof(pdfPath));
        _outPath = Path.GetFullPath(pdfPath);
        _maker = new OfdPdfMaker(ofdInput);
    }

    /// <summary>输出到流（输出流由调用方负责关闭）。与 <see cref="HtmlExporter"/> / <see cref="TextExporter"/> 的对称能力。</summary>
    public PdfExporter(string ofdPath, Stream pdfOutput)
    {
        _outStream = pdfOutput ?? throw new ArgumentNullException(nameof(pdfOutput));
        _maker = new OfdPdfMaker(ofdPath);
    }

    /// <summary>从流输入、输出到流（两个流均由调用方负责关闭）。</summary>
    public PdfExporter(Stream ofdInput, Stream pdfOutput)
    {
        _outStream = pdfOutput ?? throw new ArgumentNullException(nameof(pdfOutput));
        _maker = new OfdPdfMaker(ofdInput);
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _maker.PageCount;

    /// <inheritdoc/>
    public void Export(params int[] indexes)
    {
        var targets = ExportPageSelector.Normalize(indexes, _maker.PageCount);
        try
        {
            // 路径输出时自行创建并关闭文件流；外部传入的输出流交由调用方关闭。
            Stream? owned = null;
            Stream stream;
            if (_outStream is not null)
            {
                stream = _outStream;
            }
            else
            {
                var dir = Path.GetDirectoryName(_outPath!);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                stream = owned = File.Create(_outPath!);
            }

            try
            {
                using var doc = SkiaSharp.SKDocument.CreatePdf(stream);
                foreach (int index in targets)
                {
                    _maker.MakePage(doc, index);
                }
                doc.Close();
            }
            finally
            {
                owned?.Dispose();
            }
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("PDF 转换异常", e);
        }
    }

    /// <summary>已生成的 PDF 文件路径（Export 后有效；输出到流时为 <c>null</c>）。</summary>
    public string? GetPdfFilePath() => _outPath;

    public void Dispose()
    {
        if (!_disposed)
        {
            _maker.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
