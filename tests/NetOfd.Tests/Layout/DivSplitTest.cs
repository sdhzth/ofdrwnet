using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// <see cref="Div.Split"/> 盒级竖直切分单元测试——对齐 Java <c>Div.split(double)</c> 七段分支：
/// 切割点落入 margin / border / padding / content / padding / border / margin 区间时，
/// 两侧盒装饰的归属与数值调整必须与 Java 实现逐值一致。
/// </summary>
public class DivSplitTest
{
    private static double Occupy(Div d)
        => d.MarginTop + d.BorderTop + d.PaddingTop + (d.Height ?? 0)
           + d.PaddingBottom + d.BorderBottom + d.MarginBottom;

    /// <summary>切点落在内容区：最常见分支。上半段占位恰等于 sHeight，下半段承接剩余内容高度。</summary>
    [Fact]
    public void Split_ContentArea_AdjustsBothHeights()
    {
        var d = new Div().SetWidth(50).SetMargin(2).SetBorder(1).SetPadding(1).SetHeight(50);
        var halves = d.Split(30);
        var (d1, d2) = (halves[0], halves[1]);

        // 上装饰 2+1+1=4 → div1 内容高 30-4=26，下装饰清零
        Assert.Equal(26, d1.Height);
        Assert.Equal(0, d1.MarginBottom);
        Assert.Equal(0, d1.BorderBottom);
        Assert.Equal(0, d1.PaddingBottom);
        Assert.Equal(2, d1.MarginTop);   // 上装饰保留
        Assert.Equal(30, Occupy(d1), 6); // 上半段布局占位恰为切分高度

        // div2 上装饰清零，内容高 = 50-26 = 24
        Assert.Equal(24, d2.Height);
        Assert.Equal(0, d2.MarginTop);
        Assert.Equal(0, d2.BorderTop);
        Assert.Equal(0, d2.PaddingTop);
        Assert.False(d2.IsPlaceholder());
    }

    /// <summary>① 切点落在上 margin 区：div1 退化为纯 margin 空间占位符。</summary>
    [Fact]
    public void Split_MarginArea_Div1DegradesToPlaceholder()
    {
        var d = new Div().SetWidth(50).SetMargin(10).SetHeight(20);
        var halves = d.Split(6);   // sHeight ≤ MarginTop(10)
        var (d1, d2) = (halves[0], halves[1]);

        Assert.True(d1.IsPlaceholder());
        Assert.Equal(6, d1.MarginTop);
        Assert.Equal(0, d1.BorderTop);
        Assert.Equal(0, d1.Height);
        Assert.Equal(6, Occupy(d1), 6);
        // 残留在下一段顶部的 margin
        Assert.Equal(4, d2.MarginTop);
        Assert.Equal(20, d2.Height);  // 内容完整留在 div2
        Assert.False(d2.IsPlaceholder());
    }

    /// <summary>② 切点落在上 border 区。</summary>
    [Fact]
    public void Split_BorderArea_SplitsBorderTop()
    {
        var d = new Div().SetWidth(50).SetMargin(2).SetBorder(5).SetPadding(2).SetHeight(30);
        var halves = d.Split(6);   // m(2) < 6 ≤ m+b(7)
        var (d1, d2) = (halves[0], halves[1]);

        Assert.Equal(4, d1.BorderTop);  // 6-2：剩余空间全为 border
        Assert.Equal(0, d1.PaddingTop);
        Assert.Equal(0, d1.Height);
        Assert.Equal(6, Occupy(d1), 6);
        Assert.Equal(1, d2.BorderTop);  // 5-4 残余 border
        Assert.Equal(0, d2.MarginTop);
        Assert.False(d2.IsPlaceholder());
    }

    /// <summary>③ 切点落在上 padding 区。</summary>
    [Fact]
    public void Split_PaddingArea_SplitsPaddingTop()
    {
        var d = new Div().SetWidth(50).SetMargin(1).SetBorder(1).SetPadding(10).SetHeight(40);
        var halves = d.Split(5);   // m+b(2) < 5 ≤ m+b+p(12)
        var (d1, d2) = (halves[0], halves[1]);

        Assert.Equal(3, d1.PaddingTop);  // 5-1-1
        Assert.Equal(0, d1.Height);
        Assert.Equal(5, Occupy(d1), 6);
        Assert.Equal(7, d2.PaddingTop);  // 10-3 残余 padding
        Assert.Equal(0, d2.MarginTop);
        Assert.Equal(0, d2.BorderTop);
    }

