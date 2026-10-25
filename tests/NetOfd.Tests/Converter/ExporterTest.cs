using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NetOfd.Converter;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// OFD 导出器矩阵测试：SVG / 纯文本 / HTML，
/// 以及渲染层两处关键语义（PageBlock 递归、图层 Z 序、CTM 与 Boundary 的复合顺序）。
/// </summary>
public class ExporterTest
{
    // ── 公共辅助 ────────────────────────────────────────────
    private static string Dir(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_exporter_tests", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>用布局引擎造一个含文字的单页 OFD。</summary>
    private static string MakeTextOfd(string dir, params string[] lines)
    {
        string ofd = Path.Combine(dir, "src.ofd");
        using (var doc = new OFDDoc(ofd))
        {
            foreach (var line in lines)
                doc.Add(new Paragraph().AddText(line).SetFontSize(8d));
        }
        return ofd;
    }

    /// <summary>用布局引擎造一个两页 OFD。</summary>
    private static string MakeTwoPageOfd(string dir)
    {
        string ofd = Path.Combine(dir, "two.ofd");
        using (var doc = new OFDDoc(ofd))
        {
            doc.AddPage();
            doc.CreateDrawContext(new STBox(0, 0, 210, 297)).FillText("第一页文字", 10, 50);
            doc.AddPage();
            doc.CreateDrawContext(new STBox(0, 0, 210, 297)).FillText("第二页文字", 10, 50);
        }
        return ofd;
    }

    /// <summary>把 OFD 包内某个条目替换为新内容，写出到新文件。</summary>
    private static string RewriteEntry(string srcOfd, string dstOfd, string entry, string newXml)
    {
        var items = new List<(string Name, byte[] Data)>();
        using (var zip = ZipFile.OpenRead(srcOfd))
        {
            foreach (var e in zip.Entries)
            {
                using var s = e.Open();
                using var ms = new MemoryStream();
                s.CopyTo(ms);
                items.Add((e.FullName, ms.ToArray()));
            }
        }

        using var outFs = File.Create(dstOfd);
        using var outZip = new ZipArchive(outFs, ZipArchiveMode.Create);
        foreach (var (name, data) in items)
        {
            var ze = outZip.CreateEntry(name, CompressionLevel.Fastest);
            using var zs = ze.Open();
            byte[] payload = string.Equals(name, entry, StringComparison.OrdinalIgnoreCase)
                ? new UTF8Encoding(false).GetBytes(newXml)
                : data;
            zs.Write(payload, 0, payload.Length);
        }
        return dstOfd;
    }

    private const string PageHead =
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
        "<ofd:Page xmlns:ofd=\"http://www.ofdspec.org/2016\"><ofd:Content>";
    private const string PageTail = "</ofd:Content></ofd:Page>";

    private static SKBitmap RenderFirstPage(string ofdPath, double ppm)
    {
        using var maker = new OfdImageMaker(ofdPath);
        var bmp = maker.MakePage(0, new ConverterOptions { PPM = ppm, AntiAlias = false });
        Assert.NotNull(bmp);
        return bmp!;
    }

    private static bool IsRed(SKColor c) => c.Red > 200 && c.Green < 60 && c.Blue < 60;
    private static bool IsBlue(SKColor c) => c.Blue > 200 && c.Red < 60 && c.Green < 60;

    // ══════════════════════════════════════════════════════
    // 1. SVG 导出
    // ══════════════════════════════════════════════════════

    [Fact]
    public void SvgExporter_AllPages_ShouldProduceOneSvgEach()
    {
        var dir = Dir("svg_all");
        string ofd = MakeTwoPageOfd(dir);
        string outDir = Path.Combine(dir, "svg");

        using (var exporter = new SvgExporter(ofd, outDir, ppm: 4))
        {
            exporter.Export();
            var files = exporter.GetSvgFilePaths();

            Assert.Equal(2, files.Count);
            Assert.All(files, f => Assert.True(File.Exists(f)));
            Assert.Equal(Path.Combine(outDir, "0.svg"), files[0]);
            Assert.Equal(Path.Combine(outDir, "1.svg"), files[1]);

            string svg = File.ReadAllText(files[0]);
            Assert.Contains("<svg", svg);
            Assert.Contains("</svg>", svg);          // 结尾标签必须完整（画布释放后才写出）
            // A4：210mm × 4ppm = 840，297mm × 4ppm = 1188
            Assert.Contains("840", svg);
            Assert.Contains("1188", svg);
        }
    }

    [Fact]
    public void SvgExporter_SpecificPage_ShouldSkipOthers()
    {
        var dir = Dir("svg_one");
        string ofd = MakeTwoPageOfd(dir);
        string outDir = Path.Combine(dir, "svg");

        using var exporter = new SvgExporter(ofd, outDir, ppm: 3);
        exporter.Export(1);          // 只导出第 2 页

        var files = exporter.GetSvgFilePaths();
        Assert.Single(files);
        // 与 Java 一致：文件名按“已生成序号”而非原始页码
        Assert.Equal(Path.Combine(outDir, "0.svg"), files[0]);
    }

    [Fact]
    public void SvgExporter_OutOfRangeIndex_ShouldBeIgnored()
    {
        var dir = Dir("svg_oob");
        string ofd = MakeTextOfd(dir, "越界页码测试");
        using var exporter = new SvgExporter(ofd, Path.Combine(dir, "svg"));
        exporter.Export(-1, 0, 99);
        Assert.Single(exporter.GetSvgFilePaths());
    }

    [Fact]
    public void OfdSvgMaker_InvalidIndex_ShouldThrowConvertException()
    {
        var dir = Dir("svg_bad");
        string ofd = MakeTextOfd(dir, "abc");
        using var maker = new OfdSvgMaker(ofd);
        Assert.Throws<GeneralConvertException>(() => maker.MakePage(5));
    }

    // ══════════════════════════════════════════════════════
    // 2. 纯文本导出
    // ══════════════════════════════════════════════════════

    [Fact]
    public void TextExporter_ShouldExtractAllTextLines()
    {
        var dir = Dir("txt_all");
        string ofd = MakeTextOfd(dir, "第一行内容", "SecondLine", "第三行内容");
        string txt = Path.Combine(dir, "sub", "out.txt");   // 顺带验证父目录自动创建

        using (var exporter = new TextExporter(ofd, txt))
            exporter.Export();

        Assert.True(File.Exists(txt));
        string content = File.ReadAllText(txt);
        Assert.Contains("第一行内容", content);
        Assert.Contains("SecondLine", content);
        Assert.Contains("第三行内容", content);
    }

    [Fact]
    public void TextExporter_ToStream_ShouldRespectPageSelection()
    {
        var dir = Dir("txt_stream");
        string ofd = MakeTwoPageOfd(dir);

        using var ms = new MemoryStream();
        using (var ofdFs = File.OpenRead(ofd))
        using (var exporter = new TextExporter(ofdFs, ms))
        {
            exporter.PageSeparator = true;
            exporter.Export(1);
        }

        string text = Encoding.UTF8.GetString(ms.ToArray());
        Assert.Contains("第二页文字", text);
        Assert.DoesNotContain("第一页文字", text);
    }

    // ══════════════════════════════════════════════════════
    // 3. HTML 导出
    // ══════════════════════════════════════════════════════

    [Fact]
    public void HtmlExporter_ShouldEmbedSvgAndSelectableTextLayer()
    {
        var dir = Dir("html");
        string ofd = MakeTextOfd(dir, "HTML导出验证文字");
        string html = Path.Combine(dir, "out.html");

        using (var exporter = new HtmlExporter(ofd, html))
        {
            exporter.Title = "病历预览";
            exporter.Export();
        }

        string doc = File.ReadAllText(html);
        Assert.StartsWith("<!DOCTYPE html>", doc);
        Assert.EndsWith("</html>", doc);
        Assert.Contains("<title>病历预览</title>", doc);
        Assert.Contains("class=\"ofd-page\"", doc);
        Assert.Contains("<svg", doc);
        // 文本层：透明但可选中
        Assert.Contains("class=\"ofd-text\"", doc);
        Assert.Contains("color:transparent", doc);
        Assert.Contains("HTML导出验证文字", doc);
        // 页面宽度应等于 ScreenWidth 默认值 1000px
        Assert.Contains("width:1000px", doc);
    }

    [Fact]
    public void HtmlExporter_TextLayerDisabled_ShouldOnlyKeepSvg()
    {
        var dir = Dir("html_notext");
        string ofd = MakeTextOfd(dir, "仅矢量层");
        string html = Path.Combine(dir, "out.html");

        using (var exporter = new HtmlExporter(ofd, html))
        {
            exporter.TextLayer = false;
            exporter.Export();
        }

        string doc = File.ReadAllText(html);
        Assert.Contains("<svg", doc);
        Assert.DoesNotContain("class=\"ofd-text\"", doc);
    }

    [Fact]
    public void HtmlExporter_MultiPage_ShouldEmitOneDivPerPage()
    {
        var dir = Dir("html_multi");
        string ofd = MakeTwoPageOfd(dir);
        string html = Path.Combine(dir, "out.html");

        using (var exporter = new HtmlExporter(ofd, html))
            exporter.Export();

        string doc = File.ReadAllText(html);
        int count = doc.Split("class=\"ofd-page\"").Length - 1;
        Assert.Equal(2, count);
    }

    // ══════════════════════════════════════════════════════
    // 4. 渲染语义：PageBlock 递归
    // ══════════════════════════════════════════════════════

    [Fact]
    public void NestedPageBlock_ObjectsShouldBeRenderedAndExtracted()
    {
        var dir = Dir("pageblock");
        string src = MakeTextOfd(dir, "占位");

        // 手工构造：文字与红色方块埋在两层 PageBlock 内
        string page = PageHead +
            "<ofd:Layer Type=\"Body\" ID=\"2\">" +
              "<ofd:PageBlock ID=\"90\">" +
                "<ofd:PageBlock ID=\"91\">" +
                  "<ofd:TextObject Boundary=\"10 10 100 10\" Font=\"3\" Size=\"8\">" +
                    "<ofd:FillColor Value=\"0,0,0\"/>" +
                    "<ofd:TextCode X=\"0\" Y=\"8\">嵌套页块内的文字</ofd:TextCode>" +
                  "</ofd:TextObject>" +
                  "<ofd:PathObject Boundary=\"20 40 30 30\" Fill=\"true\" Stroke=\"false\">" +
                    "<ofd:FillColor Value=\"255,0,0\"/>" +
                    "<ofd:AbbreviatedData>S 0 0 L 30 0 L 30 30 L 0 30 C</ofd:AbbreviatedData>" +
                  "</ofd:PathObject>" +
                "</ofd:PageBlock>" +
              "</ofd:PageBlock>" +
            "</ofd:Layer>" + PageTail;

        string dst = RewriteEntry(src, Path.Combine(dir, "nested.ofd"),
            "Doc_0/Pages/Page_0/Content.xml", page);

        // 4.1 文本可提取
        string txt = Path.Combine(dir, "out.txt");
        using (var te = new TextExporter(dst, txt)) te.Export();
        Assert.Contains("嵌套页块内的文字", File.ReadAllText(txt));

        // 4.2 图形可渲染（ppm=1 → 1px = 1mm）
        using var bmp = RenderFirstPage(dst, ppm: 1);
        Assert.True(IsRed(bmp.GetPixel(30, 50)), "嵌套 PageBlock 内的红色方块应被绘制");
    }

    // ══════════════════════════════════════════════════════
    // 5. 渲染语义：图层 Z 序
    // ══════════════════════════════════════════════════════

    [Fact]
    public void LayerZOrder_BodyShouldPaintOverBackground_RegardlessOfDocumentOrder()
    {
        var dir = Dir("zorder");
        string src = MakeTextOfd(dir, "占位");

        // 文档顺序刻意颠倒：Body 在前、Background 在后。
        // 若按文档顺序绘制则蓝色覆盖红色；正确 Z 序应为 Background → Body，红色在上。
        string rect(string color) =>
            $"<ofd:PathObject Boundary=\"0 0 210 297\" Fill=\"true\" Stroke=\"false\">" +
            $"<ofd:FillColor Value=\"{color}\"/>" +
            "<ofd:AbbreviatedData>S 0 0 L 210 0 L 210 297 L 0 297 C</ofd:AbbreviatedData>" +
            "</ofd:PathObject>";

        string page = PageHead +
            "<ofd:Layer Type=\"Body\" ID=\"2\">" + rect("255,0,0") + "</ofd:Layer>" +
            "<ofd:Layer Type=\"Background\" ID=\"3\">" + rect("0,0,255") + "</ofd:Layer>" +
            PageTail;

        string dst = RewriteEntry(src, Path.Combine(dir, "z.ofd"),
            "Doc_0/Pages/Page_0/Content.xml", page);

        using var bmp = RenderFirstPage(dst, ppm: 1);
        var px = bmp.GetPixel(100, 100);
        Assert.True(IsRed(px), $"Body 层应覆盖 Background 层，实际像素 {px}");
        Assert.False(IsBlue(px));
    }

    // ══════════════════════════════════════════════════════
    // 6. 渲染语义：CTM 与 Boundary 的复合顺序
    // ══════════════════════════════════════════════════════

    [Fact]
    public void CtmAndBoundary_TranslationMustNotBeScaledByCtm()
    {
        var dir = Dir("ctm");
        string src = MakeTextOfd(dir, "占位");

        // 单位正方形 + CTM 放大 10 倍 + Boundary 平移 (10,10)
        //   正确（先 CTM 后平移）：占据 (10,10)-(20,20) mm
        //   错误（先平移后 CTM）：占据 (100,100)-(110,110) mm —— 平移量被 CTM 二次放大
        string page = PageHead +
            "<ofd:Layer Type=\"Body\" ID=\"2\">" +
              "<ofd:PathObject Boundary=\"10 10 100 100\" CTM=\"10 0 0 10 0 0\" Fill=\"true\" Stroke=\"false\">" +
                "<ofd:FillColor Value=\"255,0,0\"/>" +
                "<ofd:AbbreviatedData>S 0 0 L 1 0 L 1 1 L 0 1 C</ofd:AbbreviatedData>" +
              "</ofd:PathObject>" +
            "</ofd:Layer>" + PageTail;

        string dst = RewriteEntry(src, Path.Combine(dir, "ctm.ofd"),
            "Doc_0/Pages/Page_0/Content.xml", page);

        using var bmp = RenderFirstPage(dst, ppm: 1);
        Assert.True(IsRed(bmp.GetPixel(15, 15)),
            "CTM 缩放后的方块应落在 (10,10)-(20,20)mm");
        Assert.False(IsRed(bmp.GetPixel(105, 105)),
            "若平移被 CTM 二次放大则会错误地落在 (100,100)-(110,110)mm");
    }
}
