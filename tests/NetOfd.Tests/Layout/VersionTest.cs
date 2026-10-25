using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.Versions;
using NetOfd.Layout.Edit;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 版本管理（Versions）功能测试。
/// 对应 Java: ofdrw-core versions 包（GB/T 33190-2016 第 19 章）
/// </summary>
public class VersionTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_ver_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>构建单页样例文档并返回字节。</summary>
    private static byte[] BuildSampleOfdBytes(string name, string text = "版本测试样本")
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText(text).SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    private static byte[]? ReadEntry(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var e = za.GetEntry(entryName);
        if (e == null) return null;
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    private static XDocument EntryXml(byte[] ofd, string entryName)
        => XDocument.Parse(Encoding.UTF8.GetString(ReadEntry(ofd, entryName)!));

    private static List<string> ListEntries(byte[] ofd)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        return za.Entries.Select(e => e.FullName.Replace('\\', '/')).ToList();
    }

    /// <summary>Lambda 绘制器。</summary>
    private class LambdaDrawer : NetOfd.Layout.Element.Canvas.IDrawer
    {
        private readonly Action<NetOfd.Layout.Element.Canvas.DrawContext> _draw;
        public LambdaDrawer(Action<NetOfd.Layout.Element.Canvas.DrawContext> draw) => _draw = draw;
        public void Draw(NetOfd.Layout.Element.Canvas.DrawContext ctx) => _draw(ctx);
    }

    // ──────────────────────────────────────────────────────────────

    /// <summary>创建快照应生成版本描述文件、复制文档目录并登记入口。</summary>
    [Fact]
    public void AddVersion_ShouldCreateSnapshotStructure()
    {
        var ofd = BuildSampleOfdBytes("Ver_Basic.ofd");

        var versioned = OfdVersioner.AddVersion(ofd, "初稿", out var index);

        Assert.Equal(1, index);

        // OFD.xml 登记版本入口（当前版本）
        var ofdXml = EntryXml(versioned, "OFD.xml");
        var verEl = ofdXml.Descendants().First(e => e.Name.LocalName == "Version");
        Assert.Equal("v1", verEl.Attribute("ID")?.Value);
        Assert.Equal("1", verEl.Attribute("Index")?.Value);
        Assert.Equal("true", verEl.Attribute("Current")?.Value);
        Assert.Equal("Doc_0/Versions/V_1/DocVersion.xml", verEl.Attribute("BaseLoc")?.Value);

        // 版本描述文件内容
        var docVersion = new DocVersion(EntryXml(versioned, "Doc_0/Versions/V_1/DocVersion.xml").Root!);
        Assert.Equal("v1", docVersion.ID);
        Assert.Equal("初稿", docVersion.Name);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), docVersion.CreationDate);
        // 路径为包根绝对写法（前导 "/"），严格阅读器不会按描述文件目录二次拼接
        Assert.Equal("/Doc_0/V_1/Document.xml", docVersion.DocRoot!.Value);
        var files = docVersion.FileList!.GetFiles();
        Assert.NotEmpty(files);
        // 文件列表包含复制的 Document.xml 且包内存在
        Assert.Contains(files, f => f.File.Value == "/Doc_0/V_1/Document.xml");
        // 文件标识必须是 XML NCName（首字符为字母），纯数字 ID 会被严格阅读器判非法
        Assert.All(files, f => Assert.Matches("^[A-Za-z_][A-Za-z0-9_.-]*$", f.ID));
        Assert.Equal(files.Count, files.Select(f => f.ID).Distinct().Count());
        Assert.NotNull(ReadEntry(versioned, "Doc_0/V_1/Document.xml"));
        Assert.NotNull(ReadEntry(versioned, "Doc_0/V_1/Pages/Page_0/Content.xml"));
    }

    /// <summary>多次快照应递增版本号且仅最新版本为默认。</summary>
    [Fact]
    public void AddVersion_Twice_IndexAndCurrent()
    {
        var ofd = BuildSampleOfdBytes("Ver_Twice.ofd");
        var v1 = OfdVersioner.AddVersion(ofd, "初稿", out var i1);
        var v2 = OfdVersioner.AddVersion(v1, "修订稿", out var i2);

        Assert.Equal(1, i1);
        Assert.Equal(2, i2);

        var versions = new Versions(EntryXml(v2, "OFD.xml")
            .Descendants().First(e => e.Name.LocalName == "Versions"));
        var list = versions.GetVersions();
        Assert.Equal(2, list.Count);
        Assert.False(list[0].Current, "旧版本不应再是默认版本");
        Assert.True(list[1].Current, "最新版本应为默认版本");
        Assert.NotNull(ReadEntry(v2, "Doc_0/Versions/V_2/DocVersion.xml"));

        // 快照不嵌套：V_2 不包含 V_1 的副本
        Assert.Null(ReadEntry(v2, "Doc_0/V_2/V_1/Document.xml"));
    }

    /// <summary>读取版本入口与版本描述文件。</summary>
    [Fact]
    public void GetVersions_And_LoadDocVersion()
    {
        var ofd = BuildSampleOfdBytes("Ver_Read.ofd");
        var versioned = OfdVersioner.AddVersion(ofd, "审核稿", out _);

        var entries = OfdVersioner.GetVersions(versioned);
        var entry = Assert.Single(entries);
        Assert.Equal("v1", entry.ID);
        Assert.True(entry.Current);

        var docVersion = OfdVersioner.LoadDocVersion(versioned, "v1");
        Assert.Equal("审核稿", docVersion.Name);
        Assert.NotNull(docVersion.FileList);

        // 不存在的版本应抛出异常
        Assert.Throws<InvalidOperationException>(() =>
            OfdVersioner.LoadDocVersion(versioned, "不存在"));
    }

    /// <summary>提取历史版本应为独立文档且不受后续编辑影响。</summary>
    [Fact]
    public void ExtractVersion_ShouldIsolateOldState()
    {
        var ofd = BuildSampleOfdBytes("Ver_Extract.ofd");
        var v1 = OfdVersioner.AddVersion(ofd, "初稿", out _);

        // 快照后对活动文档添加注释
        var annotated = OfdAnnotator.AddAnnotation(v1, 1,
            new Annotation(10, 10, 40, 16, NetOfd.Core.Annotation.PageAnnot.AnnotType.Path,
                new LambdaDrawer(ctx => ctx.FillText("批注", 3, 10))));

        // 提取初稿：不含注释；提取当前状态（新快照）：含注释
        var oldState = OfdVersioner.ExtractVersion(annotated, "v1");
        Assert.DoesNotContain(ListEntries(oldState), n => n.Contains("/Annots/", StringComparison.OrdinalIgnoreCase));
        Assert.Null(ReadEntry(oldState, "Doc_0/Annots/Annotations.xml"));
        Assert.NotNull(ReadEntry(oldState, "Doc_0/Document.xml"));

        // 提取出的文档结构完整：OFD.xml 指向版本入口，页面存在
        var extractedOfdXml = EntryXml(oldState, "OFD.xml");
        Assert.Equal("Doc_0/Document.xml",
            extractedOfdXml.Descendants().First(e => e.Name.LocalName == "DocRoot").Value);
        Assert.Contains(extractedOfdXml.Descendants(), e => e.Name.LocalName == "DocInfo");

        // 当前状态再次快照后提取，应包含注释（快照目录段已剥离）
        var v2 = OfdVersioner.AddVersion(annotated, "批注稿", out _);
        var newState = OfdVersioner.ExtractVersion(v2, "v2");
        Assert.NotNull(ReadEntry(newState, "Doc_0/Annots/Annotations.xml"));
    }

    /// <summary>版本模型序列化往返（对齐 Java DocVersionTest 用例）。</summary>
    [Fact]
    public void DocVersion_ModelRoundtrip()
    {
        var fileList = new FileList()
            .AddFile("12", new STLoc("./Res/VersionFile_1.xml"))
            .AddFile("13", new STLoc("./Res/VersionFile_2.xml"));
        var model = new DocVersion("77", fileList)
        {
            Version = "1.0",
            Name = "MyVer",
            CreationDate = new DateOnly(2026, 8, 21),
            DocRoot = new STLoc("./Res/DocRoot.xml")
        };

        var parsed = new DocVersion(XDocument.Parse(
            Encoding.UTF8.GetString(OfdAnnotator.ToUtf8Bytes(model.Element))).Root!);

        Assert.Equal("77", parsed.ID);
        Assert.Equal("1.0", parsed.Version);
        Assert.Equal("MyVer", parsed.Name);
        Assert.Equal(new DateOnly(2026, 8, 21), parsed.CreationDate);
        Assert.Equal("./Res/DocRoot.xml", parsed.DocRoot!.Value);
        var files = parsed.FileList!.GetFiles();
        Assert.Equal(2, files.Count);
        Assert.Equal("12", files[0].ID);
        Assert.Equal("./Res/VersionFile_1.xml", files[0].File.Value);
        Assert.Equal("13", files[1].ID);
        Assert.Equal("./Res/VersionFile_2.xml", files[1].File.Value);
    }
}
