using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NetOfd.Converter;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// OFD → PDF 导出器测试。
/// </summary>
public class PdfExporterTest
{
    private static string Dir(string name)
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_pdf_tests", name);
        if (Directory.Exists(dir)) Directory.Delete(dir, true);
        Directory.CreateDirectory(dir);
        return dir;
    }

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

    private static string MakeTwoPageOfd(string dir)
    {
        string ofd = Path.Combine(dir, "two.ofd");
        using (var doc = new OFDDoc(ofd))
        {
            doc.AddPage();
            doc.CreateDrawContext(new global::NetOfd.Core.BasicType.STBox(0, 0, 210, 297)).FillText("第一页文字", 10, 50);
            doc.AddPage();
            doc.CreateDrawContext(new global::NetOfd.Core.BasicType.STBox(0, 0, 210, 297)).FillText("第二页文字", 10, 50);
        }
        return ofd;
    }

    private static string MakeTableOfd(string dir)
    {
        string ofd = Path.Combine(dir, "table.ofd");
        using (var doc = new OFDDoc(ofd))
        {
            var table = new Table { ColumnCount = 3, RowCount = 2 };
            table.AddCell(new TableCell { Content = "姓名" }, 0, 0);
            table.AddCell(new TableCell { Content = "年龄" }, 0, 1);
            table.AddCell(new TableCell { Content = "城市" }, 0, 2);
            table.AddCell(new TableCell { Content = "张三" }, 1, 0);
            table.AddCell(new TableCell { Content = "30" }, 1, 1);
            table.AddCell(new TableCell { Content = "青岛" }, 1, 2);
            doc.AddTable(table);
        }
        return ofd;
    }

    [Fact]
    public void PdfExporter_AllPages_ShouldProduceValidPdf()
    {
        var dir = Dir("pdf_all");
        string ofd = MakeTextOfd(dir, "PDF导出验证文字");
        string pdf = Path.Combine(dir, "out.pdf");

        using (var exporter = new PdfExporter(ofd, pdf))
        {
            exporter.Export();
        }

        Assert.True(File.Exists(pdf));
        var info = new FileInfo(pdf);
        Assert.True(info.Length > 100, $"PDF 文件应有实际内容，实际大小 {info.Length}");

        // 验证 PDF 文件头
        byte[] header = File.ReadAllBytes(pdf).Take(5).ToArray();
        Assert.Equal((byte)'%', header[0]);
        Assert.Equal((byte)'P', header[1]);
        Assert.Equal((byte)'D', header[2]);
        Assert.Equal((byte)'F', header[3]);
        Assert.Equal((byte)'-', header[4]);
    }

    [Fact]
    public void PdfExporter_TwoPage_ShouldProducePdfWithMultiplePages()
    {
        var dir = Dir("pdf_two");
        string ofd = MakeTwoPageOfd(dir);
        string pdf = Path.Combine(dir, "out.pdf");

        using (var exporter = new PdfExporter(ofd, pdf))
        {
            exporter.Export();
        }

        Assert.True(File.Exists(pdf));
        var info = new FileInfo(pdf);
        Assert.True(info.Length > 200, $"双页 PDF 应有更多内容，实际大小 {info.Length}");

        // 验证 PDF 包含多个 Page 对象
        string pdfContent = File.ReadAllText(pdf);
        // PDF 中每个页面由 "/Type /Page" 标记（不含 /Pages）
        int pageCount = CountOccurrences(pdfContent, "/Type /Page\n");
        Assert.True(pageCount >= 2, $"双页 PDF 应至少有 2 个 /Type /Page，实际 {pageCount}");
    }

    [Fact]
    public void PdfExporter_SpecificPage_ShouldOnlyContainSelectedPages()
    {
        var dir = Dir("pdf_one");
        string ofd = MakeTwoPageOfd(dir);
        string pdf = Path.Combine(dir, "out.pdf");

        using (var exporter = new PdfExporter(ofd, pdf))
        {
            exporter.Export(1); // 仅导出第 2 页
        }

        Assert.True(File.Exists(pdf));
        var info = new FileInfo(pdf);
        Assert.True(info.Length > 100);

        // 只导出 1 页，PDF 应只有 1 个 Page 对象
        string pdfContent = File.ReadAllText(pdf);
        int pageCount = CountOccurrences(pdfContent, "/Type /Page\n");
        Assert.Equal(1, pageCount);
    }

    [Fact]
    public void PdfExporter_OutOfRangeIndex_ShouldBeIgnored()
    {
        var dir = Dir("pdf_oob");
        string ofd = MakeTextOfd(dir, "越界测试");
        string pdf = Path.Combine(dir, "out.pdf");

        using var exporter = new PdfExporter(ofd, pdf);
        exporter.Export(-1, 0, 99);

        Assert.True(File.Exists(pdf));
    }

    [Fact]
    public void PdfExporter_Table_ShouldProduceValidPdf()
    {
        var dir = Dir("pdf_table");
        string ofd = MakeTableOfd(dir);
        string pdf = Path.Combine(dir, "out.pdf");

        using (var exporter = new PdfExporter(ofd, pdf))
        {
            exporter.Export();
        }

        Assert.True(File.Exists(pdf));
        var info = new FileInfo(pdf);
        Assert.True(info.Length > 200, $"表格 PDF 应有实际内容，实际大小 {info.Length}");

        // 验证 PDF 文件头
        byte[] header = File.ReadAllBytes(pdf).Take(5).ToArray();
        Assert.Equal((byte)'%', header[0]);
    }

    [Fact]
    public void PdfExporter_PageCount_ShouldMatchOfdPages()
    {
        var dir = Dir("pdf_count");
        string ofd = MakeTwoPageOfd(dir);

        using var exporter = new PdfExporter(ofd, Path.Combine(dir, "out.pdf"));
        Assert.Equal(2, exporter.PageCount);
    }

    [Fact]
    public void OfdPdfMaker_InvalidIndex_ShouldThrowConvertException()
    {
        var dir = Dir("pdf_bad");
        string ofd = MakeTextOfd(dir, "abc");
        using var maker = new OfdPdfMaker(ofd);
        Assert.Throws<GeneralConvertException>(() =>
        {
            using var ms = new MemoryStream();
            using var doc = SkiaSharp.SKDocument.CreatePdf(ms);
            maker.MakePage(doc, 5);
        });
    }

    private static int CountOccurrences(string text, string pattern)
    {
        int count = 0, idx = 0;
        while ((idx = text.IndexOf(pattern, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += pattern.Length;
        }
        return count;
    }
}
