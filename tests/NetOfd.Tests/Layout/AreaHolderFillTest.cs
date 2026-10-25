using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 区域占位区块编辑填充测试：向 Java 官方登记样本 areaholder_fields.ofd
/// （AreaHolderBlocks.xml：name→PageBlock 10，birthday→PageBlock 21）填充内容。
/// 对应 Java: org.ofdrw.layout.areaholder.AreaHolderContext + AreaHolderContextTest
/// </summary>
public class AreaHolderFillTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_areaholder_fill_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private static string SamplePath()
        => Path.Combine(AppContext.BaseDirectory, "Resources", "areaholder_fields.ofd");

    private static byte[] Sample() => File.ReadAllBytes(SamplePath());

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

    private static XElement PageBlock(byte[] ofd, string id)
    {
        var content = EntryXml(ofd, "Doc_0/Pages/Page_0/Content.xml");
        return content.Descendants().First(e =>
            e.Name.LocalName == "PageBlock" && (string?)e.Attribute("ID") == id);
    }

    /// <summary>在 name 区顶部画一行文字的绘制器（页面绝对坐标）。</summary>
    private class NameTextDrawer : IDrawer
    {
        public string Text { get; set; } = "张三";
        public double X { get; set; } = 54;
        public double Y { get; set; } = 27;

        public void Draw(DrawContext ctx)
        {
            ctx.FillStyle = "#0000CC";
            ctx.Font = "4mm SimSun";
            ctx.FillText(Text, X, Y);
        }
    }

    /// <summary>lambda 到 IDrawer 的适配（IDrawer 是接口非委托）。</summary>
    private sealed class LambdaDrawer(Action<DrawContext> draw) : IDrawer
    {
        public void Draw(DrawContext ctx) => draw(ctx);
    }

    // ──────────────────────────────────────────────────────────────

    /// <summary>查询登记区域应返回 AreaHolderBlocks.xml 登记的 Boundary。</summary>
    [Fact]
    public void TryGetAreaHolder_ReturnsRegisteredBoundary()
    {
        Assert.True(OfdEditor.TryGetAreaHolder(Sample(), "name", out var nameBox));
        Assert.Equal(52.406, nameBox.TopLeftX, 3);
        Assert.Equal(25.4, nameBox.TopLeftY, 3);
        Assert.Equal(100, nameBox.Width, 3);
        Assert.Equal(10, nameBox.Height, 3);

        Assert.True(OfdEditor.TryGetAreaHolder(Sample(), "birthday", out var birthdayBox));
        Assert.Equal(52.406, birthdayBox.TopLeftX, 3);
        Assert.Equal(36.106, birthdayBox.TopLeftY, 3);

        // 不存在的区域 → false（Java AreaHolderContext.get 返回 null 同语义）
        Assert.False(OfdEditor.TryGetAreaHolder(Sample(), "memo", out _));
    }

    /// <summary>填充内容应写入登记 PageBlock（Java Canvas.setPreferBlock 同构），而非顶层图层。</summary>
    [Fact]
    public void FillAreaHolder_WritesIntoRegisteredPageBlock()
    {
        var edited = OfdEditor.FillAreaHolder(Sample(), "name", new NameTextDrawer(), out var output);
        Assert.True(edited);

        // 登记 PageBlock(10) 内出现新增 TextObject
        var block = PageBlock(output, "10");
        Assert.Contains(block.Descendants(), e => e.Name.LocalName == "TextObject"
            && e.Descendants().Any(t => t.Name.LocalName == "TextCode"
                && t.Value.Contains("张三")));

        // 文本所属图元的直接父级是 PageBlock（不是 Layer）
        var textEl = block.Descendants().First(e => e.Name.LocalName == "TextObject"
            && e.Descendants().Any(t => t.Name.LocalName == "TextCode" && t.Value.Contains("张三")));
        Assert.Equal("PageBlock", textEl.Parent?.Name.LocalName);

        // 原表单标签仍在（未被破坏）
        var content = EntryXml(output, "Doc_0/Pages/Page_0/Content.xml");
        Assert.Contains(content.Descendants(), e => e.Name.LocalName == "TextCode" && e.Value == "姓名");

        // 登记文件未被编辑改动
        var ahb = EntryXml(output, "Doc_0/AreaHolderBlocks.xml");
        Assert.Contains(ahb.Descendants(), e => e.Name.LocalName == "AreaHolderBlock"
            && (string?)e.Attribute("AreaName") == "name");
    }

    /// <summary>区域不存在时返回 false 且输出为原字节（不产生改动）。</summary>
    [Fact]
    public void FillAreaHolder_MissingArea_ReturnsFalseAndKeepsInput()
    {
        var input = Sample();
        var ok = OfdEditor.FillAreaHolder(input, "nonexistent", new NameTextDrawer(), out var output);
        Assert.False(ok);
        Assert.Equal(input, output);
    }

    /// <summary>会话可连续填充多个区域（多 PageBlock 各自落字）。</summary>
    [Fact]
    public void FillAreaHolder_Session_FillsMultipleAreas()
    {
        using var session = OfdEditSession.Open(Sample());
        Assert.True(session.FillAreaHolder("name", new LambdaDrawer(ctx =>
        {
            ctx.FillStyle = "#0000CC";
            ctx.Font = "4mm SimSun";
            ctx.FillText("张三", 54, 27);
        })));
        Assert.True(session.FillAreaHolder("birthday", new LambdaDrawer(ctx =>
        {
            ctx.FillStyle = "#CC0000";
            ctx.Font = "4mm SimSun";
            ctx.FillText("2019-09-27", 54, 37.7);
        })));
        Assert.True(session.TryGetAreaHolder("name", out var box));
        Assert.Equal(100, box.Width, 3);
        var output = session.Save();

        Assert.Contains(PageBlock(output, "10").Descendants(), e => e.Name.LocalName == "TextCode" && e.Value.Contains("张三"));
        Assert.Contains(PageBlock(output, "21").Descendants(), e => e.Name.LocalName == "TextCode" && e.Value.Contains("2019-09-27"));

        // 图元 ID 自增后写回 MaxUnitID（编辑产生新对象）
        var docXml = EntryXml(output, "Doc_0/Document.xml");
        var maxUnit = docXml.Descendants().First(e => e.Name.LocalName == "MaxUnitID").Value;
        Assert.True(int.Parse(maxUnit) > 21, $"MaxUnitID 应随编辑递增，实际 {maxUnit}");
    }

    /// <summary>文件版重载：向 OFD 文件填充并输出新文件。</summary>
    [Fact]
    public void FillAreaHolder_FileOverload_WritesOutputFile()
    {
        var inputPath = Out("fah_input.ofd");
        var outputPath = Out("fah_output.ofd");
        File.WriteAllBytes(inputPath, Sample());

        var ok = OfdEditor.FillAreaHolder(inputPath, outputPath, "name", new NameTextDrawer());
        Assert.True(ok);
        Assert.True(File.Exists(outputPath));
        Assert.Contains(PageBlock(File.ReadAllBytes(outputPath), "10").Descendants(),
            e => e.Name.LocalName == "TextCode" && e.Value.Contains("张三"));
    }
}
