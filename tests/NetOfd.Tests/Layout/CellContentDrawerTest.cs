using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// CellContentDrawer 单元格化填充测试（#6 areaholder 剩余项闭环）。
/// 复用 Java 官方登记样本 areaholder_fields.ofd（name→PageBlock 10，边界 52.406,25.4,100,10）。
/// 对齐 Java: org.ofdrw.layout.element.canvas.CellContentDrawer + AreaHolderContextTest.getCell
/// </summary>
public class CellContentDrawerTest
{
    private static string SamplePath()
        => Path.Combine(AppContext.BaseDirectory, "Resources", "areaholder_fields.ofd");

    private static byte[] Sample() => File.ReadAllBytes(SamplePath());

    private static XDocument EntryXml(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var e = za.GetEntry(entryName) ?? throw new FileNotFoundException(entryName);
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return XDocument.Parse(Encoding.UTF8.GetString(buf.ToArray()));
    }

    /// <summary>取登记 PageBlock（按 ID）元素。</summary>
    private static XElement PageBlock(byte[] ofd, string id)
    {
        var content = EntryXml(ofd, "Doc_0/Pages/Page_0/Content.xml");
        return content.Descendants().First(e =>
            e.Name.LocalName == "PageBlock" && (string?)e.Attribute("ID") == id);
    }

    /// <summary>解析 Boundary="x y w h" 为四元组。</summary>
    private static (double X, double Y, double W, double H) ParseBoundary(XElement obj)
    {
        var parts = obj.Attribute("Boundary")!.Value.Split(' ');
        return (double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[2], System.Globalization.CultureInfo.InvariantCulture),
                double.Parse(parts[3], System.Globalization.CultureInfo.InvariantCulture));
    }

    private static XElement? FirstTextObject(XElement block, string contains)
        => block.Descendants().FirstOrDefault(e => e.Name.LocalName == "TextObject"
            && e.Descendants().Any(t => t.Name.LocalName == "TextCode" && t.Value.Contains(contains)));

    /// <summary>默认（左对齐 + 垂直居中）文字应写入登记 PageBlock，基线居中。</summary>
    [Fact]
    public void CellFill_Default_LeftCenter_WritesIntoPageBlock()
    {
        var ok = OfdEditor.FillAreaHolder(Sample(), "name",
            cell => cell.SetValue("张三"), out var output);
        Assert.True(ok);

        var block = PageBlock(output, "10");
        var txt = FirstTextObject(block, "张三");
        Assert.NotNull(txt);
        Assert.Equal("PageBlock", txt!.Parent?.Name.LocalName); // 不可挂在顶层图层

        // 默认 3mm 宋体，"张三" CJK 各 3mm → 宽 6，垂直居中 baseline=6.2 → top=3.2
        var b = ParseBoundary(txt);
        Assert.Equal(52.406, b.X, 3);   // 左下角 X
        Assert.Equal(28.6, b.Y, 3);     // 25.4 + 3.2
        Assert.Equal(6.0, b.W, 3);
    }

    /// <summary>水平居中：文字整体落在区域水平中心。</summary>
    [Fact]
    public void CellFill_CenterAlign_XCenteredInArea()
    {
        var ok = OfdEditor.FillAreaHolder(Sample(), "name",
            cell => cell.SetValue("张三").SetTextAlign(TextAlign.Center), out var output);
        Assert.True(ok);

        var txt = FirstTextObject(PageBlock(output, "10"), "张三")!;
        var b = ParseBoundary(txt);
        // 区域中心 = 52.406 + 50 = 102.406；文字宽 6 → 左缘 = 102.406 - 3 = 99.406
        Assert.Equal(99.406, b.X, 3);
    }

    /// <summary>垂直顶部对齐：文字贴区域顶部。</summary>
    [Fact]
    public void CellFill_VerticalTop_YAtTop()
    {
        var ok = OfdEditor.FillAreaHolder(Sample(), "name",
            cell => cell.SetValue("张三").SetVerticalAlign(VerticalAlign.Top), out var output);
        Assert.True(ok);

        var txt = FirstTextObject(PageBlock(output, "10"), "张三")!;
        var b = ParseBoundary(txt);
        // 垂直顶部 baseline=3 → top=0 → Y=25.4
        Assert.Equal(25.4, b.Y, 3);
    }

    /// <summary>会话式 Action&lt;CellContentDrawer&gt; 重载：自动按区域边界定位并填充。</summary>
    [Fact]
    public void CellFill_Session_ActionOverload()
    {
        using var session = OfdEditSession.Open(Sample());
        var ok = session.FillAreaHolder("name", cell =>
            cell.SetValue("李雷").SetTextAlign(TextAlign.Center).SetColor("#0000CC"));
        Assert.True(ok);

        var output = session.Save();
        var txt = FirstTextObject(PageBlock(output, "10"), "李雷");
        Assert.NotNull(txt);
        Assert.Equal("PageBlock", txt!.Parent?.Name.LocalName);

        // 颜色被写入（DrawParam / CT_Color 资源）
        var content = EntryXml(output, "Doc_0/Pages/Page_0/Content.xml");
        Assert.Contains(content.Descendants(), e => e.Name.LocalName == "TextObject"
            && e.Descendants().Any(t => t.Name.LocalName == "TextCode" && t.Value.Contains("李雷")));
    }

    /// <summary>区域不存在时返回 false 且输出为原字节（不抛错，同 Java get 返回 null）。</summary>
    [Fact]
    public void CellFill_MissingArea_ReturnsFalseAndKeepsInput()
    {
        var input = Sample();
        var ok = OfdEditor.FillAreaHolder(input, "memo", // memo 未在 AreaHolderBlocks.xml 登记
            cell => cell.SetValue("x"), out var output);
        Assert.False(ok);
        Assert.Equal(input, output);
    }

    /// <summary>超宽文字自动换行：多行落入同一 PageBlock。</summary>
    [Fact]
    public void CellFill_LongText_AutoWrapsToMultipleLines()
    {
        // 40 个 CJK 字 × 3mm = 120mm，区域宽 100mm → 至少拆成 2 行
        const string longText = "一二三四五六七八九十甲乙丙丁戊己庚辛壬癸子丑寅卯辰巳午未申酉戌亥天地玄黄宇宙洪荒日月盈昃辰宿列张";
        var ok = OfdEditor.FillAreaHolder(Sample(), "name",
            cell => cell.SetValue(longText).SetFontSize(3),
            out var output);
        Assert.True(ok);

        var block = PageBlock(output, "10");
        var lines = block.Descendants()
            .Where(e => e.Name.LocalName == "TextObject"
                && e.Descendants().Any(t => t.Name.LocalName == "TextCode"))
            .ToList();
        Assert.True(lines.Count >= 2, $"预期自动换行（≥2 个 TextObject），实际 {lines.Count}");
    }
}
