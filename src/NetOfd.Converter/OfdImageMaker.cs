using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicType;
using NetOfd.Reader;
using NetOfd.Reader.Model;
using SkiaSharp;

namespace NetOfd.Converter;

/// <summary>
/// 低阶 OFD→图像渲染器（对应 Java ofdrw-converter 的 ImageMaker）。
/// 接收 <see cref="OFDReader"/> 与渲染参数，逐页生成位图。
/// </summary>
public class OfdImageMaker : IDisposable
{
    private readonly OFDReader _reader;
    private List<AnnotionEntity>? _annots;
    private bool _disposed;

    public OfdImageMaker(string ofdPath)
    {
        _reader = new OFDReader(ofdPath);
    }

    public OfdImageMaker(Stream ofdInput)
    {
        _reader = new OFDReader(ofdInput);
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _reader.GetPageCount();

    /// <summary>获取指定页（0-based）的位图；页码越界返回 null。</summary>
    public SKBitmap? MakePage(int pageIndex, ConverterOptions? opts = null)
    {
        opts ??= new ConverterOptions();
        var page = _reader.GetPage(pageIndex);
        if (page is null) return null;

        var (w, h) = GetPageSize(pageIndex);
        // 注释实体（印章/水印等）懒加载并缓存：图层渲染之后由渲染器绘制（对齐 Java AWTMaker）
        _annots ??= _reader.GetAnnotationEntities();
        return OfdPageRenderer.Render(page, _reader.GetResourceManager(), w, h, opts, _annots);
    }

    /// <summary>页面尺寸（mm）：优先 PageObject.Area，其次 Document.CommonData.PageArea，最后 A4。</summary>
    public (double Width, double Height) GetPageSize(int pageIndex)
        => PageSizeUtil.GetPageSize(_reader, pageIndex);

    public void Dispose()
    {
        if (!_disposed)
        {
            _reader.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
