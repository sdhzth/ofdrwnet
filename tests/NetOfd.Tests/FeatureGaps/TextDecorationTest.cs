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
/// 文字装饰端到端：下划线/删除线应生成描边路径（此前为 TODO 空转，静默丢失）。
/// </summary>
public class TextDecorationTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_deco_tests");
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

    private static void AssertHasStrokeLine(string path, string label)
    {
        var content = FirstPageContent(path);
        var strokePaths = content.Descendants()
            .Where(e => e.Name.LocalName == "PathObject")
            .Where(e => (string?)e.Attribute("Stroke") == "true")
            .ToList();

        Assert.True(strokePaths.Count >= 1, $"{label}: 未找到描边路径");

        var data = (string?)strokePaths[0].Elements()
            .FirstOrDefault(e => e.Name.LocalName == "AbbreviatedData")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(data), $"{label}: 缺少路径数据");
        Assert.Contains("M ", data!);
        Assert.Contains("L ", data!);
        Assert.DoesNotContain("C ", data!); // 直线而非曲线

        // 装饰线应有颜色与线宽
        Assert.NotNull(strokePaths[0].Elements()
            .FirstOrDefault(e => e.Name.LocalName == "StrokeColor"));
    }

    [Fact]
    public void Underline_ShouldProduceStrokeLine()
    {
        var path = Path.Combine(OutDir(), "deco_u.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new Paragraph().SetWidth(160)
                .Add(new Span("带下划线文本").SetFontSize(5d).SetUnderline()));
        }
        AssertHasStrokeLine(path, "下划线");
    }

    [Fact]
    public void StrikeThrough_ShouldProduceStrokeLine()
    {
        var path = Path.Combine(OutDir(), "deco_s.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            var span = new Span("带删除线文本").SetFontSize(5d);
            span.StrikeThrough = true;
            doc.Add(new Paragraph().SetWidth(160).Add(span));
        }
        AssertHasStrokeLine(path, "删除线");
    }

    [Fact]
    public void PlainSpan_ShouldNotProduceDecoration()
    {
        var path = Path.Combine(OutDir(), "deco_none.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new Paragraph().SetWidth(160)
                .Add(new Span("普通文本无装饰").SetFontSize(5d)));
        }
        var content = FirstPageContent(path);
        Assert.DoesNotContain(content.Descendants()
            .Where(e => e.Name.LocalName == "PathObject")
            .Where(e => (string?)e.Attribute("Stroke") == "true"), _ => true);
    }
}
