using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// 段落对齐与文字样式记录测试（此前 Bold/Italic/TextAlign 均被静默丢弃）。
/// </summary>
public class ParagraphAlignAndStyleTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_align_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static XDocument FirstPageContent(string path)
    {
        using var ms = File.OpenRead(path);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = za.Entries.First(e => e.FullName.EndsWith("Content.xml", StringComparison.Ordinal));
        using var s = entry.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(buf.ToArray()));
    }

    private static double BoundaryX(XElement content)
    {
        var textObj = content.Descendants()
            .First(e => e.Name.LocalName == "TextObject");
        var parts = ((string?)textObj.Attribute("Boundary"))!
            .Split(' ').Select(double.Parse).ToArray();
        return parts[0];
    }

    [Theory]
    [InlineData(TextAlign.Left)]
    [InlineData(TextAlign.Center)]
    [InlineData(TextAlign.Right)]
    public void TextAlign_ShouldShiftRunBoundary(TextAlign align)
    {
        var path = Path.Combine(OutDir(), $"align_{align}.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            // 行宽 160，文本"对齐"2 字 ×5mm 字宽 = 10mm：
            // Left→10；Center→10+(160-10)/2=85？注意 maxWidth=Width-? 此处以实测为准
            // 显式 X=0：流式引擎对未定位段落默认从左边距起排，本用例聚焦对齐平移本身
            var p = new Paragraph().SetWidth(160).SetTextAlign(align)
                .Add(new Span("对齐").SetFontSize(5));
            p.SetXY(0, 0);
            doc.Add(p);
        }

        var x = BoundaryX(FirstPageContent(path).Root!);
        switch (align)
        {
            case TextAlign.Left:
                Assert.InRange(x, -1, 2);
                break;
            case TextAlign.Center:
                Assert.InRange(x, 55, 95);
                break;
            case TextAlign.Right:
                Assert.InRange(x, 120, 165);
                break;
        }
    }

    [Fact]
    public void Bold_Italic_ShouldBeRecordedOnTextObject()
    {
        var path = Path.Combine(OutDir(), "bold_italic.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new Paragraph().SetWidth(160)
                .Add(new Span("粗斜体").SetFontSize(5).SetBold().SetItalic()));
            doc.Add(new Paragraph().SetWidth(160)
                .Add(new Span("常规").SetFontSize(5)));
        }

        var content = FirstPageContent(path);
        var objs = content.Descendants()
            .Where(e => e.Name.LocalName == "TextObject").ToList();
        Assert.Equal(2, objs.Count);

        var styled = objs[0];
        Assert.Equal("true", (string?)styled.Attribute("Bold"));
        Assert.Equal("true", (string?)styled.Attribute("Italic"));

        Assert.Null(objs[1].Attribute("Bold"));
        Assert.Null(objs[1].Attribute("Italic"));
    }
}
