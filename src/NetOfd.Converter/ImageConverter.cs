using System;
using System.IO;
using NetOfd.Layout;
using NetOfd.Layout.Element;

namespace NetOfd.Converter;

/// <summary>
/// 图片转 OFD 转换器。
/// <para>
/// 将图片（png/jpg/jpeg/gif/bmp）转换为 OFD 文档，每张图片放置到独立的一页并居中，
/// 超出页面时会等比缩放以适应页面。
/// </para>
/// 对应 Java: org.ofdrw.converter.ofdconverter.ImageConverter（实现 DocConverter）
/// </summary>
public class ImageConverter : IDisposable
{
    private readonly OFDDoc _ofdDoc;
    private bool _closed;

    /// <summary>每毫米像素数量，默认 15。</summary>
    private double _ppm = 15;

    /// <summary>创建 Image→OFD 转换器（输出到文件路径）。</summary>
    /// <param name="ofdPath">转换后的 OFD 文件路径</param>
    public ImageConverter(string ofdPath)
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

    /// <summary>创建 Image→OFD 转换器（输出到流）。</summary>
    /// <param name="output">转换后的 OFD 流</param>
    public ImageConverter(Stream output)
    {
        if (output is null)
            throw new ArgumentException("转换后的OFD流为空");
        _ofdDoc = new OFDDoc(output);
    }

    /// <summary>
    /// 新建 OFD 页面并放入图片（图片居中，超出页面时等比缩放以适应）。
    /// </summary>
    /// <param name="filepath">待转换图片路径</param>
    /// <param name="indexes">忽略（图片非文档类文件，页码参数无效）</param>
    public void Convert(string filepath, params int[] indexes)
    {
        if (string.IsNullOrEmpty(filepath) || !File.Exists(filepath) || IsDirectory(filepath))
        {
            return;
        }

        var pageLayout = _ofdDoc.GetPageLayout();
        double pageWidth = pageLayout.Width;
        double pageHeight = pageLayout.Height;

        double width = 50;
        double height = 50;
        try
        {
            var bitmap = ReadImage(filepath);
            if (bitmap is not null)
            {
                // 图片可解析时分析大小（像素 → mm）
                width = bitmap.Width / _ppm;
                height = bitmap.Height / _ppm;
                double scale = 1;
                if (width > pageWidth)
                {
                    scale = pageWidth / width;
                }
                if (height > pageHeight)
                {
                    var s = pageHeight / height;
                    if (s > scale) scale = s;
                }
                width *= scale;
                height *= scale;
            }

            var page = new VirtualPage(pageLayout);

            var item = new Img(width, height, filepath);
            item.SetPosition(Position.Absolute);
            double x = (pageWidth - width) / 2;
            double y = (pageHeight - height) / 2;
            item.SetXY(x, y);
            page.Add(item);
            _ofdDoc.AddVPage(page);
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("图片转换OFD异常", e);
        }
    }

    /// <summary>
    /// 追加图片到新一页（图片在页面中宽度/高度由调用者指定，居中）。
    /// </summary>
    /// <param name="filepath">图片文件</param>
    /// <param name="width">图片在页面中宽度（mm）</param>
    /// <param name="height">图片在页面中高度（mm）</param>
    public void Append(string filepath, double width, double height)
    {
        try
        {
            var pageLayout = _ofdDoc.GetPageLayout();
            double pageWidth = pageLayout.Width;
            double pageHeight = pageLayout.Height;

            var page = new VirtualPage(pageLayout);

            var item = new Img(width, height, filepath);
            item.SetPosition(Position.Absolute);
            double x = (pageWidth - width) / 2;
            double y = (pageHeight - height) / 2;
            item.SetXY(x, y);
            page.Add(item);
            _ofdDoc.AddVPage(page);
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("图片转换OFD异常", e);
        }
    }

    /// <summary>设置图片质量（像素每毫米）。</summary>
    /// <param name="ppm">像素每毫米，必须大于 0</param>
    public void SetPPM(double ppm)
    {
        if (ppm <= 0)
            throw new ArgumentException("PPM不能小于0", nameof(ppm));
        _ppm = ppm;
    }

    /// <summary>设置页面尺寸。</summary>
    public void SetPageSize(PageLayout pageLayout)
    {
        _ofdDoc.SetDefaultPageLayout(pageLayout);
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

    /// <summary>读取图片像素尺寸（无法解析时返回 null）。</summary>
    private static SkiaSharp.SKBitmap? ReadImage(string filepath)
    {
        try
        {
            return SkiaSharp.SKBitmap.Decode(filepath);
        }
        catch
        {
            return null;
        }
    }
}
