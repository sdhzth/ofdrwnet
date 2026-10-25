using System.IO.Compression;
using System.Text;
using NetOfd.Pkg.Container;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// OFDReader 多文档栈式支持测试——对齐 Java <c>cdDoc(int)/getDoc(int)/cdDefaultDoc()</c>。
/// <para>Java 语义（ofdrw-reader OFDReader L414-459）：</para>
/// <list type="bullet">
/// <item><c>cdDoc(int)</c>：按 <b>OFD.xml DocBody 出现序号</b>（非目录名编号）切换——解析该
/// DocBody.DocRoot 指向的 Document.xml，并让资源加载器永久指向该文档目录；返回 Document。</item>
/// <item><c>getDoc(int)</c>：save/restore 语义——只读解析不切换当前文档。</item>
/// <item><c>cdDefaultDoc()</c>：等价 cdDoc(0)。</item>
/// </list>
/// <para>Java 官方测试资源无多文档样本（OFDReaderTest 未测 cdDoc），故测试内动态构造：
/// 以 helloworld.ofd 为底，克隆 Doc_0 → Doc_1（正文 TextCode 改为「第二文档：…」以区分），
/// OFD.xml 追加第二个 DocBody（DocRoot=Doc_1/Document.xml），打包为 MultiDocTest.ofd。</para>
/// </summary>
public class OFDReaderMultiDocTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private const string TextDoc1 = "第二文档：Reader 文档切换验证";

    // ── 动态构造双 DocBody 样本（一次性） ──

    private static readonly string? MultiDocPath = BuildMultiDoc();

    private static bool HasSource => File.Exists(Path.Combine(Res, "helloworld.ofd"));

    private static string? BuildMultiDoc()
    {
        if (!HasSource) return null;
        string outFile = Path.Combine(AppContext.BaseDirectory, "MultiDocTest.ofd");
        if (File.Exists(outFile)) return outFile; // 幂等：已构造过

        using var za = ZipFile.OpenRead(Path.Combine(Res, "helloworld.ofd"));
        var entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        foreach (var e in za.Entries)
        {
            if (e.FullName.EndsWith("/")) continue; // 目录条目
            using var ms = new MemoryStream();
            using (var s = e.Open()) s.CopyTo(ms);
            entries[e.FullName] = ms.ToArray();
        }

        // 1) 克隆 Doc_0/* → Doc_1/*，正文文本改第二文档内容以区分
        foreach (var (path, data) in entries
                     .Where(kv => kv.Key.StartsWith("Doc_0/", StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            string newPath = "Doc_1/" + path["Doc_0/".Length..];
            byte[] newData = data;
            if (newPath.EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase))
                newData = RewriteTextCode(data, TextDoc1);
            entries[newPath] = newData;
        }

        // 2) OFD.xml 追加第二个 DocBody（DocRoot=Doc_1/Document.xml，新 DocID）
        entries["OFD.xml"] = AppendSecondDocBody(entries["OFD.xml"]);

        // 3) 写新 zip
        using var fs = File.Create(outFile);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var (path, data) in entries)
        {
            var entry = zip.CreateEntry(path.Replace('\\', '/'), CompressionLevel.Optimal);
            using var s = entry.Open();
            s.Write(data, 0, data.Length);
        }
        return outFile;
    }

    /// <summary>把 Content.xml 中第一个 TextCode 的文本替换为 newText（字符串级替换，零重写风险）</summary>
    private static byte[] RewriteTextCode(byte[] xml, string newText)
    {
        string s = Encoding.UTF8.GetString(xml);
        const string oldText = "你好呀，OFD Reader&amp;Writer！";
        int pos = s.IndexOf(oldText, StringComparison.Ordinal);
        if (pos < 0) return xml;
        s = s[..pos] + newText + s[(pos + oldText.Length)..];
        return Encoding.UTF8.GetBytes(s);
    }

    /// <summary>OFD.xml 中第一个 DocBody 结束标签之后追加第二个 DocBody</summary>
    private static byte[] AppendSecondDocBody(byte[] xml)
    {
        string s = Encoding.UTF8.GetString(xml);
        const string marker = "</ofd:DocBody>";
        int pos = s.IndexOf(marker, StringComparison.Ordinal);
        if (pos < 0) return xml;
        pos += marker.Length;
        string second =
            "<ofd:DocBody><ofd:DocInfo>" +
            "<ofd:DocID>9f2c0b3a1d4e5f60718293a4b5c6d7e8</ofd:DocID>" +
            "<ofd:CreationDate>2026-09-03</ofd:CreationDate>" +
            "<ofd:Creator>NetOfd MultiDoc Test</ofd:Creator>" +
            "<ofd:CreatorVersion>1.0.0</ofd:CreatorVersion>" +
            "</ofd:DocInfo><ofd:DocRoot>Doc_1/Document.xml</ofd:DocRoot></ofd:DocBody>";
        s = s[..pos] + second + s[pos..];
        return Encoding.UTF8.GetBytes(s);
    }

    private static List<string> PageText(OFDReader reader, int pageNum = 1)
        => new ContentExtractor(reader, null).GetPageContent(pageNum);

    private static void AssertOnDoc0(OFDReader reader)
    {
        Assert.Equal("Doc_0", reader.GetDocDir().Name);
        Assert.Contains(PageText(reader), t => t.Contains("你好呀"));
    }

    // ── 用例 ─────────────────────────────────────────────────

    [Fact]
    public void MultiDocOfd_ShouldContainTwoDocBodies()
    {
        if (MultiDocPath is null) { Assert.True(true, "Java 测试资源不存在，跳过。"); return; }
        using var reader = new OFDReader(MultiDocPath);

        var bodies = reader.GetOfd().GetDocBodies();
        Assert.Equal(2, bodies.Count);
        Assert.Equal("Doc_0/Document.xml", bodies[0].DocRoot?.Value);
        Assert.Equal("Doc_1/Document.xml", bodies[1].DocRoot?.Value);
    }

    [Fact]
    public void CdDoc_ToSecondDoc_ShouldSwitchAllState()
    {
        if (MultiDocPath is null) { Assert.True(true, "Java 测试资源不存在，跳过。"); return; }
        using var reader = new OFDReader(MultiDocPath);

        // 初始：第一个 DocBody（Doc_0）
        AssertOnDoc0(reader);

        // CdDoc(1) → Doc_1：目录名、正文、缓存 Document、资源定位器全部切换
        var doc1 = reader.CdDoc(1);
        Assert.NotNull(doc1);
        Assert.Equal("Doc_1", reader.GetDocDir().Name);
        Assert.Contains(PageText(reader), t => t.Contains(TextDoc1));
        Assert.Same(doc1.Element, reader.GetDocument().Element);
        // 文档缓存指向 Doc_1 的 Document.xml（与 OFDDir 层缓存同一 XElement）
        Assert.Same(doc1.Element, reader.GetOfdDir().GetDocByIndex(1).GetDocument()!.Element);
        // 资源定位器基准已切：能解析 Doc_1 内相对路径（Document.xml 同目录的 PublicRes.xml）
        var rl = reader.GetResourceLocator();
        Assert.True(rl.Exist(new NetOfd.Core.BasicType.STLoc("PublicRes.xml")));
    }

    [Fact]
    public void CdDefaultDoc_ShouldReturnToFirstDoc()
    {
        if (MultiDocPath is null) { Assert.True(true, "Java 测试资源不存在，跳过。"); return; }
        using var reader = new OFDReader(MultiDocPath);

        reader.CdDoc(1);
        Assert.Equal("Doc_1", reader.GetDocDir().Name);

        var doc0 = reader.CdDefaultDoc();
        Assert.NotNull(doc0);
        Assert.Equal("Doc_0", reader.GetDocDir().Name);
        AssertOnDoc0(reader);
    }

    [Fact]
    public void GetDoc_IsReadOnly_ShouldNotSwitchCurrentDoc()
    {
        if (MultiDocPath is null) { Assert.True(true, "Java 测试资源不存在，跳过。"); return; }
        using var reader = new OFDReader(MultiDocPath);

        // 只读获取文档 1：返回其 Document，但当前文档仍为 Doc_0
        var doc1 = reader.GetDoc(1);
        Assert.NotNull(doc1);
        Assert.Equal("Doc_0", reader.GetDocDir().Name);
        AssertOnDoc0(reader);
        // 返回的 Document 确实对应 Doc_1（与容器层同一 XElement）
        Assert.Same(doc1.Element, reader.GetOfdDir().GetDocByIndex(1).GetDocument()!.Element);
    }

    [Fact]
    public void CdDoc_OutOfRange_ShouldThrowAndKeepState()
    {
        if (MultiDocPath is null) { Assert.True(true, "Java 测试资源不存在，跳过。"); return; }
        using var reader = new OFDReader(MultiDocPath);

        Assert.Throws<BadOFDException>(() => reader.CdDoc(2));
        Assert.Throws<BadOFDException>(() => reader.CdDoc(-1));
        // 异常后状态不变（仍在 Doc_0）
        Assert.Equal("Doc_0", reader.GetDocDir().Name);
        AssertOnDoc0(reader);
    }
}
