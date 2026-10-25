using NetOfd.Core.PageDescription;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// 矩形框文本抽取过滤器测试（对齐 Java org.ofdrw.reader.extractor.RegionTextExtractorFilter）。
/// 验证：Boundary 偏移 + TextCode 逐字 Delta 推进定位、g 语法展开、区域半开判定、CTM 仿射变换。
/// </summary>
public class RegionTextExtractorFilterTest
{
    /// <summary>构造一个 Boundary=(0,0,200,50)、TextCode 起始 (10,10) 的单 TextCode 文本对象。</summary>
    private static (CTText TextObject, TextCode Code) Make(string text, string? deltaX = null, string? deltaY = null)
    {
        var t = new CTText { Boundary = new NetOfd.Core.BasicType.STBox(0, 0, 200, 50) };
        var c = new TextCode { X = 10, Y = 10, Text = text, DeltaX = deltaX, DeltaY = deltaY };
        t.AddTextCode(c);
        return (t, c);
    }

    private static string? Allow(CTText textObject, double x, double y, double w, double h)
        => new RegionTextExtractorFilter(x, y, w, h).AllowText(textObject, textObject.GetTextCodes()[0]);

    // ── 区域判定 ──────────────────────────────────────────────

    [Fact]
    public void WholeRegion_ShouldReturnWholeText()
    {
        var (t, _) = Make("AB");
        Assert.Equal("AB", Allow(t, 0, 0, 200, 50));
    }

    [Fact]
    public void NoDelta_CharsShareSamePosition()
    {
        // 无 DeltaX → 推进为 0，A/B 同点 (10,10)
        var (t, _) = Make("AB");
        // 半开 [0,15) 包含 (10,10) → 两字都命中
        Assert.Equal("AB", Allow(t, 0, 0, 15, 50));
        // 半开 [11,15) 不含 (10,10) → 无
        Assert.Null(Allow(t, 11, 0, 4, 50));
    }

    [Fact]
    public void GExpandedDelta_ShouldAdvancePerChar()
    {
        // A@(10,10)，B@(20,10)（g 1 10 展开为 [10]，文本长 2 → 补 [10,10]）
        var (t, _) = Make("AB", deltaX: "g 1 10");
        // [0,15) → 仅 A
        Assert.Equal("A", Allow(t, 0, 0, 15, 50));
        // [15,25) → 仅 B
        Assert.Equal("B", Allow(t, 15, 0, 10, 50));
        // 全区域 → AB
        Assert.Equal("AB", Allow(t, 0, 0, 200, 50));
    }

    [Fact]
    public void GFourValues_ShouldAdvanceFourChars()
    {
        // A@10 B@15 C@20 D@25（g 4 5）
        var (t, _) = Make("ABCD", deltaX: "g 4 5");
        // [0,18) → A(10) B(15) 中；C(20) 否
        Assert.Equal("AB", Allow(t, 0, 0, 18, 50));
        // [0,26) → 全中
        Assert.Equal("ABCD", Allow(t, 0, 0, 26, 50));
    }

    [Fact]
    public void DeltaY_ShouldAdvanceVertically()
    {
        // A@(10,10)，B@(10,20)（g 1 10）
        var (t, _) = Make("AB", deltaY: "g 1 10");
        // [0,15)×[0,15) → 仅 A；B 的 Y=20 超出
        Assert.Equal("A", Allow(t, 0, 0, 50, 15));
    }

    // ── CTM 变换 ──────────────────────────────────────────────

    [Fact]
    public void CtmTranslation_ShouldTransformCharPosition()
    {
        // CTM 平移 (100,0)：字符 (10,10) → 世界 (110,10)
        var t = new CTText
        {
            Boundary = new NetOfd.Core.BasicType.STBox(0, 0, 300, 300),
            CTM = NetOfd.Core.BasicType.STArray.Parse("1 0 0 1 100 0"),
        };
        var c = new TextCode { X = 10, Y = 10, Text = "A" };
        t.AddTextCode(c);

        Assert.Equal("A", Allow(t, 100, 0, 50, 50));
        Assert.Null(Allow(t, 0, 0, 50, 50));
    }

    [Fact]
    public void CtmScale_ShouldTransformPosition()
    {
        // CTM X 缩放 2：字符 (10,10) → 世界 (20,10)
        var t = new CTText
        {
            Boundary = new NetOfd.Core.BasicType.STBox(0, 0, 300, 300),
            CTM = NetOfd.Core.BasicType.STArray.Parse("2 0 0 1 0 0"),
        };
        var c = new TextCode { X = 10, Y = 10, Text = "A" };
        t.AddTextCode(c);

        Assert.Equal("A", Allow(t, 20, 0, 5, 50));
        Assert.Null(Allow(t, 10, 0, 5, 50));
    }

    // ── 边界情况 ──────────────────────────────────────────────

    [Fact]
    public void BoundaryOffset_ShouldShiftCharPosition()
    {
        // Boundary 左上角 (30,40) + TextCode (10,10) → 世界 (40,50)
        var t = new CTText { Boundary = new NetOfd.Core.BasicType.STBox(30, 40, 200, 100) };
        var c = new TextCode { X = 10, Y = 10, Text = "A" };
        t.AddTextCode(c);

        Assert.Equal("A", Allow(t, 40, 50, 5, 5));
        Assert.Null(Allow(t, 10, 10, 5, 5));
    }

    [Fact]
    public void EmptyText_ShouldReturnNull()
    {
        var (t, _) = Make("");
        Assert.Null(Allow(t, 0, 0, 200, 50));
    }

    [Fact]
    public void NegativeSize_ShouldThrow()
    {
        Assert.Throws<ArgumentException>(() => new RegionTextExtractorFilter(0, 0, -1, 50));
    }

    [Fact]
    public void HalfOpenBoundary_RightAndBottomEdgesExclusive()
    {
        // 字符恰在矩形右边缘：区域 [20,30)，B 在 X=20 → 命中（半开左闭）；矩形 X=20 的右边界外不中
        var (t, _) = Make("AB", deltaX: "g 1 10"); // A@10, B@20
        Assert.Equal("B", Allow(t, 20, 0, 10, 50));
        Assert.Null(Allow(t, 21, 0, 10, 50)); // B@20 < 21 不中
    }
}
