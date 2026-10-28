using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Layout;
using NetOfd.Pkg.Container;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 覆盖 GB/T 33190-2016 §7.1 与 GB/T 48666-2026 §6.2 的命名空间声明要求：
/// 「XML文档使用的命名空间为 http://www.ofdspec.org/2016，其表示符应为 ofd；
///  应在包内各XML文档的根节点申明 defaults:ofd。」
/// <para>
/// 本测试类锁死三条<b>实测得出</b>的关键事实（均为 .NET 10 实测，非推测）：
/// </para>
/// <list type="number">
/// <item>
/// LINQ-to-XML <b>无法</b>表达「同一命名空间的两个前缀」：对根元素追加
/// <c>xmlns:defaults</c> 声明后序列化，元素前缀会被改写为 <c>defaults:</c>
/// （属性名亦变成 <c>defaults:Version</c>）——故声明必须在<b>文本层</b>注入。
/// </item>
/// <item>
/// 已带 <c>xmlns:defaults</c> 的文档经 LINQ 解析再序列化，同样会被改写前缀
/// ——故签名等 LINQ 重写路径需在序列化后做前缀还原。
/// </item>
/// <item>
/// 根节点只声明 <c>xmlns:defaults</c>（无 <c>xmlns:ofd</c>）时，前缀还原会产生
/// 「未声明前缀」错误（实测 <c>'ofd' is an undeclared prefix</c>）——须跳过。
/// </item>
/// </list>
/// </summary>
public class DefaultsNamespaceTest
{
    private const string Ns = "http://www.ofdspec.org/2016";

    // ────────────────────────────────────────────────────────
    //  一、InjectDefaultsDeclaration：文本层注入
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Inject_PrefixForm_RootGetsDefaultsDeclaration()
    {
        var xml = "<ofd:Document xmlns:ofd=\"" + Ns + "\"><ofd:CommonData/></ofd:Document>";
        var patched = OFDElement.InjectDefaultsDeclaration(xml, Ns);

        Assert.Contains("xmlns:defaults=\"" + Ns + "\"", patched);
        // 元素前缀必须保持 ofd:
        Assert.Contains("<ofd:Document", patched);
        Assert.DoesNotContain("<defaults:", patched);
    }

    [Fact]
    public void Inject_DefaultNamespaceForm_RootGetsDefaultsDeclaration()
    {
        // 默认命名空间形式（xmlns="..."）同样需补声明
        var xml = "<Res xmlns=\"" + Ns + "\"><Font/></Res>";
        var patched = OFDElement.InjectDefaultsDeclaration(xml, Ns);

        Assert.Contains("xmlns:defaults=\"" + Ns + "\"", patched);
        // 原有默认声明须保留（不得被改写为 xmlns:ofd）
        Assert.Contains("xmlns=\"" + Ns + "\"", patched);
    }

    [Fact]
    public void Inject_IsIdempotent()
    {
        var xml = "<ofd:Document xmlns:ofd=\"" + Ns + "\"/>";
        var once = OFDElement.InjectDefaultsDeclaration(xml, Ns);
        var twice = OFDElement.InjectDefaultsDeclaration(once, Ns);

        Assert.Equal(once, twice);
        // 只应有一处 defaults 声明
        Assert.Equal(1, CountOccurrences(twice, "xmlns:defaults="));
    }

    [Fact]
    public void Inject_SelfClosingRoot_DeclarationPlacedBeforeSlash()
    {
        var xml = "<ofd:Page xmlns:ofd=\"" + Ns + "\"/>";
        var patched = OFDElement.InjectDefaultsDeclaration(xml, Ns);

        Assert.Contains("xmlns:defaults=\"" + Ns + "\"", patched);
        // 声明须插在自闭合斜杠之前，保持 XML 合法
        Assert.Contains("xmlns:defaults=\"" + Ns + "\"/>", patched);
        Assert.DoesNotContain("/ xmlns:defaults", patched);
    }

    [Fact]
    public void Inject_NonOfdNamespace_Skipped()
    {
        // WS/T 500 共享文档（hl7 命名空间）不应被注入
        var xml = "<ClinicalDocument xmlns=\"urn:hl7-org:v3\"><Content/></ClinicalDocument>";
        var patched = OFDElement.InjectDefaultsDeclaration(xml, Ns);

        Assert.Equal(xml, patched);
        Assert.DoesNotContain("xmlns:defaults", patched);
    }

