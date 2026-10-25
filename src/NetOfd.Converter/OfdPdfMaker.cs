using NetOfd.Reader;
using SkiaSharp;

namespace NetOfd.Converter;

/// <summary>
/// 低阶 OFD→PDF 渲染器（对应 Java ofdrw-converter 的 <c>ItextMaker</c>/<c>PdfboxMaker</c>）。
/// <para>
/// 利用 SkiaSharp 内置的 <c>SKDocument.CreatePdf</c> 生成 PDF；
/// 因 PDF 画布同为 <see cref="SKCanvas"/>，可与位图 / SVG 完全复用
/// <see cref="OfdPageRenderer.RenderTo"/> 的绘制逻辑。
/// </para>
/// </summary>
public class OfdPdfMaker : IDisposable
{
    private readonly OFDReader _reader;
    private readonly bool _ownsReader;
    private bool _disposed;

    /// <summary>渲染配置（可在调用 <see cref="MakePage"/> 前调整）。</summary>
    public ConverterOptions Options { get; }

    /// <summary>从文件路径创建。</summary>
    public OfdPdfMaker(string ofdPath)
    {
        _reader = new OFDReader(ofdPath);
        _ownsReader = true;
        Options = new ConverterOptions { PPM = 72.0 / 25.4 };
    }

    /// <summary>从流创建。</summary>
    public OfdPdfMaker(Stream ofdInput)
    {
        _reader = new OFDReader(ofdInput);
        _ownsReader = true;
        Options = new ConverterOptions { PPM = 72.0 / 25.4 };
    }

    /// <summary>
    /// 复用已有的 <see cref="OFDReader"/>（<b>不</b>接管其生命周期，调用方负责释放）。
    /// </summary>
    public OfdPdfMaker(OFDReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _ownsReader = false;
        Options = new ConverterOptions { PPM = 72.0 / 25.4 };
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _reader.GetPageCount();

    /// <summary>页面尺寸（mm）。</summary>
    public (double Width, double Height) GetPageSize(int pageIndex)
        => PageSizeUtil.GetPageSize(_reader, pageIndex);

    /// <summary>
    /// 将指定页渲染并追加到 PDF 文档。
    /// </summary>
    /// <param name="doc">PDF 文档（由调用方创建和管理）</param>
    /// <param name="pageIndex">页码（0 起）</param>
    /// <exception cref="GeneralConvertException">页码越界或渲染失败</exception>
    public void MakePage(SKDocument doc, int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
            throw new GeneralConvertException($"{pageIndex} 不是有效的页码索引");

        var page = _reader.GetPage(pageIndex)
            ?? throw new GeneralConvertException($"第 {pageIndex} 页内容为空");

        var (wmm, hmm) = GetPageSize(pageIndex);
        // PDF 单位为点（1pt = 1/72 inch），1mm = 72/25.4 pt
        float wpt = (float)(wmm * 72.0 / 25.4);
        float hpt = (float)(hmm * 72.0 / 25.4);

        using var canvas = doc.BeginPage(wpt, hpt);
        OfdPageRenderer.RenderTo(canvas, page, _reader.GetResourceManager(), Options);
        doc.EndPage();
    }

    /// <summary>
    /// 将所有页渲染并写入指定流，生成完整 PDF 文件。
    /// </summary>
    public void MakeAll(Stream outputStream)
    {
        using var doc = SKDocument.CreatePdf(outputStream);
        for (int i = 0; i < PageCount; i++)
        {
            MakePage(doc, i);
        }
        doc.Close();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            if (_ownsReader) _reader.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
