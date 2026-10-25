using System;
using System.IO;
using System.Linq;
using System.Text;
using NetOfd.Converter;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// ConvertHelper 转换门面测试：验证 OFD → PDF / HTML / 文本 / 图像 / SVG 的一行式入口，
/// 以及 PdfExporter 新增的流输出能力（输出流不被导出器关闭）。
/// 对应 Java: org.ofdrw.converter.ConvertHelper
/// </summary>
public class ConvertHelperTest
{
    private static string Dir(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_converthelper_tests", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string MakeOfd(string dir, params string[] lines)
    {
        string ofd = Path.Combine(dir, "src.ofd");
        using (var doc = new OFDDoc(ofd))
        {
            foreach (var line in lines)
                doc.Add(new Paragraph().AddText(line).SetFontSize(8d));
        }
        return ofd;
    }

    private static string MakeTwoPageOfd(string dir)
    {
        string ofd = Path.Combine(dir, "two.ofd");
        using (var doc = new OFDDoc(ofd))
        {
            doc.AddPage();
            doc.CreateDrawContext(new global::NetOfd.Core.BasicType.STBox(0, 0, 210, 297))
               .FillText("第一页文字", 10, 50);
            doc.AddPage();
            doc.CreateDrawContext(new global::NetOfd.Core.BasicType.STBox(0, 0, 210, 297))
               .FillText("第二页文字", 10, 50);
        }
        return ofd;
    }

    private static bool IsPdf(byte[] bytes) =>
        bytes.Length > 5 && bytes[0] == (byte)'%' && bytes[1] == (byte)'P'
        && bytes[2] == (byte)'D' && bytes[3] == (byte)'F' && bytes[4] == (byte)'-';

    // ────────────────────────────────────────────────────────
    //  PDF
    // ────────────────────────────────────────────────────────

    [Fact]
    public void ToPdf_PathToPath_ProducesValidPdf()
    {
        var dir = Dir("pdf_path_path");
        string ofd = MakeOfd(dir, "门面转换验证");
        string pdf = Path.Combine(dir, "out.pdf");

        ConvertHelper.ToPdf(ofd, pdf);

        Assert.True(File.Exists(pdf), "PDF 文件应生成");
        Assert.True(IsPdf(File.ReadAllBytes(pdf)), "输出应为 PDF（%PDF- 头）");
    }

    [Fact]
    public void ToPdfBytes_ReturnsPdfBytes()
    {
        var dir = Dir("pdf_bytes");
        string ofd = MakeOfd(dir, "字节数组输出验证");

        byte[] bytes = ConvertHelper.ToPdfBytes(ofd);

        Assert.True(IsPdf(bytes), "字节数组应为 PDF");
        Assert.True(bytes.Length > 500, $"PDF 体积异常偏小: {bytes.Length}");
    }

    [Fact]
    public void ToPdf_PathToStream_WritesIntoCallerStream()
    {
        var dir = Dir("pdf_path_stream");
        string ofd = MakeOfd(dir, "流输出验证");

        using var ms = new MemoryStream();
        ConvertHelper.ToPdf(ofd, ms);

        Assert.True(IsPdf(ms.ToArray()), "流内容应为 PDF");
    }

    [Fact]
    public void ToPdf_StreamToPath_ProducesValidPdf()
    {
        var dir = Dir("pdf_stream_path");
        string ofd = MakeOfd(dir, "流输入验证");
        string pdf = Path.Combine(dir, "out.pdf");

        using var input = File.OpenRead(ofd);
        ConvertHelper.ToPdf(input, pdf);

        Assert.True(IsPdf(File.ReadAllBytes(pdf)), "输出应为 PDF");
    }

    [Fact]
    public void ToPdf_StreamToStream_BothStreamsWork()
    {
        var dir = Dir("pdf_stream_stream");
        string ofd = MakeOfd(dir, "双流验证");

        using var input = File.OpenRead(ofd);
        using var output = new MemoryStream();
        ConvertHelper.ToPdf(input, output);

        Assert.True(IsPdf(output.ToArray()), "输出流内容应为 PDF");
    }

    [Fact]
    public void ToPdf_SelectedPages_StillProducesValidPdf()
    {
        var dir = Dir("pdf_selected");
        string ofd = MakeTwoPageOfd(dir);
        string pdf = Path.Combine(dir, "first.pdf");

        // 仅导出第 0 页
        ConvertHelper.ToPdf(ofd, pdf, 0);

        Assert.True(IsPdf(File.ReadAllBytes(pdf)), "按页码导出仍应生成合法 PDF");
    }

    [Fact]
    public void PdfExporter_StreamOutput_DoesNotCloseCallerStream()
    {
        var dir = Dir("pdf_stream_not_closed");
        string ofd = MakeOfd(dir, "流生命周期验证");

        using var ms = new MemoryStream();
        string? filePath;
        using (var exporter = new PdfExporter(ofd, ms))
        {
            exporter.Export();
            filePath = exporter.GetPdfFilePath();
        }

        // 约定：外部传入的流由调用方关闭，导出器不得代为关闭
        Assert.True(ms.CanWrite, "导出器不应关闭调用方提供的流");
        Assert.True(ms.Length > 0, "流中应有 PDF 数据");
        Assert.Null(filePath);   // 输出到流时无文件路径
    }

    [Fact]
    public void PdfExporter_PathOutput_ExposesFilePath()
    {
        var dir = Dir("pdf_path_expose");
        string ofd = MakeOfd(dir, "路径暴露验证");
        string pdf = Path.Combine(dir, "out.pdf");

        using var exporter = new PdfExporter(ofd, pdf);
        exporter.Export();

        Assert.Equal(Path.GetFullPath(pdf), exporter.GetPdfFilePath());
    }

    // ────────────────────────────────────────────────────────
    //  HTML / 文本
    // ────────────────────────────────────────────────────────

    [Fact]
    public void ToHtml_PathToPath_ProducesHtml()
    {
        var dir = Dir("html_path");
        string ofd = MakeOfd(dir, "HTML导出验证");
        string html = Path.Combine(dir, "out.html");

        ConvertHelper.ToHtml(ofd, html);

        Assert.True(File.Exists(html), "HTML 文件应生成");
        var content = File.ReadAllText(html, Encoding.UTF8);
        Assert.Contains("html", content.ToLowerInvariant());
    }

    [Fact]
    public void ToHtml_StreamToStream_WritesIntoCallerStream()
    {
        var dir = Dir("html_stream");
        string ofd = MakeOfd(dir, "HTML流验证");

        using var input = File.OpenRead(ofd);
        using var output = new MemoryStream();
        ConvertHelper.ToHtml(input, output);

        var text = Encoding.UTF8.GetString(output.ToArray());
        Assert.Contains("html", text.ToLowerInvariant());
    }

    [Fact]
    public void ToText_PathToPath_ProducesNonEmptyText()
    {
        var dir = Dir("text_path");
        string ofd = MakeOfd(dir, "文本导出验证");
        string txt = Path.Combine(dir, "out.txt");

        ConvertHelper.ToText(ofd, txt);

        Assert.True(File.Exists(txt), "文本 文件应生成");
        Assert.True(new FileInfo(txt).Length > 0, "文本文件不应为空");
    }

    [Fact]
    public void ToText_StreamToStream_WritesIntoCallerStream()
    {
        var dir = Dir("text_stream");
        string ofd = MakeOfd(dir, "文本流验证");

        using var input = File.OpenRead(ofd);
        using var output = new MemoryStream();
        ConvertHelper.ToText(input, output);

        Assert.True(output.Length > 0, "输出流不应为空");
    }

    // ────────────────────────────────────────────────────────
    //  图像 / SVG
    // ────────────────────────────────────────────────────────

    [Fact]
    public void ToImage_ReturnsGeneratedImageFiles()
    {
        var dir = Dir("image_out");
        string ofd = MakeOfd(dir, "图像导出验证");
        string outDir = Path.Combine(dir, "imgs");

        var files = ConvertHelper.ToImage(ofd, outDir, "png", 10);

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.True(File.Exists(f), $"图像文件应存在: {f}"));
        Assert.All(files, f => Assert.EndsWith(".png", f));
    }

    [Fact]
    public void ToSvg_ReturnsGeneratedSvgFiles()
    {
        var dir = Dir("svg_out");
        string ofd = MakeOfd(dir, "SVG导出验证");
        string outDir = Path.Combine(dir, "svgs");

        var files = ConvertHelper.ToSvg(ofd, outDir, 10);

        Assert.NotEmpty(files);
        Assert.All(files, f => Assert.True(File.Exists(f), $"SVG 文件应存在: {f}"));
        Assert.All(files, f => Assert.Contains("<svg", File.ReadAllText(f)));
    }

    [Fact]
    public void ToImage_TwoPages_ProducesOneFilePerPage()
    {
        var dir = Dir("image_two");
        string ofd = MakeTwoPageOfd(dir);
        string outDir = Path.Combine(dir, "imgs2");

        var files = ConvertHelper.ToImage(ofd, outDir, "png", 8);

        Assert.Equal(2, files.Count);
    }
}
