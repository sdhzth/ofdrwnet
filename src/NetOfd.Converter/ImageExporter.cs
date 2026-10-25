using SkiaSharp;
using System.Collections.Concurrent;
using System.IO.Compression;

namespace NetOfd.Converter;

/// <summary>
/// OFD → 图像 导出器（对应 Java ofdrw-converter 的 ImageExporter）。
/// <para>
/// 用法：
/// <code>
/// using var exporter = new ImageExporter("input.ofd", "out_images");
/// var paths = exporter.Export();   // 导出全部页为 0.png, 1.png ...
/// // 或仅导出部分页：exporter.Export(0, 2);
/// </code>
/// </para>
/// </summary>
public class ImageExporter : IDisposable
{
    private readonly OfdImageMaker _maker;
    private readonly string _outDir;
    private readonly string _imageType;
    private readonly ConverterOptions _opts;
    private readonly string? _ofdPath;
    private readonly List<string> _generated = new();
    private bool _disposed;

    /// <summary>
    /// 并行导出模式：每个页面在独立的工作线程中使用各自的文档实例渲染与编码。
    /// <para>多核机器上可显著缩短大批量页面的导出耗时（瓶颈为图像编码，可跨页并行）；
    /// 代价是峰值内存约为 单页位图 × 并行度。仅在以<b>文件路径</b>构造时可用。</para>
    /// </summary>
    public bool ParallelExport { get; set; }

    /// <summary>并行导出时的最大并行度，默认处理器核心数。</summary>
    public int MaxParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// 从文件路径创建导出器。
    /// </summary>
    /// <param name="ofdPath">OFD 文件路径</param>
    /// <param name="outDir">图像输出目录</param>
    /// <param name="imageType">图像格式（png/jpg/bmp/gif/webp），默认 png</param>
    /// <param name="ppm">像素/毫米，默认 15</param>
    public ImageExporter(string ofdPath, string outDir, string imageType = "png", double ppm = 15)
    {
        _maker = new OfdImageMaker(ofdPath);
        _outDir = outDir;
        _imageType = NormalizeType(imageType);
        _opts = new ConverterOptions { PPM = ppm };
        _ofdPath = ofdPath;
    }

    /// <summary>从流创建导出器。</summary>
    public ImageExporter(Stream ofdInput, string outDir, string imageType = "png", double ppm = 15)
    {
        _maker = new OfdImageMaker(ofdInput);
        _outDir = outDir;
        _imageType = NormalizeType(imageType);
        _opts = new ConverterOptions { PPM = ppm };
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _maker.PageCount;

    /// <summary>设置像素/毫米（Pixels Per Millimeter）。</summary>
    public void SetPPM(double ppm) => _opts.PPM = ppm;

    /// <summary>按 DPI 设置清晰度（dpi ≈ ppm * 254 / 10）。</summary>
    public void SetDPI(int dpi) => _opts.PPM = dpi * 0.01 / 0.254;

    /// <summary>
    /// 导出指定页为图像文件。不传参数则导出全部页。
    /// 文件名形如 <c>0.png</c>、<c>1.png</c>（索引为原文档页码）。
    /// </summary>
    /// <param name="indexes">要导出的页码（0-based），可乱序、可重复；为空表示全部</param>
    /// <returns>生成的图像文件路径列表</returns>
    public List<string> Export(params int[] indexes)
    {
        Directory.CreateDirectory(_outDir);
        var list = (indexes is { Length: > 0 })
            ? indexes
            : Enumerable.Range(0, _maker.PageCount).ToArray();

        var result = ParallelExport && list.Length > 1
            ? ExportParallel(list)
            : ExportSequential(list);

        _generated.AddRange(result);
        return result;
    }

    private List<string> ExportSequential(int[] list)
    {
        var result = new List<string>();
        foreach (var idx in list)
        {
            var bitmap = _maker.MakePage(idx, _opts);
            if (bitmap is null) continue;

            string path = Path.Combine(_outDir, $"{idx}.{_imageType}");
            try
            {
                SaveImage(bitmap, path);
                result.Add(path);
            }
            finally
            {
                bitmap.Dispose();
            }
        }
        return result;
    }

    private List<string> ExportParallel(int[] list)
    {
        if (_ofdPath is null)
            throw new InvalidOperationException(
                "并行导出仅支持以文件路径构造的 ImageExporter（流构造请先落盘）");

        var results = new ConcurrentDictionary<int, string>();
        var errors = new ConcurrentQueue<Exception>();
        using var semaphore = new SemaphoreSlim(Math.Max(1, MaxParallelism));
        var tasks = list.Select(idx => Task.Run(() =>
        {
            try
            {
                semaphore.Wait();
                try
                {
                    // 每页独立文档实例，规避共享容器懒加载缓存的线程安全问题
                    using var maker = new OfdImageMaker(_ofdPath);
                    var bitmap = maker.MakePage(idx, _opts);
                    if (bitmap is null) return;
                    string path = Path.Combine(_outDir, $"{idx}.{_imageType}");
                    try
                    {
                        SaveImage(bitmap, path);
                        results[idx] = path;
                    }
                    finally
                    {
                        bitmap.Dispose();
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            }
            catch (Exception ex)
            {
                errors.Enqueue(ex);
            }
        })).ToArray();

        Task.WaitAll(tasks);
        if (!errors.IsEmpty)
            throw new AggregateException("并行导出部分页面失败", errors);

        return list.Where(results.ContainsKey).Select(idx => results[idx]).ToList();
    }

    /// <summary>已生成的图像文件路径。</summary>
    public List<string> GetImagePaths() => new(_generated);

    public void Close() => Dispose();

    public void Dispose()
    {
        if (!_disposed)
        {
            _maker.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }

    private void SaveImage(SKBitmap bitmap, string path)
    {
        var format = ToImageFormat(_imageType);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 100);
        if (data is null) throw new InvalidOperationException("图像编码失败");
        File.WriteAllBytes(path, data.ToArray());
    }

    private static string NormalizeType(string type)
    {
        type = (type ?? "png").Trim().ToLowerInvariant();
        return type switch
        {
            "jpg" or "jpeg" => "jpg",
            "bmp" => "bmp",
            "gif" => "gif",
            "webp" => "webp",
            _ => "png",
        };
    }

    private static SKEncodedImageFormat ToImageFormat(string type) => type switch
    {
        "jpg" => SKEncodedImageFormat.Jpeg,
        "bmp" => SKEncodedImageFormat.Bmp,
        "gif" => SKEncodedImageFormat.Gif,
        "webp" => SKEncodedImageFormat.Webp,
        _ => SKEncodedImageFormat.Png,
    };
}
