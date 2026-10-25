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
/// 两端对齐：折行整行应带 TextCode.Width（渲染端按行宽均匀分布字形），末行保持左对齐。
/// </summary>
public class JustifyAlignTest
{
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

    [Fact]
    public void Justify_WrappedLines_ShouldCarryWidth_LastLineNot()
    {
        var path = Path.Combine(Path.GetTempPath(), "netofd_justify.ofd");
        // 40 字 × 5mm = 200mm，行宽 60 → 折成 ≥3 行
        var text = new string('两', 40);
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new Paragraph().SetWidth(60).SetTextAlign(TextAlign.Justify)
                .Add(new Span(text).SetFontSize(5)));
        }

        var codes = FirstPageContent(path).Descendants()
            .Where(e => e.Name.LocalName == "TextCode").ToList();
        Assert.True(codes.Count >= 3, $"折行数不足: {codes.Count}");

        for (int i = 0; i < codes.Count - 1; i++)
        {
            var w = (string?)codes[i].Attribute("Width");
            Assert.True(!string.IsNullOrWhiteSpace(w),
                $"第 {i + 1} 行（折行整行）缺少 Width");
            Assert.Equal(60, double.Parse(w!), 1);
        }
        Assert.Null(codes[^1].Attribute("Width")); // 末行不拉伸
    }

    [Fact]
    public void LeftAlign_ShouldNeverSetWidth()
    {
        var path = Path.Combine(Path.GetTempPath(), "netofd_left_nowidth.ofd");
        var text = new string('两', 40);
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new Paragraph().SetWidth(60).SetTextAlign(TextAlign.Left)
                .Add(new Span(text).SetFontSize(5)));
        }
        Assert.DoesNotContain(FirstPageContent(path).Descendants()
            .Where(e => e.Name.LocalName == "TextCode"),
            c => c.Attribute("Width") is not null);
    }
}
