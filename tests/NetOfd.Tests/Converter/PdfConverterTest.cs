using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Converter;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// PDF→OFD 矢量转换器测试。
/// 对齐 Java: org.ofdrw.converter.ofdconverter.PDFConverter
/// </summary>
public class PdfConverterTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_pdfconv_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>构造一个包含文字、填充矩形、直线、圆形的 PDF（指定页数，其余页为空白）。</summary>
    private static string CreatePdf(int pages)
    {
        var path = Path.Combine(OutDir(), $"src_{pages}.pdf");
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(UglyToad.PdfPig.Fonts.Standard14Fonts.Standard14Font.Courier);
        for (int i = 0; i < pages; i++)
        {
            var page = builder.AddPage(200, 300);
            if (i == 0)
            {
                page.AddText($"Hello PDF Page {i + 1}", 12, new PdfPoint(20, 250), font);
                page.SetTextAndFillColor(0, 255, 0);
                page.DrawRectangle(new PdfPoint(30, 30), 50, 40, 10, true);
                page.DrawLine(new PdfPoint(10, 10), new PdfPoint(150, 60), 4);
            }
        }
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    /// <summary>生成 PNG 字节（SkiaSharp）。</summary>
    private static byte[] MakePng(int w, int h)
    {
        using var bmp = new SkiaSharp.SKBitmap(w, h);
        using (var canvas = new SkiaSharp.SKCanvas(bmp))
        {
            canvas.Clear(SkiaSharp.SKColors.Red);
        }
        using var img = SkiaSharp.SKImage.FromBitmap(bmp);
        using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }

    /// <summary>构造一个第一页内嵌 PNG 图片的 PDF。</summary>
    private static string CreatePdfWithImage()
    {
        var path = Path.Combine(OutDir(), "src_img.pdf");
        using var builder = new PdfDocumentBuilder();
        var png = MakePng(40, 30);
        var page = builder.AddPage(200, 300);
        page.AddPng(png, new PdfRectangle(50, 100, 120, 150));
        File.WriteAllBytes(path, builder.Build());
        return path;
    }

    /// <summary>读取 OFD 中所有页面 Content.xml 的路径元素列表。</summary>
    private static XDocument[] ReadContents(string ofd)
    {
        using var za = ZipFile.OpenRead(ofd);
        return za.Entries
            .Where(e => e.FullName.Contains("/Pages/", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith("/Content.xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .Select(e =>
            {
                using var s = e.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                return XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
            })
            .ToArray();
    }

    /// <summary>文字被转换为 TextObject，内容与字号/颜色被写入。</summary>
    [Fact]
    public void Convert_TextLettersBecomeTextObjects()
    {
        var pdf = CreatePdf(1);
        var ofdPath = Path.Combine(OutDir(), "pdf_text.ofd");
        using (var conv = new PdfConverter(ofdPath))
        {
            conv.Convert(pdf);
        }

        var contents = ReadContents(ofdPath);
        Assert.Single(contents);
        var textObjs = contents[0].Descendants().Where(e => e.Name.LocalName == "TextObject").ToList();
        // “Hello PDF Page 1” 每个字母独立成对象（空格跳过）
        var merged = new StringBuilder();
        foreach (var to in textObjs)
            foreach (var tc in to.Descendants().Where(e => e.Name.LocalName == "TextCode"))
                merged.Append(tc.Value);
        Assert.Contains("Hello", merged.ToString());
        Assert.Contains("Page", merged.ToString());
        Assert.Equal(13, textObjs.Count); // "HelloPDFPage1" = 13 字符（去空格）
    }

    /// <summary>矢量路径被转换为 PathObject（含填充/描边）。</summary>
    [Fact]
    public void Convert_PathsBecomePathObjects()
    {
        var pdf = CreatePdf(1);
        var ofdPath = Path.Combine(OutDir(), "pdf_path.ofd");
        using (var conv = new PdfConverter(ofdPath))
        {
            conv.Convert(pdf);
        }

        var contents = ReadContents(ofdPath);
        var pathObjs = contents[0].Descendants().Where(e => e.Name.LocalName == "PathObject").ToList();
        Assert.NotEmpty(pathObjs);
        // 填充矩形 + 直线 → 至少两个 PathObject
        Assert.True(pathObjs.Count >= 2, $"期望至少 2 个 PathObject，实际 {pathObjs.Count}");
    }

    /// <summary>空参（无 indexes）转换全部页面；指定 page index 仅转换对应页。</summary>
    [Fact]
    public void Convert_PageSelection_Works()
    {
        var pdf3 = CreatePdf(3);
        var allPath = Path.Combine(OutDir(), "pdf_all.ofd");
        using (var conv = new PdfConverter(allPath)) conv.Convert(pdf3);
        Assert.Equal(3, ReadContents(allPath).Length);

        var selPath = Path.Combine(OutDir(), "pdf_sel.ofd");
        using (var conv2 = new PdfConverter(selPath)) conv2.Convert(pdf3, 2);
        Assert.Single(ReadContents(selPath));
    }

    /// <summary>坐标按 pt→mm 换算（PDF 原点在左下）：页面宽 200pt，"H" 起点 x=20pt → 7.06mm。</summary>
    [Fact]
    public void Convert_CoordinatesConvertPtToMm()
    {
        var pdf = CreatePdf(1);
        var ofdPath = Path.Combine(OutDir(), "pdf_coord.ofd");
        using (var conv = new PdfConverter(ofdPath)) conv.Convert(pdf);

        var contents = ReadContents(ofdPath);
        var firstText = contents[0].Descendants()
            .First(e => e.Name.LocalName == "TextObject")
            .Descendants().First(e => e.Name.LocalName == "TextCode");
        // PdfPig 以自下而上坐标系报告，页面高度 300pt → y=250pt 为基线（此处仅校验 X）。
        var x = double.Parse(firstText.Attribute("X")!.Value, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(20.0 / (72.0 / 25.4), x, 2); // ≈ 7.06mm
    }

    /// <summary>页面区域（每页 PhysicalBox）按 PDF 页 pt→mm 换算（200×300pt → 70.6×105.8mm）。</summary>
    [Fact]
    public void Convert_PagePhysicalBoxMatchesPdf()
    {
        var pdf = CreatePdf(1);
        var ofdPath = Path.Combine(OutDir(), "pdf_pgsize.ofd");
        using (var conv = new PdfConverter(ofdPath)) conv.Convert(pdf);

        // GB/T 33190：Content.xml 根元素 <Page> 的页面区域子元素名为 <Area>
        // （CommonData 里的文档级默认值才叫 <PageArea>），此处校验每页 Area/PhysicalBox。
        var contents = ReadContents(ofdPath);
        var area = contents[0].Descendants().First(e => e.Name.LocalName == "Area")
            .Descendants().First(e => e.Name.LocalName == "PhysicalBox").Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(200.0 / (72.0 / 25.4), area[2], 1); // ≈ 70.56mm
        Assert.Equal(300.0 / (72.0 / 25.4), area[3], 1); // ≈ 105.83mm
    }

    /// <summary>嵌入图片被转换为 ImageObject（位置/尺寸按 pt→mm 换算）。</summary>
    [Fact]
    public void Convert_EmbeddedImageBecomesImageObject()
    {
        var pdf = CreatePdfWithImage();
        var ofdPath = Path.Combine(OutDir(), "pdf_img.ofd");
        using (var conv = new PdfConverter(ofdPath)) conv.Convert(pdf);

        var contents = ReadContents(ofdPath);
        var imgObj = contents[0].Descendants().First(e => e.Name.LocalName == "ImageObject");
        var bx = imgObj.Attribute("Boundary")!.Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // 图片 PDF 范围 (50,100)-(120,150)：x=Left/uuPmm≈17.64, y=(300-Top)/uuPmm≈52.92, w≈24.69, h≈17.64
        var uu = 72.0 / 25.4;
        Assert.Equal(50 / uu, bx[0], 1);
        Assert.Equal((300 - 150) / uu, bx[1], 1);
        Assert.Equal(70 / uu, bx[2], 1);
        Assert.Equal(50 / uu, bx[3], 1);
        // 图片资源已被嵌入（ImageObject 引用 ResourceID）
        Assert.NotNull(imgObj.Attribute("ResourceID"));
    }

    /// <summary>缺失文件跳过（不新增页面，不抛出）。</summary>
    [Fact]
    public void Convert_MissingFile_Skips()
    {
        var pdf = CreatePdf(1);
        var ofdPath = Path.Combine(OutDir(), "pdf_missing.ofd");
        using (var conv = new PdfConverter(ofdPath))
        {
            conv.Convert(pdf); // 先转换一个有效页面，避免关闭 0 页文档
            conv.Convert(Path.Combine(OutDir(), "no_such.pdf"));
        }
        Assert.Single(ReadContents(ofdPath));
    }

    /// <summary>流构造器输出到 MemoryStream。</summary>
    [Fact]
    public void Convert_ToStream_ProducesOfd()
    {
        var pdf = CreatePdf(1);
        using var ms = new MemoryStream();
        using (var conv = new PdfConverter(ms)) conv.Convert(pdf);

        Assert.True(ms.Length > 0);
        ms.Position = 0;
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.NotEmpty(za.Entries);
    }

    /// <summary>转换产物可被库自带 OFDReader 回读：页数正确且文字可提取。</summary>
    [Fact]
    public void ConvertedOfd_CanBeReadByOfdReader()
    {
        var pdf = CreatePdf(1);
        var ofdPath = Path.Combine(OutDir(), "pdf_roundtrip.ofd");
        using (var conv = new PdfConverter(ofdPath)) conv.Convert(pdf);

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        var pages = reader.GetPages().Where(p => p is not null).ToList();
        Assert.Single(pages);
        var texts = NetOfd.Reader.ContentExtractor.ExtractAllPagesText(pages);
        Assert.Contains(texts, t => t != null && t.Contains("Hello"));
    }

    /// <summary>0 页 PDF：转换后关闭不应抛出，也不应生成无效（0 页）OFD 文件。</summary>
    [Fact]
    public void Convert_EmptyPdf_ClosesWithoutThrowing()
    {
        var tmp = Path.Combine(OutDir(), "empty.pdf");
        using (var b = new PdfDocumentBuilder()) File.WriteAllBytes(tmp, b.Build());

        var ofdPath = Path.Combine(OutDir(), "pdf_empty.ofd");
        File.Delete(ofdPath);
        using (var conv = new PdfConverter(ofdPath)) conv.Convert(tmp);

        Assert.False(File.Exists(ofdPath), "0 页转换不应生成 OFD 文件");
    }

    /// <summary>真实 PDF（Java ofdrw 官方转换器测试资源）回归：转换不抛、产物可被 Reader 回读且页数正确。</summary>
    [Fact]
    public void Convert_RealPdf_ProducesReadableOfd()
    {
        var pdf = Path.Combine(AppContext.BaseDirectory, "Resources", "pdf_real_ofdrw_test3.pdf");
        Assert.True(File.Exists(pdf), "真实 PDF 资源应存在");

        var ofdPath = Path.Combine(OutDir(), "pdf_real.ofd");
        File.Delete(ofdPath);
        using (var conv = new PdfConverter(ofdPath)) conv.Convert(pdf);
        Assert.True(File.Exists(ofdPath));

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        var pages = reader.GetPages().Where(p => p is not null).ToList();
        Assert.Equal(4, pages.Count);
    }
}
