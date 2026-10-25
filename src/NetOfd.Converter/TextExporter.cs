using NetOfd.Reader;
using System.Text;

namespace NetOfd.Converter;

/// <summary>
/// OFD → 纯文本 导出器（对应 Java ofdrw-converter 的 <c>TextExporter</c>）。
/// <para>
/// 注意：部分 OFD 使用字形索引定位文字、或整页均为路径 / 图片图元，
/// 这类文档无法提取到文本；另外受文本布局影响，导出顺序也不保证与视觉顺序一致。
/// </para>
/// <para>
/// 用法：
/// <code>
/// using (var exporter = new TextExporter("input.ofd", "out.txt"))
///     exporter.Export();
/// </code>
/// </para>
/// </summary>
public class TextExporter : IOfdExporter
{
    private readonly OFDReader _reader;
    private readonly bool _ownsReader;
    private readonly TextWriter _out;
    private readonly bool _ownsWriter;
    private bool _disposed;

    /// <summary>从文件路径创建，文本写入 <paramref name="txtPath"/>（父目录自动创建）。</summary>
    public TextExporter(string ofdPath, string txtPath)
    {
        if (string.IsNullOrWhiteSpace(txtPath))
            throw new ArgumentException("导出文本文件路径为空", nameof(txtPath));

        string full = Path.GetFullPath(txtPath);
        string? parent = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(parent))
        {
            if (File.Exists(parent))
                throw new ArgumentException($"已经存在同名文件: {parent}", nameof(txtPath));
            Directory.CreateDirectory(parent);
        }

        _reader = new OFDReader(ofdPath);
        _ownsReader = true;
        _out = new StreamWriter(File.Create(full), new UTF8Encoding(false));
        _ownsWriter = true;
    }

    /// <summary>从流创建（OFD 流与输出流均由调用方负责关闭）。</summary>
    public TextExporter(Stream ofdInput, Stream txtOutput)
    {
        if (txtOutput is null) throw new ArgumentNullException(nameof(txtOutput));
        _reader = new OFDReader(ofdInput);
        _ownsReader = true;
        _out = new StreamWriter(txtOutput, new UTF8Encoding(false), leaveOpen: true);
        _ownsWriter = true;
    }

    /// <summary>复用已有 <see cref="OFDReader"/> 与 <see cref="TextWriter"/>（均不接管生命周期）。</summary>
    public TextExporter(OFDReader reader, TextWriter output)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _out = output ?? throw new ArgumentNullException(nameof(output));
        _ownsReader = false;
        _ownsWriter = false;
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _reader.GetPageCount();

    /// <summary>
    /// 页与页之间是否插入一个空行分隔，默认 <c>false</c>（与 Java 行为一致：不插入）。
    /// </summary>
    public bool PageSeparator { get; set; }

    /// <inheritdoc/>
    public void Export(params int[] indexes)
    {
        var targets = ExportPageSelector.Normalize(indexes, _reader.GetPageCount());
        try
        {
            bool first = true;
            foreach (int index in targets)
            {
                if (PageSeparator && !first) _out.WriteLine();
                first = false;

                var page = _reader.GetPage(index);
                foreach (string line in ContentExtractor.ExtractPageTextItems(page))
                    _out.WriteLine(line);
            }
            _out.Flush();
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("文本导出异常", e);
        }
    }

    public void Close() => Dispose();

    public void Dispose()
    {
        if (!_disposed)
        {
            _disposed = true;
            if (_ownsWriter)
            {
                _out.Flush();
                _out.Dispose();
            }
            if (_ownsReader) _reader.Dispose();
        }
        GC.SuppressFinalize(this);
    }
}