    [Fact]
    public void Inject_SkipsXmlDeclarationAndComments()
    {
        var xml = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<!-- 注释 -->\n" +
                  "<ofd:Document xmlns:ofd=\"" + Ns + "\"/>";
        var patched = OFDElement.InjectDefaultsDeclaration(xml, Ns);

        Assert.Contains("xmlns:defaults=\"" + Ns + "\"", patched);
        // 注释须保留
        Assert.Contains("<!-- 注释 -->", patched);
        // 声明须落在根元素上而非注释里
        Assert.Contains("<ofd:Document xmlns:ofd=\"" + Ns + "\" xmlns:defaults=", patched);
    }

    [Fact]
    public void Inject_EmptyOrNull_ReturnsUnchanged()
    {
        Assert.Equal("", OFDElement.InjectDefaultsDeclaration("", Ns));
        Assert.Equal("abc", OFDElement.InjectDefaultsDeclaration("abc", Ns));
    }

    [Fact]
    public void InjectBytes_NonUtf8OrEmpty_ReturnsUnchanged()
    {
        var empty = Array.Empty<byte>();
        Assert.Same(empty, OFDElement.InjectDefaultsDeclarationBytes(empty, Ns));

        // 非 XML 内容原样返回（不抛异常）
        var junk = Encoding.UTF8.GetBytes("not xml at all");
        Assert.Equal(junk, OFDElement.InjectDefaultsDeclarationBytes(junk, Ns));
    }

    [Fact]
    public void InjectToMap_OnlyTouchesOfdXmlEntries()
    {
        var map = new Dictionary<string, byte[]>
        {
            ["OFD.xml"] = Encoding.UTF8.GetBytes("<ofd:OFD xmlns:ofd=\"" + Ns + "\"/>"),
            ["Doc_0/Document.xml"] = Encoding.UTF8.GetBytes("<ofd:Document xmlns:ofd=\"" + Ns + "\"/>"),
            ["Doc_0/Tags/CustomTag_1.xml"] = Encoding.UTF8.GetBytes("<ClinicalDocument xmlns=\"urn:hl7-org:v3\"/>"),
            ["Doc_0/font.ttf"] = new byte[] { 1, 2, 3 },
        };
        int changed = OFDElement.InjectDefaultsDeclarationToMap(map, Ns);

        Assert.Equal(2, changed);
        Assert.Contains("xmlns:defaults", Encoding.UTF8.GetString(map["OFD.xml"]));
        Assert.Contains("xmlns:defaults", Encoding.UTF8.GetString(map["Doc_0/Document.xml"]));
        // 非 OFD 命名空间与二进制不得被改动
        Assert.DoesNotContain("xmlns:defaults", Encoding.UTF8.GetString(map["Doc_0/Tags/CustomTag_1.xml"]));
        Assert.Equal(new byte[] { 1, 2, 3 }, map["Doc_0/font.ttf"]);
    }

    // ────────────────────────────────────────────────────────
    //  二、RestoreOfdPrefix：LINQ 重写后的前缀还原
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Restore_DefaultsPrefixedElements_BecomeOfdPrefixed()
    {
        var xml = "<defaults:OFD xmlns:ofd=\"" + Ns + "\" xmlns:defaults=\"" + Ns + "\">" +
                  "<defaults:DocBody/></defaults:OFD>";
        var patched = OFDElement.RestoreOfdPrefix(xml);

        Assert.Contains("<ofd:OFD", patched);
        Assert.Contains("<ofd:DocBody", patched);
        Assert.DoesNotContain("<defaults:", patched);
        // 命名空间声明须保留
        Assert.Contains("xmlns:defaults=\"" + Ns + "\"", patched);
    }

    [Fact]
    public void Restore_WithoutOfdDeclaration_Skipped()
    {
        // 仅有 xmlns:defaults 声明时还原会造成未声明前缀错误 → 必须跳过
        var xml = "<defaults:Signature xmlns:defaults=\"" + Ns + "\"><defaults:SignedInfo/></defaults:Signature>";
        var patched = OFDElement.RestoreOfdPrefix(xml);

        Assert.Equal(xml, patched);
        Assert.Contains("<defaults:Signature", patched);
    }

    [Fact]
    public void Restore_NoDefaultsDeclaration_Skipped()
    {
        var xml = "<ofd:OFD xmlns:ofd=\"" + Ns + "\"/>";
        Assert.Equal(xml, OFDElement.RestoreOfdPrefix(xml));
    }

