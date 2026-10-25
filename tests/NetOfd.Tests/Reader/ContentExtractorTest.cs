using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.PageDescription;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// 内容抽取器测试（按页码抽取文本 / 文本对象，支持过滤器）。
/// 对应 Java: <c>org.ofdrw.reader.ContentExtractor</c> + <c>ExtractorFilter</c>。
/// </summary>
public class ContentExtractorTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_extractor_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    // ── 样例文档 ──────────────────────────────────────────────────

    /// <summary>
    /// 构建两页样例：第 1 页「姓名：张三」+「Hello World」；
    /// 第 2 页「姓名：张三」+「保持不变的内容」。
    /// </summary>
    private static string BuildTwoPageOfd(string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.Add(P("姓名：张三"));
            doc.Add(P("Hello World"));
            doc.Add(new NetOfd.Layout.Element.PageAreaFiller());
            doc.Add(P("姓名：张三"));
            doc.Add(P("保持不变的内容"));
        }
        return path;
    }

    private static NetOfd.Layout.Element.Paragraph P(string text)
        => new NetOfd.Layout.Element.Paragraph().AddText(text).SetFontSize(5d);

    // ── zip 辅助 ──────────────────────────────────────────────────

    private static Dictionary<string, byte[]> ReadZipMap(string path)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var za = ZipFile.OpenRead(path);
        foreach (var e in za.Entries)
        {
            using var s = e.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            map[e.FullName] = buf.ToArray();
        }
        return map;
    }

    private static void WriteZipMap(string path, Dictionary<string, byte[]> map)
    {
        if (File.Exists(path)) File.Delete(path);
        using var za = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var kv in map)
        {
            var entry = za.CreateEntry(kv.Key);
            using var s = entry.Open();
            s.Write(kv.Value, 0, kv.Value.Length);
        }
    }

    private static XDocument EntryXml(string path, string entryName)
        => XDocument.Parse(Encoding.UTF8.GetString(ReadZipMap(path)[entryName]));

    private static string PageLoc(string path, int pageNumber)
    {
        var docXml = EntryXml(path, "Doc_0/Document.xml");
        var pagesEl = docXml.Descendants()
            .First(e => e.Name.LocalName == "Pages" && e.Parent?.Name.LocalName == "Document");
        var loc = pagesEl.Elements().Where(e => e.Name.LocalName == "Page")
            .ElementAt(pageNumber - 1).Attribute("BaseLoc")!.Value;
        var n = loc.Trim().TrimStart('/').Replace('\\', '/');
        return n.StartsWith("Doc_", StringComparison.OrdinalIgnoreCase) ? n : $"Doc_0/{n}";
    }

    // ──────────────────────────────────────────────────────────────

    /// <summary>按页抽取：每个 TextCode 一条，顺序与写入一致。</summary>
    [Fact]
    public void GetPageContent_ShouldReturnOneItemPerTextCode()
    {
        var path = BuildTwoPageOfd("CE_Page.ofd");
        using var reader = new NetOfd.Reader.OFDReader(path);
        var ex = new NetOfd.Reader.ContentExtractor(reader);

        Assert.Equal(new[] { "姓名：张三", "Hello World" }, ex.GetPageContent(1));
        Assert.Equal(new[] { "姓名：张三", "保持不变的内容" }, ex.GetPageContent(2));
    }

    /// <summary>抽取文本对象：返回 CTText 本身（可供调用方改写/替换）。</summary>
    [Fact]
    public void GetPageTextObject_ShouldReturnTextObjects()
    {
        var path = BuildTwoPageOfd("CE_Obj.ofd");
        using var reader = new NetOfd.Reader.OFDReader(path);
        var ex = new NetOfd.Reader.ContentExtractor(reader);

        var objs = ex.GetPageTextObject(1);
        Assert.Equal(2, objs.Count);

        var texts = objs.Select(TextOf).ToList();
        Assert.Contains("姓名：张三", texts);
        Assert.Contains("Hello World", texts);
    }

    /// <summary>全文档抽取：按页顺序聚合所有 TextCode。</summary>
    [Fact]
    public void ExtractAll_ShouldAggregateEveryPage()
    {
        var path = BuildTwoPageOfd("CE_All.ofd");
        using var reader = new NetOfd.Reader.OFDReader(path);
        var ex = new NetOfd.Reader.ContentExtractor(reader);

        Assert.Equal(
            new[] { "姓名：张三", "Hello World", "姓名：张三", "保持不变的内容" },
            ex.ExtractAll());

        Assert.Equal(4, ex.ExtractAllTextObject().Count);
    }

    /// <summary>逐页遍历：仅对非空页回调，页码从 1 开始。</summary>
    [Fact]
    public void Traverse_ShouldInvokePerPage()
    {
        var path = BuildTwoPageOfd("CE_Traverse.ofd");
        using var reader = new NetOfd.Reader.OFDReader(path);
        var ex = new NetOfd.Reader.ContentExtractor(reader);

        var seen = new List<(int Page, string Joined)>();
        ex.Traverse((pageNum, contents) => seen.Add((pageNum, string.Join("|", contents))));

        Assert.Equal(new[] { (1, "姓名：张三|Hello World"), (2, "姓名：张三|保持不变的内容") }, seen);
    }

    /// <summary>过滤器：返回 null 丢弃该条；返回非空则「改写」抽取结果。</summary>
    [Fact]
    public void Filter_ShouldDropAndRewriteExtractedText()
    {
        var path = BuildTwoPageOfd("CE_Filter.ofd");
        using var reader = new NetOfd.Reader.OFDReader(path);

        var ex = new NetOfd.Reader.ContentExtractor(reader, new DropAsciiRenameChinese());

        // 「Hello World」被丢弃；「姓名：张三」被改写为 NAME
        Assert.Equal(new[] { "NAME" }, ex.GetPageContent(1));

        // 文本对象：整对象只要有任一条通过过滤即入选
        var objs = ex.GetPageTextObject(1);
        Assert.Single(objs);
        Assert.Equal("姓名：张三", TextOf(objs[0]));
    }

    /// <summary>示例过滤器：丢弃 ASCII 文本，中文文本改写为固定标记。</summary>
    private sealed class DropAsciiRenameChinese : NetOfd.Reader.IExtractorFilter
    {
        public string? AllowText(CTText textObject, TextCode code)
        {
            var s = code.Text ?? string.Empty;
            if (s.Length > 0 && s[0] <= 127) return null;   // 丢弃 ASCII
            return "NAME";                                  // 改写
        }
    }

    /// <summary>非法页码应抛出参数异常。</summary>
    [Fact]
    public void GetPageContent_InvalidPageNumber_ShouldThrow()
    {
        var path = BuildTwoPageOfd("CE_Range.ofd");
        using var reader = new NetOfd.Reader.OFDReader(path);
        var ex = new NetOfd.Reader.ContentExtractor(reader);

        Assert.Throws<ArgumentException>(() => ex.GetPageContent(0));
        Assert.Throws<ArgumentException>(() => ex.GetPageContent(99));
        Assert.Throws<ArgumentException>(() => ex.GetPageTextObject(0));
    }

    /// <summary>嵌套 PageBlock 内的文本也应被递归抽取到。</summary>
    [Fact]
    public void NestedPageBlock_ShouldBeExtractedRecursively()
    {
        var path = BuildTwoPageOfd("CE_Block.ofd");
        var wrapped = Out("CE_Block_wrapped.ofd");
        WrapInPageBlock(path, wrapped, 1, "Hello World");

        using var reader = new NetOfd.Reader.OFDReader(wrapped);
        var ex = new NetOfd.Reader.ContentExtractor(reader);

        var items = ex.GetPageContent(1);
        Assert.Contains("Hello World", items);
        Assert.Contains("姓名：张三", items);
    }

    /// <summary>把指定文本所在的 TextObject 包进一层 PageBlock（模拟分块内容）。</summary>
    private static void WrapInPageBlock(string input, string output, int pageNumber, string text)
    {
        var map = ReadZipMap(input);
        var loc = PageLoc(input, pageNumber);
        var doc = XDocument.Parse(Encoding.UTF8.GetString(map[loc]));
        var ns = doc.Root!.Name.Namespace;

        var textObj = doc.Descendants()
            .First(e => e.Name.LocalName == "TextObject" && e.Value.Contains(text, StringComparison.Ordinal));
        var layer = textObj.Parent ?? throw new InvalidOperationException("TextObject 缺少父图层");
        var idx = layer.Elements().ToList().IndexOf(textObj);

        textObj.Remove();
        var block = new XElement(ns + "PageBlock", textObj);
        // 插回原位置（保持顺序）
        var children = layer.Elements().ToList();
        layer.RemoveNodes();
        for (int i = 0; i < children.Count; i++)
        {
            if (i == idx) layer.Add(block);
            layer.Add(children[i]);
        }
        if (idx >= children.Count) layer.Add(block);

        map[loc] = Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
        WriteZipMap(output, map);
    }

    /// <summary>把 CTText 的所有 TextCode 文本拼接起来。</summary>
    private static string TextOf(CTText t)
        => string.Concat(t.GetTextCodes().Select(c => c.Text));
}
