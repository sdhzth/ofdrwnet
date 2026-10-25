using System;
using System.IO;
using System.Text;
using NetOfd.Layout;
using NetOfd.Layout.Element;

namespace NetOfd.Converter;

/// <summary>
/// 文本转 OFD 转换器。
/// <para>
/// 将纯文本文件转换为 OFD 文档，每个文本行追加为一个段落（自动分页）。
/// </para>
/// 对应 Java: org.ofdrw.converter.ofdconverter.TextConverter（实现 DocConverter）
/// </summary>
public class TextConverter : IDisposable
{
    private readonly OFDDoc _ofdDoc;
    private bool _closed;

    /// <summary>字号（pt，1pt ≈ 0.353mm）。默认 3。</summary>
    private double _fontSize = 3;

    /// <summary>创建 Text→OFD 转换器（输出到文件路径）。</summary>
    /// <param name="ofdPath">转换后的 OFD 文件路径</param>
    public TextConverter(string ofdPath)
    {
        if (string.IsNullOrEmpty(ofdPath))
            throw new ArgumentException("转换后的OFD文件路径为空", nameof(ofdPath));

        var full = Path.GetFullPath(ofdPath);
        if (!File.Exists(full))
        {
            var parent = Path.GetDirectoryName(full);
            if (Directory.Exists(parent))
            {
                if (!IsDirectory(parent))
                    throw new ArgumentException($"已经存在同名文件: {parent}");
            }
            else
            {
                Directory.CreateDirectory(parent!);
            }
        }
        _ofdDoc = new OFDDoc(full);
    }

    private static bool IsDirectory(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Directory) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>创建 Text→OFD 转换器（输出到流）。</summary>
    /// <param name="output">转换后的 OFD 流，由调用者负责关闭</param>
    public TextConverter(Stream output)
    {
        if (output is null)
            throw new ArgumentException("转换后的OFD流为空");
        _ofdDoc = new OFDDoc(output);
    }

    /// <summary>
    /// 向 OFD 页面中追加文本（每行一个段落）。
    /// </summary>
    /// <param name="filepath">待转换文本文件路径</param>
    /// <param name="indexes">忽略（文本非文档类文件，页码参数无效）</param>
    public void Convert(string filepath, params int[] indexes)
    {
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath) || IsDirectory(filepath))
        {
            return;
        }
        try
        {
            // 对齐 Java：逐行追加，空行也会生成（空）段落以保留行间距。
            foreach (var line in File.ReadLines(filepath, Encoding.UTF8))
            {
                Append(line);
            }
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("文本转换OFD异常", e);
        }
    }

    /// <summary>追加文本，文本将新起一行。</summary>
    /// <param name="txt">文本</param>
    public void Append(string txt)
    {
        var p = new Paragraph(txt).SetFontSize(_fontSize);
        _ofdDoc.Add(p);
    }

    /// <summary>设置页面尺寸。</summary>
    public void SetPageSize(PageLayout pageLayout)
    {
        _ofdDoc.SetDefaultPageLayout(pageLayout);
    }

    /// <summary>设置字号（pt，1pt ≈ 0.353mm）。非正值回落为 3。</summary>
    public void SetFontSize(double fontSize)
    {
        _fontSize = fontSize <= 0 ? 3 : fontSize;
    }

    /// <summary>关闭文档并释放资源（幂等）。</summary>
    public void Close()
    {
        if (_closed) return;
        _closed = true;
        _ofdDoc.Close();
    }

    public void Dispose()
    {
        Close();
    }
}
