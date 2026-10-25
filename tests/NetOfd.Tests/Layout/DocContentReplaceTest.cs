using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Layout.Edit;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 文档内容替换测试（按文本映射替换已有 OFD 中的文字并重算 DeltaX）。
/// 对应 Java: <c>org.ofdrw.layout.DocContentReplace</c>。
/// </summary>
public class DocContentReplaceTest
{
    // 测试用字号（mm）：DeltaX 期望值 = 字宽比例 × 字号
    private const double FontSize = 5d;

    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_replace_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    // ── 样例文档 ──────────────────────────────────────────────────

    /// <summary>
    /// 构建两页样例文档：
    /// 第 1 页 = 「姓名：张三」+「Hello World」；第 2 页 = 「姓名：张三」+「保持不变的内容」。
    /// </summary>
    private static byte[] BuildTwoPageOfd(string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("姓名：张三").SetFontSize(FontSize));
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("Hello World").SetFontSize(FontSize));

            // 填充剩余空间 → 其后内容进入新页
            doc.Add(new NetOfd.Layout.Element.PageAreaFiller());

            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("姓名：张三").SetFontSize(FontSize));
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("保持不变的内容").SetFontSize(FontSize));
        }
        return File.ReadAllBytes(path);
    }

    // ── zip / xml 辅助 ────────────────────────────────────────────

    private static Dictionary<string, byte[]> ReadZipMap(byte[] ofd)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var e in za.Entries)
        {
            using var s = e.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            map[e.FullName] = buf.ToArray();
        }
        return map;
    }

    private static byte[] WriteZipMap(Dictionary<string, byte[]> map)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var kv in map)
            {
                var entry = za.CreateEntry(kv.Key);
                using var s = entry.Open();
                s.Write(kv.Value, 0, kv.Value.Length);
            }
        return ms.ToArray();
    }

    private static XDocument EntryXml(byte[] ofd, string entryName)
        => XDocument.Parse(Encoding.UTF8.GetString(ReadZipMap(ofd)[entryName]));

    private static XDocument PageXml(byte[] ofd, int pageNumber)
        => EntryXml(ofd, Resolve(ofd, GetPageBaseLoc(ofd, pageNumber)));

    private static string GetPageBaseLoc(byte[] ofd, int pageNumber)
    {
        var docXml = EntryXml(ofd, "Doc_0/Document.xml");
        var pagesEl = docXml.Descendants()
            .First(e => e.Name.LocalName == "Pages" && e.Parent?.Name.LocalName == "Document");
        return pagesEl.Elements().Where(e => e.Name.LocalName == "Page")
            .ElementAt(pageNumber - 1).Attribute("BaseLoc")!.Value;
    }

    private static string Resolve(byte[] ofd, string loc)
    {
        _ = ofd;
        var n = loc.Trim().TrimStart('/').Replace('\\', '/');
        return n.StartsWith("Doc_", StringComparison.OrdinalIgnoreCase) ? n : $"Doc_0/{n}";
    }

    /// <summary>取页面中正文包含 <paramref name="text"/> 的 TextObject 元素。</summary>
    private static XElement TextObjectOf(byte[] ofd, int pageNumber, string text)
        => PageXml(ofd, pageNumber).Descendants()
            .First(e => e.Name.LocalName == "TextObject" && e.Value.Contains(text, StringComparison.Ordinal));

    /// <summary>取页面中正文包含 <paramref name="text"/> 的 TextCode 元素。</summary>
    private static XElement TextCodeOf(byte[] ofd, int pageNumber, string text)
        => TextObjectOf(ofd, pageNumber, text).Elements()
            .First(e => e.Name.LocalName == "TextCode");

    private static List<string> PageTextCodes(byte[] ofd, int pageNumber)
        => PageXml(ofd, pageNumber).Descendants()
            .Where(e => e.Name.LocalName == "TextCode")
            .Select(e => e.Value).ToList();

    /// <summary>在指定页的匹配 TextObject 上追加 CGTransform 子节点（模拟带字形索引的文档）。</summary>
    private static byte[] InjectCgTransform(byte[] ofd, int pageNumber, string text)
    {
        var map = ReadZipMap(ofd);
        var loc = Resolve(ofd, GetPageBaseLoc(ofd, pageNumber));
        var doc = XDocument.Parse(Encoding.UTF8.GetString(map[loc]));
        var ns = doc.Root!.Name.Namespace;
        var textObj = doc.Descendants()
            .First(e => e.Name.LocalName == "TextObject" && e.Value.Contains(text, StringComparison.Ordinal));
        textObj.Add(new XElement(ns + "CGTransform",
            new XAttribute("CodePosition", "0"),
            new XAttribute("CodeCount", "1"),
            new XAttribute("Glyphs", "1")));
        map[loc] = Encoding.UTF8.GetBytes(doc.ToString(SaveOptions.DisableFormatting));
        return WriteZipMap(map);
    }

    // ──────────────────────────────────────────────────────────────

    /// <summary>全文档替换：所有页的匹配文本都应被替换。</summary>
    [Fact]
    public void ReplaceText_WholeDocument_ShouldReplaceOnEveryPage()
    {
        var ofd = BuildTwoPageOfd("DCR_All.ofd");

        var edited = DocContentReplace.ReplaceText(ofd, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：李四",
            ["Hello World"] = "Hi World",
        });

        Assert.Contains("姓名：李四", PageTextCodes(edited, 1));
        Assert.Contains("Hi World", PageTextCodes(edited, 1));
        Assert.Contains("姓名：李四", PageTextCodes(edited, 2));

        // 未被映射的文本原样保留
        Assert.Contains("保持不变的内容", PageTextCodes(edited, 2));

        // 往返可读（结构未被破坏）
        var tmp = Out("DCR_All_out.ofd");
        File.WriteAllBytes(tmp, edited);
        using var reader = new NetOfd.Reader.OFDReader(tmp);
        Assert.Equal(2, reader.GetPageCount());
        Assert.Contains("姓名：李四", reader.GetPageText(0));
        Assert.Contains("姓名：李四", reader.GetPageText(1));
    }

    /// <summary>指定页替换：仅目标页生效，其他页保持原样。</summary>
    [Fact]
    public void ReplaceText_SinglePage_ShouldOnlyAffectTargetPage()
    {
        var ofd = BuildTwoPageOfd("DCR_Page.ofd");

        var edited = DocContentReplace.ReplaceText(ofd, 1, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：王五",
        });

        Assert.Contains("姓名：王五", PageTextCodes(edited, 1));
        Assert.DoesNotContain("姓名：张三", PageTextCodes(edited, 1));
        // 第 2 页不受影响
        Assert.Contains("姓名：张三", PageTextCodes(edited, 2));
        Assert.DoesNotContain("姓名：王五", PageTextCodes(edited, 2));
    }

    /// <summary>
    /// 中文字宽为 1 em：DeltaX 应为 len-1 个「字号」值。
    /// 「姓名：李四」5 字、字号 5mm → 4 个 5。
    /// </summary>
    [Fact]
    public void ReplaceText_ShouldRecomputeDeltaX_Chinese()
    {
        var ofd = BuildTwoPageOfd("DCR_DxChs.ofd");

        var edited = DocContentReplace.ReplaceText(ofd, 1, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：李四",
        });

        var code = TextCodeOf(edited, 1, "姓名：李四");
        var dx = code.Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);
        Assert.Equal("5 5 5 5", dx);
    }

    /// <summary>
    /// ASCII 走内置宽度表（宋体系 0.5 em）：DeltaX = 0.5 × 字号。
    /// 「Hi World」8 字、字号 5mm → 7 个 2.5。
    /// </summary>
    [Fact]
    public void ReplaceText_ShouldRecomputeDeltaX_Ascii()
    {
        var ofd = BuildTwoPageOfd("DCR_DxAscii.ofd");

        var edited = DocContentReplace.ReplaceText(ofd, 1, new Dictionary<string, string>
        {
            ["Hello World"] = "Hi World",
        });

        var code = TextCodeOf(edited, 1, "Hi World");
        var dx = code.Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);

        var parts = dx!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Hi World".Length - 1, parts.Length);
        foreach (var p in parts)
            Assert.Equal(FontSize * 0.5, double.Parse(p, CultureInfo.InvariantCulture), 6);
    }

    /// <summary>未命中的文本：内容不变，且原有 DeltaX 保持不变（不被改动/追加）。</summary>
    [Fact]
    public void ReplaceText_UnmatchedText_ShouldStayUntouched()
    {
        var original = BuildTwoPageOfd("DCR_Unmatched_orig.ofd");
        var ofd = BuildTwoPageOfd("DCR_Unmatched.ofd");

        var edited = DocContentReplace.ReplaceText(ofd, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：李四",
        });

        var code = TextCodeOf(edited, 2, "保持不变的内容");
        Assert.Equal("保持不变的内容", code.Value);
        // 未命中的纯中文 run：写路径已按新语义生成 DeltaX（≥2 字符即写，对齐 ofdrw），
        // 替换未命中时应与原文件完全一致，不被改动/追加
        var codeOrig = TextCodeOf(original, 2, "保持不变的内容");
        Assert.Equal(codeOrig.Attribute("DeltaX")?.Value, code.Attribute("DeltaX")?.Value);

        // 未命中的 ASCII 行同样不动——写路径对半角 run 已生成 DeltaX（增量 H：每字符 0.5em 推进），
        // 替换未命中应保持原值不被改动/追加
        var ascii = TextCodeOf(edited, 1, "Hello World");
        Assert.Equal("Hello World", ascii.Value);
        var dx = ascii.Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);
        var parts = dx!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Hello World".Length - 1, parts.Length);
        foreach (var p in parts)
            Assert.Equal(FontSize * 0.5, double.Parse(p, CultureInfo.InvariantCulture), 6);
    }

    /// <summary>替换后应删除 TextObject 下的 CGTransform（替换字形在原子集中无码位）。</summary>
    [Fact]
    public void ReplaceText_ShouldRemoveCgTransform()
    {
        var ofd = BuildTwoPageOfd("DCR_Cg.ofd");
        var withCg = InjectCgTransform(ofd, 1, "姓名：张三");

        // 前置校验：注入确实生效
        Assert.Contains(TextObjectOf(withCg, 1, "姓名：张三").Elements(),
            e => e.Name.LocalName == "CGTransform");

        var edited = DocContentReplace.ReplaceText(withCg, 1, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：李四",
        });

        var textObj = TextObjectOf(edited, 1, "姓名：李四");
        Assert.DoesNotContain(textObj.Elements(), e => e.Name.LocalName == "CGTransform");
        // 文本与 DeltaX 仍正确写入
        Assert.Equal("姓名：李四", textObj.Elements().First(e => e.Name.LocalName == "TextCode").Value);
    }

    /// <summary>文件接口：返回被替换的 TextCode 数量，并写出结果文件。</summary>
    [Fact]
    public void ReplaceText_FilePath_ShouldReturnHitCountAndWriteFile()
    {
        var input = Out("DCR_File_in.ofd");
        File.WriteAllBytes(input, BuildTwoPageOfd("DCR_File.ofd"));
        var output = Out("DCR_File_out.ofd");

        // 全文档：两页各 1 处「姓名：张三」→ 2
        var hitsAll = DocContentReplace.ReplaceText(input, output, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：李四",
        });
        Assert.Equal(2, hitsAll);
        Assert.True(File.Exists(output));

        var written = File.ReadAllBytes(output);
        Assert.Contains("姓名：李四", PageTextCodes(written, 1));
        Assert.Contains("姓名：李四", PageTextCodes(written, 2));

        // 单页：仅第 2 页 1 处
        var out2 = Out("DCR_File_out2.ofd");
        var hitsOne = DocContentReplace.ReplaceText(input, out2, 2, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：赵六",
        });
        Assert.Equal(1, hitsOne);
        Assert.Contains("姓名：张三", PageTextCodes(File.ReadAllBytes(out2), 1));
        Assert.Contains("姓名：赵六", PageTextCodes(File.ReadAllBytes(out2), 2));
    }

    /// <summary>空映射应直接返回 0 且文本内容不变。</summary>
    [Fact]
    public void ReplaceText_EmptyMap_ShouldReturnZero()
    {
        var ofd = BuildTwoPageOfd("DCR_Empty.ofd");
        var input = Out("DCR_Empty_in.ofd");
        File.WriteAllBytes(input, ofd);
        var output = Out("DCR_Empty_out.ofd");

        var hits = DocContentReplace.ReplaceText(input, output, new Dictionary<string, string>());

        Assert.Equal(0, hits);
        var written = File.ReadAllBytes(output);
        Assert.Equal(PageTextCodes(ofd, 1), PageTextCodes(written, 1));
        Assert.Equal(PageTextCodes(ofd, 2), PageTextCodes(written, 2));
    }

    /// <summary>非法页码（0 / 负数 / 越界）应抛出参数异常。</summary>
    [Fact]
    public void ReplaceText_InvalidPageNumber_ShouldThrow()
    {
        var ofd = BuildTwoPageOfd("DCR_Range.ofd");

        Assert.Throws<ArgumentException>(() =>
            DocContentReplace.ReplaceText(ofd, 0, new Dictionary<string, string> { ["a"] = "b" }));
        Assert.Throws<ArgumentException>(() =>
            DocContentReplace.ReplaceText(ofd, -1, new Dictionary<string, string> { ["a"] = "b" }));
        Assert.Throws<ArgumentException>(() =>
            DocContentReplace.ReplaceText(ofd, 99, new Dictionary<string, string> { ["a"] = "b" }));
    }

    // ── 扩展选项（对应 Java ReplaceTextHandler / ExtractorFilter）──────────

    /// <summary>TextObject 过滤器：被过滤掉的文本对象整体不参与替换。</summary>
    [Fact]
    public void ReplaceText_WithTextObjectFilter_ShouldSkipFilteredObjects()
    {
        var ofd = BuildTwoPageOfd("DCR_Filter.ofd");

        var options = new ReplaceTextOptions
        {
            // 只处理含「姓名」的文本对象
            TextObjectFilter = e => e.Value.Contains("姓名", StringComparison.Ordinal),
        };

        var edited = DocContentReplace.ReplaceText(ofd, 1, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：李四",
            ["Hello World"] = "Hi World",
        }, options);

        Assert.Contains("姓名：李四", PageTextCodes(edited, 1));
        // 被过滤的 TextObject 原样保留（构建时已带半角 DeltaX，过滤不应改动/追加）
        Assert.Contains("Hello World", PageTextCodes(edited, 1));
        var dx2 = TextCodeOf(edited, 1, "Hello World").Attribute("DeltaX")?.Value;
        Assert.NotNull(dx2);
        var parts2 = dx2!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Hello World".Length - 1, parts2.Length);
        foreach (var p in parts2)
            Assert.Equal(FontSize * 0.5, double.Parse(p, CultureInfo.InvariantCulture), 6);
    }

    /// <summary>自定义度量字体：DeltaX 应按新字体的字宽表计算（Times New Roman 的 W = 0.944 em）。</summary>
    [Fact]
    public void ReplaceText_WithFontResolver_ShouldUseProvidedMetrics()
    {
        var ofd = BuildTwoPageOfd("DCR_FontHook.ofd");
        using var tnr = new NetOfd.Font.OFDFont("Times New Roman", null);

        var options = new ReplaceTextOptions { FontResolver = (_, _) => tnr };
        var edited = DocContentReplace.ReplaceText(ofd, 1, new Dictionary<string, string>
        {
            ["Hello World"] = "WW",
        }, options);

        // 默认（宋体 0.5 em）应为 2.5；改用 Times New Roman 后 W = 0.944 × 5 = 4.72
        var dx = TextCodeOf(edited, 1, "WW").Attribute("DeltaX")?.Value;
        Assert.Equal("4.72", dx);
    }

    /// <summary>自定义 CGTransform 生成：替换后应写入新的 CGTransform（而非默认删除）。</summary>
    [Fact]
    public void ReplaceText_WithCgTransformFactory_ShouldAppendNewCgTransform()
    {
        var ofd = BuildTwoPageOfd("DCR_CgHook.ofd");

        var options = new ReplaceTextOptions
        {
            CgTransformFactory = (textObj, _) => new XElement(textObj.Name.Namespace + "CGTransform",
                new XAttribute("CodePosition", "0"),
                new XAttribute("CodeCount", "2"),
                new XAttribute("Glyphs", "7 8")),
        };

        var edited = DocContentReplace.ReplaceText(ofd, 1, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：李四",
        }, options);

        var textObj = TextObjectOf(edited, 1, "姓名：李四");
        var cgList = textObj.Elements().Where(e => e.Name.LocalName == "CGTransform").ToList();
        Assert.Single(cgList);
        Assert.Equal("7 8", cgList[0].Attribute("Glyphs")?.Value);
    }

    /// <summary>
    /// 字体声明在页级资源（PageRes.xml）中时仍应能被解析到：
    /// 解析成功 → 走宋体 ASCII 宽表（0.5 em）；解析失败 → 回退 1.0 em。
    /// </summary>
    [Fact]
    public void ReplaceText_FontInPageRes_ShouldStillResolveFont()
    {
        var ofd = MoveFontToPageRes(BuildTwoPageOfd("DCR_PageRes.ofd"));

        // 前置校验：PublicRes 中已无字体声明
        var pub = EntryXml(ofd, "Doc_0/PublicRes.xml");
        Assert.DoesNotContain(pub.Descendants(), e => e.Name.LocalName == "Font");

        var edited = DocContentReplace.ReplaceText(ofd, 1, new Dictionary<string, string>
        {
            ["Hello World"] = "Hi World",
        });

        var dx = TextCodeOf(edited, 1, "Hi World").Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);
        var parts = dx!.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("Hi World".Length - 1, parts.Length);
        foreach (var p in parts)
            Assert.Equal(FontSize * 0.5, double.Parse(p, CultureInfo.InvariantCulture), 6);
    }

    /// <summary>把 PublicRes.xml 中的字体声明搬到 Doc_0/PageRes.xml（模拟字体误置于页级资源的文档）。</summary>
    private static byte[] MoveFontToPageRes(byte[] ofd)
    {
        const string pubPath = "Doc_0/PublicRes.xml";
        var map = ReadZipMap(ofd);
        var pub = XDocument.Parse(Encoding.UTF8.GetString(map[pubPath]));
        var ns = pub.Root!.Name.Namespace;
        var fonts = pub.Root.Element(ns + "Fonts")
                    ?? throw new InvalidOperationException("样例 PublicRes 缺少 Fonts 节点");
        var font = fonts.Element(ns + "Font")
                   ?? throw new InvalidOperationException("样例 PublicRes 缺少 Font 节点");
        font.Remove();
        map[pubPath] = Encoding.UTF8.GetBytes(pub.ToString(SaveOptions.DisableFormatting));

        var pageRes = new XElement(ns + "Res", new XElement(ns + "Fonts", font));
        map["Doc_0/PageRes.xml"] =
            Encoding.UTF8.GetBytes(new XDocument(pageRes).ToString(SaveOptions.DisableFormatting));
        return WriteZipMap(map);
    }

    /// <summary>原文与替换值相同时视为未变更，不计入命中数。</summary>
    [Fact]
    public void ReplaceText_SameValue_ShouldNotCountAsHit()
    {
        var input = Out("DCR_Same_in.ofd");
        File.WriteAllBytes(input, BuildTwoPageOfd("DCR_Same.ofd"));
        var output = Out("DCR_Same_out.ofd");

        var hits = DocContentReplace.ReplaceText(input, output, new Dictionary<string, string>
        {
            ["姓名：张三"] = "姓名：张三",
        });

        Assert.Equal(0, hits);
    }
}
