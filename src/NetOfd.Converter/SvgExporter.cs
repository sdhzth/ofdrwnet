using System.Text;

namespace NetOfd.Converter;

/// <summary>
/// OFD → SVG 导出器（对应 Java ofdrw-converter 的 <c>SVGExporter</c>）。
/// <para>
/// 用法：
/// <code>
/// using var exporter = new SvgExporter("input.ofd", "out_svg");
/// exporter.Export();                 // 全部页 → 0.svg, 1.svg ...
/// var files = exporter.GetSvgFilePaths();
/// </code>
/// </para>
/// </summary>
public class SvgExporter : IOfdExporter
{
    private readonly OfdSvgMaker _maker;
    private readonly string _outDir;
    private readonly List<string> _generated = new();
    private bool _disposed;

    /// <summary>从文件路径创建导出器。</summary>
    /// <param name="ofdPath">待转换 OFD 文件</param>
    /// <param name="outDir">SVG 输出目录（不存在则创建）</param>
    /// <param name="ppm">每毫米像素数（Pixels Per Millimeter），默认 15</param>
    public SvgExporter(string ofdPath, string outDir, double ppm = 15)
    {
        _outDir = PrepareDir(outDir);
        _maker = new OfdSvgMaker(ofdPath, ppm);
    }

    /// <summary>从流创建导出器（流由调用方负责关闭）。</summary>
    public SvgExporter(Stream ofdInput, string outDir, double ppm = 15)
    {
        _outDir = PrepareDir(outDir);
        _maker = new OfdSvgMaker(ofdInput, ppm);
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _maker.PageCount;

    /// <summary>
    /// 设置转换质量。请在 <see cref="Export(int[])"/> 之前调用。
    /// </summary>
    public void SetPPM(double ppm) => _maker.SetPPM(ppm);

    /// <inheritdoc/>
    public void Export(params int[] indexes)
    {
        var targets = ExportPageSelector.Normalize(indexes, _maker.PageCount);
        try
        {
            foreach (int index in targets)
            {
                string svg = _maker.MakePage(index);
                // 与 Java 一致：文件名按“已生成数量”递增，而非原始页码
                string dst = Path.Combine(_outDir, $"{_generated.Count}.svg");
                File.WriteAllText(dst, svg, new UTF8Encoding(false));
                _generated.Add(dst);
            }
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("SVG 转换异常", e);
        }
    }

    /// <summary>已生成的 SVG 文件路径。</summary>
    public List<string> GetSvgFilePaths() => new(_generated);

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

    private static string PrepareDir(string outDir)
    {
        if (string.IsNullOrWhiteSpace(outDir))
            throw new ArgumentException("导出目录路径为空", nameof(outDir));
        string full = Path.GetFullPath(outDir);
        if (File.Exists(full))
            throw new ArgumentException($"已经存在同名文件: {full}", nameof(outDir));
        Directory.CreateDirectory(full);
        return full;
    }
}
