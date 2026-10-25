using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Converter;
using NetOfd.Layout;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// Image→OFD 转换器测试。
/// 对齐 Java: org.ofdrw.converter.ofdconverter.ImageConverter
/// </summary>
public class ImageConverterTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_imgconv_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>生成指定像素尺寸的 PNG 测试图片（纯色）。</summary>
    private static string MakePng(int w, int h, out string path)
    {
        path = Path.Combine(OutDir(), $"img_{w}x{h}_{Guid.NewGuid():N}.png");
        using var bmp = new SkiaSharp.SKBitmap(w, h);
        using (var canvas = new SkiaSharp.SKCanvas(bmp))
        {
            canvas.Clear(SkiaSharp.SKColors.Red);
        }
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        using var fs = File.Create(path);
        data.SaveTo(fs);
        return path;
    }

    private static XDocument PageContent(string ofd, int index)
    {
        using var za = ZipFile.OpenRead(ofd);
        var name = $"Doc_0/Pages/Page_{index}/Content.xml";
        var entry = za.GetEntry(name);
        Assert.NotNull(entry);
        using var s = entry!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>默认 A4 页 + ppm=15：300px → 20mm，居中在 (95, 138.5)。</summary>
    [Fact]
    public void Convert_CentersImageOnPage()
    {
        var ofdPath = Path.Combine(OutDir(), "img_convert.ofd");
        MakePng(300, 300, out var png);
        using (var conv = new ImageConverter(ofdPath))
        {
            conv.Convert(png);
        }

        var content = PageContent(ofdPath, 0);
        var imgObj = content.Descendants().First(e => e.Name.LocalName == "ImageObject");
        var bx = ((string?)imgObj.Attribute("Boundary"))!.Split(' ').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // 宽 20、高 20，居中：X=95, Y=138.5
        Assert.Equal(95, bx[0], 1);
        Assert.Equal(138.5, bx[1], 1);
        Assert.Equal(20, bx[2], 1);
        Assert.Equal(20, bx[3], 1);
    }

    /// <summary>append 使用显式宽高放置图片（仍在页面居中）。</summary>
    [Fact]
    public void Append_UsesExplicitSize()
    {
        var ofdPath = Path.Combine(OutDir(), "img_append.ofd");
        MakePng(1000, 1000, out var png);
        using (var conv = new ImageConverter(ofdPath))
        {
            conv.Append(png, 40, 30);
        }

        var content = PageContent(ofdPath, 0);
        var imgObj = content.Descendants().First(e => e.Name.LocalName == "ImageObject");
        var bx = ((string?)imgObj.Attribute("Boundary"))!.Split(' ').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // 宽 40、高 30，居中：X=(210-40)/2=85, Y=(297-30)/2=133.5
        Assert.Equal(85, bx[0], 1);
        Assert.Equal(133.5, bx[1], 1);
        Assert.Equal(40, bx[2], 1);
        Assert.Equal(30, bx[3], 1);
    }

    /// <summary>图片超出页面高度时等比缩放至适应页面（保持宽高比）。</summary>
    [Fact]
    public void Convert_ScalesOversizedImageToFitPage()
    {
        var ofdPath = Path.Combine(OutDir(), "img_scale.ofd");
        // 9000x9000 px，ppm=15 → 600mm 宽/高，远超 A4
        MakePng(9000, 9000, out var png);
        using (var conv = new ImageConverter(ofdPath))
        {
            conv.Convert(png);
        }

        var content = PageContent(ofdPath, 0);
        var imgObj = content.Descendants().First(e => e.Name.LocalName == "ImageObject");
        var bx = ((string?)imgObj.Attribute("Boundary"))!.Split(' ').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // 等比缩放至 height=297：width=297。居中 → X=(210-297)/2=-43.5, Y=0
        Assert.Equal(297, bx[3], 1);
        Assert.InRange(bx[0], -45, -42);
    }

    /// <summary>缺失文件 / 目录不做转换（跳过，不新增页面）。</summary>
    [Fact]
    public void Convert_NullOrMissingFile_SkipsNoPageAdded()
    {
        var ofdPath = Path.Combine(OutDir(), "img_none.ofd");
        MakePng(200, 200, out var png);
        using (var conv = new ImageConverter(ofdPath))
        {
            conv.Append(png, 50, 50);
            // 以下均不产生页面
            conv.Convert(Path.Combine(OutDir(), "no_such_file.png"));
            conv.Convert(@"C:\Windows"); // 目录
        }
        // 仅应有 Append 产生的 1 页
        Assert.Single(PageContent(ofdPath, 0).Descendants().Where(e => e.Name.LocalName == "ImageObject"));
        using var za = ZipFile.OpenRead(ofdPath);
        Assert.DoesNotContain(za.Entries, e => e.FullName.Contains("/Pages/Page_1/", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>多页：重复 convert / append 每次产生独立页面。</summary>
    [Fact]
    public void Convert_MultipleImages_ProduceMultiplePages()
    {
        var ofdPath = Path.Combine(OutDir(), "img_multi.ofd");
        MakePng(200, 200, out var png1);
        MakePng(300, 300, out var png2);
        using (var conv = new ImageConverter(ofdPath))
        {
            conv.Convert(png1);
            conv.Append(png2, 50, 50);
        }
        var content0 = PageContent(ofdPath, 0);
        var content1 = PageContent(ofdPath, 1);
        Assert.Single(content0.Descendants().Where(e => e.Name.LocalName == "ImageObject"));
        Assert.Single(content1.Descendants().Where(e => e.Name.LocalName == "ImageObject"));
    }
}