    [Fact]
    public void Restore_ResultMustRemainWellFormedXml()
    {
        var xml = "<defaults:OFD xmlns:ofd=\"" + Ns + "\" xmlns:defaults=\"" + Ns + "\">" +
                  "<defaults:DocBody><defaults:Signatures Loc=\"/x\"/></defaults:DocBody></defaults:OFD>";
        var patched = OFDElement.RestoreOfdPrefix(xml);

        // 还原后必须仍是合法 XML（此前缺陷：'ofd' is an undeclared prefix）
        var doc = XDocument.Parse(patched);
        var ofdNs = XNamespace.Get(Ns);
        Assert.Equal("OFD", doc.Root!.Name.LocalName);
        Assert.Equal(Ns, doc.Root.Name.NamespaceName);
        Assert.Equal("Signatures", doc.Root.Element(ofdNs + "DocBody")!.Element(ofdNs + "Signatures")!.Name.LocalName);
    }
    [Fact]
    public void LinqToXml_CannotExpressDualPrefix_ThisIsWhyTextInjectionIsRequired()
    {
        // 锁定事实：LINQ-to-XML 追加 xmlns:defaults 会把元素前缀改写为 defaults:
        // ——这正是必须走文本层注入的原因（若某天 LINQ 行为改变，本测试会失败提醒复核）。
        var root = new XElement(XNamespace.Get(Ns) + "OFD",
            new XAttribute(XNamespace.Get(Ns) + "Version", "1.2"),
            new XAttribute(XNamespace.Xmlns + "ofd", Ns),
            new XAttribute(XNamespace.Xmlns + "defaults", Ns));

        var serialized = root.ToString(SaveOptions.DisableFormatting);
        Assert.Contains("<defaults:OFD", serialized);
        Assert.DoesNotContain("<ofd:OFD", serialized);
    }

    // ────────────────────────────────────────────────────────
    //  三、库端收口：容器层产出的 OFD 全量带声明
    // ────────────────────────────────────────────────────────

    [Fact]
    public void ContainerPath_GeneratedOfd_AllOfdXmlHasDefaultsDeclaration()
    {
        var path = Path.Combine(Path.GetTempPath(), "netofd_defaults_container.ofd");
        if (File.Exists(path)) File.Delete(path);

        using (var doc = new OFDDoc(path))
        {
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("默认命名空间声明探针").SetFontSize(9d));
            doc.Close();
        }

        var entries = ReadEntries(path);
        var ofdXmls = entries.Where(e => e.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.NotEmpty(ofdXmls);

        foreach (var (name, content) in ofdXmls)
        {
            var text = Encoding.UTF8.GetString(content);
            // 非 OFD 命名空间的条目（自定义标引等）不要求 defaults 声明
            if (!text.Contains(Ns)) continue;

            Assert.True(text.Contains("xmlns:defaults=\"" + Ns + "\""),
                $"条目 {name} 缺少 xmlns:defaults 声明");
            // 元素前缀必须是 ofd:（不得被改写为 defaults:）
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(text, @"<\s*/?defaults:"),
                $"条目 {name} 的元素前缀被改写为 defaults:");
        }

        File.Delete(path);
    }

    [Fact]
    public void ContainerPath_GeneratedOfd_RootNamespaceDeclaresBothPrefixes()
    {
        var path = Path.Combine(Path.GetTempPath(), "netofd_defaults_root.ofd");
        if (File.Exists(path)) File.Delete(path);

        using (var doc = new OFDDoc(path))
        {
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("根节点声明探针").SetFontSize(9d));
            doc.Close();
        }

        var entries = ReadEntries(path);
        var ofdRoot = Encoding.UTF8.GetString(entries["OFD.xml"]);

        Assert.Contains("xmlns:ofd=\"" + Ns + "\"", ofdRoot);
        Assert.Contains("xmlns:defaults=\"" + Ns + "\"", ofdRoot);
        // 根元素须用 ofd: 前缀
        Assert.Contains("<ofd:OFD", ofdRoot);

        File.Delete(path);
    }

    [Fact]
    public void ContainerPath_ElementsRemainWellFormed()
    {
        var path = Path.Combine(Path.GetTempPath(), "netofd_defaults_wellformed.ofd");
        if (File.Exists(path)) File.Delete(path);

        using (var doc = new OFDDoc(path))
        {
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("XML 合法性探针").SetFontSize(9d));
            doc.Close();
        }

        foreach (var (name, content) in ReadEntries(path))
        {
            if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var text = Encoding.UTF8.GetString(content);
            if (!text.Contains(Ns)) continue;

            // 注入声明后必须仍是合法 XML（否则阅读器无法解析）
            var exception = Record.Exception(() => XDocument.Parse(text));
            Assert.True(exception == null, $"条目 {name} 注入声明后 XML 非法：{exception?.Message}");
        }

        File.Delete(path);
    }

    // ────────────────────────────────────────────────────────
    //  辅助
    // ────────────────────────────────────────────────────────

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    private static Dictionary<string, byte[]> ReadEntries(string ofdPath)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var za = ZipFile.OpenRead(ofdPath);
        foreach (var e in za.Entries)
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            map[e.FullName] = ms.ToArray();
        }
        return map;
    }
}
