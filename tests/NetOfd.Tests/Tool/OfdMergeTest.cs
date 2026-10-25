using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Tool;
using Xunit;

namespace NetOfd.Tests.Tool;

public class OfdMergeTest
{
    private static readonly XNamespace Ns = "http://www.ofdspec.org/2016";

    /// <summary>生成含指定页数的样例 OFD（内容落在各页，字体资源被注册）。</summary>
    private static string GenOfd(int pages, string dir, string name)
    {
        var path = Path.Combine(dir, name);
        using (var doc = new OFDDoc(path))
        {
            for (int i = 0; i < pages; i++)
            {
                doc.AddPage();
                doc.Add(new Paragraph().AddText($"样例文档 {name} 第 {i + 1} 页内容"));
            }
        }
        return path;
    }

    /// <summary>统计 OFD 文档的页面数（解析 Doc_0/Document.xml 的 Pages 树）。</summary>
    private static int CountPages(string ofdPath)
    {
        using var za = ZipFile.OpenRead(ofdPath);
        var entry = za.GetEntry("Doc_0/Document.xml")
            ?? throw new Exception("缺少 Doc_0/Document.xml");
        byte[] bytes;
        using (var s = entry.Open()) { using var ms = new MemoryStream(); s.CopyTo(ms); bytes = ms.ToArray(); }
        if (bytes.Length == 0)
            throw new Exception($"Document.xml 为空 (0 字节): {ofdPath}");
        try
        {
            XElement docEl;
            using (var s2 = new MemoryStream(bytes)) docEl = XElement.Load(s2);
            var doc = new Document(docEl);
            return doc.Pages?.GetPages().Count ?? 0;
        }
        catch (Exception ex)
        {
            var preview = System.Text.Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 120));
            throw new Exception($"解析 Document.xml 失败 (len={bytes.Length}): {preview} | {ex.Message}");
        }
    }

    /// <summary>统计合并后 PublicRes.xml 中的资源条目数（验证资源迁移）。</summary>
    private static int CountPublicRes(string ofdPath)
    {
        using var za = ZipFile.OpenRead(ofdPath);
        var entry = za.GetEntry("Doc_0/PublicRes.xml");
        if (entry == null) return 0;
        XElement resEl;
        using (var s = entry.Open()) resEl = XElement.Load(s);
        return resEl.Elements().Count();
    }

    /// <summary>校验合并产物中所有 Page_N/Content.xml 均能被解析。</summary>
    private static void AssertAllContentParsable(string ofdPath)
    {
        using var za = ZipFile.OpenRead(ofdPath);
        foreach (var entry in za.Entries.Where(e => e.FullName.Contains("Pages/Page_") && e.FullName.EndsWith("Content.xml")))
        {
            using var s = entry.Open();
            var el = XElement.Load(s);
            Assert.Equal(Ns + "Page", el.Name);
        }
    }

    [Fact]
    public void Merge_TwoDocs_AllPages()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_merge_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = GenOfd(2, dir, "A.ofd");
            var b = GenOfd(1, dir, "B.ofd");
            var outPath = Path.Combine(dir, "merged.ofd");

            using (var merger = new OfdMerger(outPath))
            {
                merger.Add(a).Add(b);
            }

            Assert.True(File.Exists(outPath));
            Assert.Equal(3, CountPages(outPath));
            AssertAllContentParsable(outPath);
            // 字体资源应被迁移到合并文档的 PublicRes
            Assert.True(CountPublicRes(outPath) > 0);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Merge_SelectedPages()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_merge_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = GenOfd(3, dir, "A.ofd");
            var b = GenOfd(2, dir, "B.ofd");
            var outPath = Path.Combine(dir, "merged.ofd");

            using (var merger = new OfdMerger(outPath))
            {
                merger.Add(a, 1, 3).Add(b, 2); // A 第1、3页 + B 第2页 => 3 页
            }

            Assert.Equal(3, CountPages(outPath));
            AssertAllContentParsable(outPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Merge_Mix_TwoPagesIntoOne()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_merge_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = GenOfd(1, dir, "A.ofd");
            var b = GenOfd(1, dir, "B.ofd");
            var outPath = Path.Combine(dir, "mixed.ofd");

            using (var merger = new OfdMerger(outPath))
            {
                merger.AddMix(a, 1, b, 1);
            }

            // 混合：两页叠加为一个页面
            Assert.Equal(1, CountPages(outPath));
            AssertAllContentParsable(outPath);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Delete_Pages_ByIndex()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_del_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var src = GenOfd(3, dir, "src.ofd");
            var outPath = Path.Combine(dir, "del.ofd");

            using (var deleter = new OfdPageDeleter(src, outPath))
            {
                deleter.Delete(0); // 删除第 1 页（0-based）
            }

            Assert.True(File.Exists(outPath));
            Assert.Equal(2, CountPages(outPath));
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Merge_ThenDelete_RoundTrip()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_rt_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var a = GenOfd(2, dir, "A.ofd");
            var b = GenOfd(2, dir, "B.ofd");
            var merged = Path.Combine(dir, "merged.ofd");
            using (var merger = new OfdMerger(merged)) { merger.Add(a).Add(b); }
            Assert.Equal(4, CountPages(merged));

            var trimmed = Path.Combine(dir, "trimmed.ofd");
            using (var deleter = new OfdPageDeleter(merged, trimmed)) { deleter.Delete(0, 2); }
            Assert.Equal(2, CountPages(trimmed));
            AssertAllContentParsable(trimmed);
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
