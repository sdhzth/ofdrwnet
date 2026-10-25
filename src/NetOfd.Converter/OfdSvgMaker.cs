using NetOfd.Reader;
using SkiaSharp;
using System.Text;

namespace NetOfd.Converter;

/// <summary>
/// 低阶 OFD→SVG 渲染器（对应 Java ofdrw-converter 的 <c>SVGMaker</c>）。
/// <para>
/// Java 版依赖 Apache Batik 的 <c>SVGGraphics2D</c>——一个“把 Graphics2D 调用录制成 SVG”的画布。
/// .NET 版使用 SkiaSharp 内置的 <see cref="SKSvgCanvas"/> 作为等价物：
/// 它同样返回一个 <see cref="SKCanvas"/>，所有绘制指令被序列化为 SVG 文本，
/// 因此可与位图渲染<b>完全复用</b> <see cref="OfdPageRenderer.RenderTo"/> 的绘制逻辑。
/// </para>
/// </summary>
public class OfdSvgMaker : IDisposable
{
    private readonly OFDReader _reader;
    private readonly bool _ownsReader;
    private bool _disposed;

    /// <summary>渲染配置（可在调用 <see cref="MakePage"/> 前调整）。</summary>
    public ConverterOptions Options { get; }

    /// <summary>从文件路径创建。</summary>
    public OfdSvgMaker(string ofdPath, double ppm = 15)
    {
        _reader = new OFDReader(ofdPath);
        _ownsReader = true;
        Options = new ConverterOptions { PPM = ppm };
    }

    /// <summary>从流创建。</summary>
    public OfdSvgMaker(Stream ofdInput, double ppm = 15)
    {
        _reader = new OFDReader(ofdInput);
        _ownsReader = true;
        Options = new ConverterOptions { PPM = ppm };
    }

    /// <summary>
    /// 复用已有的 <see cref="OFDReader"/>（<b>不</b>接管其生命周期，调用方负责释放）。
    /// 对应 Java <c>new SVGMaker(reader, ppm)</c>。
    /// </summary>
    public OfdSvgMaker(OFDReader reader, double ppm = 15)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _ownsReader = false;
        Options = new ConverterOptions { PPM = ppm };
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _reader.GetPageCount();

    /// <summary>设置像素/毫米。</summary>
    public void SetPPM(double ppm) => Options.PPM = ppm;

    /// <summary>按 DPI 设置清晰度。</summary>
    public void SetDPI(int dpi) => Options.PPM = dpi * 0.01 / 0.254;

    /// <summary>页面尺寸（mm）。</summary>
    public (double Width, double Height) GetPageSize(int pageIndex)
        => PageSizeUtil.GetPageSize(_reader, pageIndex);

    /// <summary>渲染后的 SVG 画布像素尺寸。</summary>
    public (int Width, int Height) GetPagePixelSize(int pageIndex)
    {
        var (wmm, hmm) = GetPageSize(pageIndex);
        return (PixelOf(wmm, Options.PPM), PixelOf(hmm, Options.PPM));
    }

    /// <summary>
    /// 渲染指定页为 SVG 字符串。
    /// </summary>
    /// <param name="pageIndex">页码（0 起）</param>
    /// <returns>完整的 SVG 文档文本</returns>
    /// <exception cref="GeneralConvertException">页码越界或渲染失败</exception>
    public string MakePage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount)
            throw new GeneralConvertException($"{pageIndex} 不是有效的页码索引");

        var page = _reader.GetPage(pageIndex)
            ?? throw new GeneralConvertException($"第 {pageIndex} 页内容为空");

        var (wmm, hmm) = GetPageSize(pageIndex);
        int w = PixelOf(wmm, Options.PPM);
        int h = PixelOf(hmm, Options.PPM);

        using var ms = new MemoryStream();
        // 注意：SVG 的结尾标签在 canvas / wstream 释放时才写出，
        // 因此必须先离开 using 块再读取 MemoryStream，否则拿到的是残缺 XML。
        using (var wstream = new SKManagedWStream(ms, disposeManagedStream: false))
        using (var canvas = SKSvgCanvas.Create(new SKRect(0, 0, w, h), wstream))
        {
            // SVG 画布没有 Clear 语义，背景需要显式绘制成一个矩形
            if (Options.Background.Alpha != 0)
            {
                using var bg = new SKPaint { Style = SKPaintStyle.Fill, Color = Options.Background };
                canvas.DrawRect(new SKRect(0, 0, w, h), bg);
            }

            OfdPageRenderer.RenderTo(canvas, page, _reader.GetResourceManager(), Options);
        }

        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static int PixelOf(double mm, double ppm)
        => (int)Math.Max(1, Math.Round(mm * ppm));

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
