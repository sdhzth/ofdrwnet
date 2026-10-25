using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// OnNewPage 新页回调 + DrawFixedDiv 测试（对应 Java VPageHandler/setOnPage 语义）：
/// 每页创建并注册后触发回调（覆盖初始页/自动分页/手动 AddPage），
/// 回调经 DrawFixedDiv 立即绘制页眉/页脚等每页固定内容。
/// </summary>
public class OnNewPageTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_onnewpage_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>按页目录序号读取文档全部页面 Content.xml（Doc_0/Pages/Page_N/Content.xml）</summary>
    private static List<XDocument> AllPageContents(string ofd)
    {
        using var za = ZipFile.OpenRead(ofd);
        var entries = za.Entries
            .Where(e => e.FullName.EndsWith("/Content.xml", StringComparison.OrdinalIgnoreCase))
            .Select(e => new
            {
                Name = e.FullName,
                Order = int.TryParse(
                    e.FullName.Replace("\\", "/").Split('/').Reverse()
                        .Skip(1).FirstOrDefault()?.Replace("Page_", ""),
                    out var n) ? n : int.MaxValue
            })
            .OrderBy(x => x.Order)
            .ToList();

        var docs = new List<XDocument>();
        foreach (var entryInfo in entries)
        {
            var e = za.GetEntry(entryInfo.Name)!;
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            docs.Add(XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray())));
        }
        return docs;
    }

    private static string PageText(XDocument content)
    {
        var sb = new StringBuilder();
        foreach (var tc in content.Descendants().Where(el => el.Name.LocalName == "TextCode"))
            sb.Append(tc.Value);
        return sb.ToString();
    }

    [Fact]
    public void OnNewPage_ShouldFireForEveryPage_WithSequentialIndex()
    {
        var path = Path.Combine(OutDir(), "hook_indices.ofd");
        var pages = new List<int>();
        int pageCount;
        using (var doc = new OFDDoc(path))
        {
            doc.OnNewPage = (_, idx) => pages.Add(idx);
            // 4 块 150mm（普通 Div 为可拆块，按切缝连续填充）：必然触发 ≥2 次自动分页
            for (var k = 0; k < 4; k++)
                doc.Add(new Div().SetWidth(100).SetHeight(150));
            doc.Close();
            pageCount = doc.PageCount;
        }
        Assert.True(pageCount >= 3, $"应至少产生 3 页，实际 {pageCount}");
        // 回调次数与页数一致，页序号自 0 连续递增（初始页 + 每次自动分页）
        Assert.Equal(Enumerable.Range(0, pageCount).ToList(), pages);
    }

    [Fact]
    public void OnNewPage_ShouldDrawHeaderOnEveryPage()
    {
        var path = Path.Combine(OutDir(), "hook_header.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.OnNewPage = (d, idx) =>
            {
                d.DrawFixedDiv(new Paragraph().AddText($"页眉第{idx + 1}页")
                    .SetFontSize(4d).SetXY(20, 5));
            };
            for (var k = 0; k < 4; k++)
                doc.Add(new Div().SetWidth(100).SetHeight(150));
            doc.Add(new Paragraph().AddText("正文第一段").SetFontSize(5d));
            doc.Close();
        }

        var contents = AllPageContents(path);
        Assert.True(contents.Count >= 3, $"应至少产生 3 页，实际 {contents.Count}");
        // 每一页都有页眉，且页码与页序一致
        for (var p = 0; p < contents.Count; p++)
            Assert.Contains($"页眉第{p + 1}页", PageText(contents[p]));
        // 正文不受页眉影响（页眉 Y=5 在内容区顶之上）
        Assert.Contains("正文第一段", PageText(contents[^1]));
    }

    [Fact]
    public void DrawFixedDiv_Standalone_ShouldNotAffectFlowCursor()
    {
        var path = Path.Combine(OutDir(), "hook_standalone.ofd");
        using (var doc = new OFDDoc(path))
        {
            // 无页时先建页，随后立即绘制固定角标；正文流不受影响
            doc.DrawFixedDiv(new Paragraph().AddText("独立角标")
                .SetFontSize(4d).SetXY(150, 250));
            doc.Add(new Paragraph().AddText("正文内容").SetFontSize(5d));
            doc.Close();
            Assert.Equal(1, doc.PageCount);
        }

        var contents = AllPageContents(path);
        var text = PageText(contents[0]);
        Assert.Contains("独立角标", text);
        Assert.Contains("正文内容", text);
    }

    [Fact]
    public void OnNewPage_ShouldFireOnManualAddPage()
    {
        var path = Path.Combine(OutDir(), "hook_manual.ofd");
        var count = 0;
        using (var doc = new OFDDoc(path))
        {
            doc.OnNewPage = (_, _) => count++;
            doc.AddPage();
            doc.AddPage();
            doc.Close();
        }
        Assert.Equal(2, count);
    }

    [Fact]
    public void DrawFixedDiv_Null_ShouldThrow()
    {
        var path = Path.Combine(OutDir(), "hook_null.ofd");
        using (var doc = new OFDDoc(path))
        {
            Assert.Throws<ArgumentNullException>(() => doc.DrawFixedDiv(null!));
        }
    }
}
