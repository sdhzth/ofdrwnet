using System.Text;
using System.Xml.Linq;
using NetOfd.Pkg.Tool;
using Xunit;

namespace NetOfd.Tests.Pkg;

/// <summary>
/// 增量 W：ElemCup（XML 统一序列化）+ OFDNameSpaceModifier（命名空间升级）回归测试。
/// </summary>
public class ElemCupTest : IDisposable
{
    private static readonly XNamespace OfdNs = "http://www.ofdspec.org/2016";
    private readonly bool _savedDebugPrint = ElemCup.EnableDebugPrint;
    private readonly string _tempDir = Path.Combine(
        Path.GetTempPath(), "netofd_elemcup_" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        ElemCup.EnableDebugPrint = _savedDebugPrint;
        if (Directory.Exists(_tempDir)) Directory.Delete(_tempDir, recursive: true);
        GC.SuppressFinalize(this);
    }

    // ────────────────────────────────────────────────────────
    //  ElemCup 序列化
    // ────────────────────────────────────────────────────────

    [Fact]
    public void ToXml_AlwaysEmitsXmlDeclaration()
    {
        var el = new XElement("Page", new XElement("Area"));
        var xml = ElemCup.ToXml(el);

        Assert.NotNull(xml);
        Assert.StartsWith("<?xml version=\"1.0\" encoding=\"UTF-8\"?>", xml);
        Assert.Null(ElemCup.ToXml(null));
    }

    [Fact]
    public void EnableDebugPrintSwitchesFormatting()
    {
        var el = new XElement("Page", new XElement("Area", new XElement("Box")));

        ElemCup.EnableDebugPrint = false;
        var compact = ElemCup.ToXml(el)!;
        Assert.DoesNotContain("\n", compact);

        ElemCup.EnableDebugPrint = true;
        var pretty = ElemCup.ToXml(el)!;
        Assert.Contains("\n", pretty);
    }