    /// <summary>⑤ 切点落在内容下 padding 区。</summary>
    [Fact]
    public void Split_BottomPaddingArea_SplitsPaddingBottom()
    {
        var d = new Div().SetWidth(50)
            .SetMargin(1).SetBorder(1)
            .SetPadding(1, 1, 10, 1).SetHeight(20);
        var halves = d.Split(24);  // m+b+pT+H(23) < 24 ≤ +pB(33)
        var (d1, d2) = (halves[0], halves[1]);

        Assert.Equal(1, d1.PaddingBottom);  // 24-23
        Assert.Equal(0, d1.BorderBottom);
        Assert.Equal(0, d1.MarginBottom);
        Assert.Equal(24, Occupy(d1), 6);
        Assert.Equal(9, d2.PaddingBottom);  // 10-1 残余 padding
        Assert.Equal(0, d2.Height);
        Assert.Equal(0, d2.MarginTop);
    }

    /// <summary>⑥ 切点落在内容下 border 区。</summary>
    [Fact]
    public void Split_BottomBorderArea_SplitsBorderBottom()
    {
        var d = new Div().SetWidth(50)
            .SetMarginTop(1).SetMarginBottom(1)
            .SetBorderTop(1).SetBorderBottom(5)
            .SetPaddingTop(1).SetPaddingBottom(5).SetHeight(20);
        var halves = d.Split(30);  // …+pB(28) < 30 ≤ +bB(33)
        var (d1, d2) = (halves[0], halves[1]);

        Assert.Equal(2, d1.BorderBottom);  // 30-28
        Assert.Equal(0, d1.MarginBottom);
        Assert.Equal(30, Occupy(d1), 6);
        Assert.Equal(3, d2.BorderBottom);  // 5-2 残余 border
        Assert.Equal(0, d2.Height);
        Assert.Equal(0, d2.MarginTop);
        Assert.Equal(0, d2.PaddingBottom);
    }

    /// <summary>⑦ 切点落在内容下 margin 区：div2 退化为纯 margin 空间占位符。</summary>
    [Fact]
    public void Split_BottomMarginArea_Div2DegradesToPlaceholder()
    {
        var d = new Div().SetWidth(50)
            .SetMarginTop(1).SetMarginBottom(10)
            .SetBorderTop(1).SetBorderBottom(1)
            .SetPaddingTop(1).SetPaddingBottom(5).SetHeight(20);
        var halves = d.Split(31);  // …+bB(29) < 31 ≤ +mB(39)
        var (d1, d2) = (halves[0], halves[1]);

        Assert.Equal(2, d1.MarginBottom);  // 31-29
        Assert.Equal(31, Occupy(d1), 6);
        Assert.True(d2.IsPlaceholder());
        Assert.Equal(8, d2.MarginBottom);  // 10-2 残余 margin
        Assert.Equal(0, d2.Height);
    }

    /// <summary>整块总高（含装饰）≤ 切分高度时返回自身，不产生新对象。</summary>
    [Fact]
    public void Split_BlockFits_ReturnsSelf()
    {
        var d = new Div().SetWidth(50).SetMargin(2).SetHeight(10);
        var halves = d.Split(15);   // totalH=14 ≤ 15
        Assert.Same(d, halves[0]);
        Assert.Single(halves);
    }

    /// <summary>宽度或高度未确定时抛异常（Java 语义）。</summary>
    [Fact]
    public void Split_MissingWidthOrHeight_Throws()
    {
        var noHeight = new Div().SetWidth(10);
        Assert.Throws<InvalidOperationException>(() => noHeight.Split(5));

        var noWidth = new Div().SetHeight(10);
        Assert.Throws<InvalidOperationException>(() => noWidth.Split(5));
    }

    /// <summary>Clone/CopyTo 对齐 Java copyTo：盒属性全量复制；修正 Java 漏拷 borderDash 缺陷。</summary>
    [Fact]
    public void Clone_CopiesBoxAttributes_AndBorderDash()
    {
        var src = new Div()
            .SetWidth(40).SetHeight(60)
            .SetMargin(1).SetBorder(2).SetPadding(3)
            .SetBackgroundColor(new NetOfd.Core.PageDescription.Color.CT_Color { Value = "1,2,3" })
            .SetBorderDash(1d, 2d, 3d)
            .SetPosition(Position.Absolute)
            .SetIntegrity(true)
            .SetOpacity(0.5);
        var clone = src.Clone();

        Assert.Equal(40, clone.Width);
        Assert.Equal(60, clone.Height);
        Assert.Equal(1, clone.MarginTop);
        Assert.Equal(2, clone.BorderTop);
        Assert.Equal(3, clone.PaddingLeft);
        Assert.Equal(Position.Absolute, clone.Position);
        Assert.True(clone.IsIntegrity());
        Assert.Equal(0.5, clone.Opacity);
        Assert.NotNull(clone.BorderDash);
        Assert.Equal(3, clone.BorderDash!.Length);   // [偏移1, 线长2, 空白3]
        Assert.NotSame(src, clone);
    }
}