    [Fact]
    public void DumpBytesIsUtf8WithoutBom()
    {
        var el = new XElement("OFD");
        var bytes = ElemCup.Dump(el);

        Assert.NotNull(bytes);
        Assert.Null(ElemCup.Dump(null));

        // UTF-8 无 BOM：开头不能是 EF BB BF
        Assert.False(bytes!.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        Assert.Equal("<?", Encoding.UTF8.GetString(bytes, 0, 2));
    }

    [Fact]
    public void DumpToFileCreatesParentDirectory()
    {
        // 目标文件的父目录不存在，应自动创建
        var target = Path.Combine(_tempDir, "sub", "deep", "Page.xml");
        ElemCup.Dump(new XElement("Page"), target);

        Assert.True(File.Exists(target));
        Assert.StartsWith("<?xml", File.ReadAllText(target));
    }

    [Fact]
    public void DumpToEmptyPathThrows()
    {
        Assert.Throws<ArgumentException>(() => ElemCup.Dump(new XElement("Page"), ""));
        Assert.Throws<ArgumentException>(() => ElemCup.DumpUpNs(new XElement("Page"), null!));
    }

    [Fact]
    public void InjectRoundTripsThroughFile()
    {
        var target = Path.Combine(_tempDir, "Round.xml");
        ElemCup.Dump(new XElement(OfdNs + "Page",
            new XElement(OfdNs + "Area", new XAttribute("PhysicalBox", "0 0 210 297"))), target);

        var back = ElemCup.Inject(target);
        Assert.NotNull(back);
        Assert.Equal("Page", back!.Name.LocalName);
        Assert.Equal("0 0 210 297", back.Element(OfdNs + "Area")?.Attribute("PhysicalBox")?.Value);

        // 不存在的路径返回 null 而不抛异常
        Assert.Null(ElemCup.Inject(Path.Combine(_tempDir, "Nope.xml")));
        Assert.Null(ElemCup.Inject((Stream?)null));
    }

    // ────────────────────────────────────────────────────────
    //  OFDNameSpaceModifier 升级
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Modifier_UpgradesNamespaceLessTreeToOfd()
    {
        // 第三方工具生成的"无命名空间"OFD 片段
        var el = XElement.Parse("<Page><Area PhysicalBox=\"0 0 210 297\"><Box/></Area></Page>");
        Assert.Equal(XNamespace.None, el.Name.Namespace);

        new OFDNameSpaceModifier().Upgrade(el);

        Assert.Equal(OfdNs, el.Name.Namespace);
        Assert.Equal("Page", el.Name.LocalName);
        var area = el.Element(OfdNs + "Area");
        Assert.NotNull(area);
        Assert.Equal(OfdNs, area!.Element(OfdNs + "Box")!.Name.Namespace);
    }

    [Fact]
    public void Modifier_IsIdempotent()
    {
        var el = XElement.Parse("<Page><Area/></Page>");
        var modifier = new OFDNameSpaceModifier();

        modifier.Upgrade(el);
        string first = el.ToString(SaveOptions.DisableFormatting);

        modifier.Upgrade(el);
        string second = el.ToString(SaveOptions.DisableFormatting);

        Assert.Equal(first, second);
    }

    [Fact]
    public void Modifier_ReplacesWrongNamespace()
    {
        var wrong = XElement.Parse(
            "<Page xmlns=\"http://bad.example.com/ofd\"><Area/></Page>");

        new OFDNameSpaceModifier().Upgrade(wrong);

        Assert.Equal(OfdNs, wrong.Name.Namespace);
        Assert.NotNull(wrong.Element(OfdNs + "Area"));

        // 错误的命名空间声明不应残留
        Assert.DoesNotContain("bad.example.com", wrong.ToString(SaveOptions.DisableFormatting));
    }

    [Fact]
    public void Modifier_StripsNamespaceFromAttributes()
    {
        var el = new XElement("Page",
            new XAttribute(XNamespace.Xmlns + "ofd", OfdNs.NamespaceName),
            new XAttribute(OfdNs + "ID", "1"));

        new OFDNameSpaceModifier().Upgrade(el);

        var id = el.Attribute("ID");
        Assert.NotNull(id);
        Assert.Equal("1", id!.Value);
        Assert.Equal(XNamespace.None, id.Name.Namespace);
    }

    [Fact]
    public void Modifier_UpgradesWholeDocument()
    {
        var doc = XDocument.Parse("<Page><Area/></Page>");
        new OFDNameSpaceModifier().Upgrade(doc);

        Assert.Equal(OfdNs, doc.Root!.Name.Namespace);

        // null 安全：不抛异常
        new OFDNameSpaceModifier().Upgrade((XDocument?)null);
        new OFDNameSpaceModifier().Upgrade((XElement?)null);
    }

    // ────────────────────────────────────────────────────────
    //  DumpUpNs
    // ────────────────────────────────────────────────────────

    [Fact]
    public void DumpUpNs_WritesOfdNamespaceXml()
    {
        var target = Path.Combine(_tempDir, "Upgraded.xml");
        var source = XElement.Parse("<Page><Area PhysicalBox=\"0 0 210 297\"/></Page>");

        ElemCup.DumpUpNs(source, target);

        var written = XElement.Load(target);
        Assert.Equal(OfdNs, written.Name.Namespace);
        Assert.NotNull(written.Element(OfdNs + "Area"));
    }

    [Fact]
    public void DumpUpNs_DoesNotMutateSourceElement()
    {
        // 升级必须发生在克隆体上，否则会污染调用方内存中的文档树
        var target = Path.Combine(_tempDir, "NoMutate.xml");
        var source = XElement.Parse("<Page><Area/></Page>");

        ElemCup.DumpUpNs(source, target);

        Assert.Equal(XNamespace.None, source.Name.Namespace);
        Assert.Null(source.Element(OfdNs + "Area"));
        Assert.NotNull(source.Element("Area"));
    }

    [Fact]
    public void CleanerAndModifierAreComplementary()
    {
        // NameSpaceCleaner 降级（去 NS，便于宽松读取）→ Modifier 升级（补回 OFD NS，便于合规写回）
        var el = XElement.Parse(
            "<ofd:Page xmlns:ofd=\"http://www.ofdspec.org/2016\"><ofd:Area/></ofd:Page>");

        NetOfd.Reader.Tools.NameSpaceCleaner.Clean(el);
        Assert.Equal(XNamespace.None, el.Name.Namespace);

        new OFDNameSpaceModifier().Upgrade(el);
        Assert.Equal(OfdNs, el.Name.Namespace);
        Assert.NotNull(el.Element(OfdNs + "Area"));
    }
}
