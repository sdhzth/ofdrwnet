using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// Java Div 公开 API 缺口对齐测试——Display 枚举 / IsBlockElement / IsNoBorder /
/// IsIntegrity / IsPlaceholder + Placeholder 静态工厂。
/// <para>Java 锚点：ofdrw-layout Div.java——isBlockElement(L207)「块元素独占整个段，绝对定位
/// 默认不是块级」：position==Absolute→false；(clear==Both)||(aFloat==Right&amp;&amp;clear==Left)→true；
/// isNoBorder(L191) 四边 border 全 0；setDisplay(L831) 与 Clear 联动
/// （inlineBlock→clear none，block→clear both），getDisplay(L846) 由 isBlockElement/clear 推导；
/// setIntegrity(L718) 默认 false；setPlaceholder(L821) 默认 false；
/// 静态 placeholder(L868/885) 工厂：占位符=setPlaceholder(true)+setIntegrity(true)+Clear.none。</para>
/// <para>引擎边界：NetOfd 无流式布局/拆页引擎，这些字段与方法仅 API 对齐（与 Clear 同待遇），
/// 语义精确复制 Java 推导逻辑。</para>
/// </summary>
public class DivDisplayApiTest
{
    // ── Display 枚举 ──

    [Fact]
    public void Display_EnumOrder_ShouldMatchJava()
    {
        // Java: public enum Display { block, inlineBlock }
        Assert.Equal(0, (int)Display.Block);
        Assert.Equal(1, (int)Display.InlineBlock);
    }

    // ── 默认值 ──

    [Fact]
    public void Div_Defaults_ShouldMatchJava()
    {
        var div = new Div();

        // 默认 clear=Both → 独占段 → 块级
        Assert.True(div.IsBlockElement());
        Assert.Equal(Display.Block, div.GetDisplay());
        // 无边框（四边 0）
        Assert.True(div.IsNoBorder());
        // integrity / placeholder 默认 false
        Assert.False(div.IsIntegrity());
        Assert.False(div.IsPlaceholder());
    }

    // ── SetDisplay 联动 Clear ──

    [Fact]
    public void SetDisplay_InlineBlock_ShouldSetClearNone()
    {
        var div = new Div().SetDisplay(Display.InlineBlock);

        Assert.Equal(Clear.None, div.Clear);
        Assert.Equal(Display.InlineBlock, div.GetDisplay());
    }

    [Fact]
    public void SetDisplay_Block_ShouldSetClearBoth()
    {
        var div = new Div().SetDisplay(Display.Block);

        Assert.Equal(Clear.Both, div.Clear);
        Assert.Equal(Display.Block, div.GetDisplay());
    }

    // ── IsBlockElement 组合语义 ──

    [Theory]
    [InlineData(Position.Absolute, Clear.Both, AFloat.None, false)]   // 绝对定位不是块级
    [InlineData(Position.Static, Clear.Both, AFloat.None, true)]      // 独占
    [InlineData(Position.Static, Clear.Left, AFloat.Right, true)]     // 浮动 + Clear 对立
    [InlineData(Position.Static, Clear.None, AFloat.Right, false)]    // 仅浮动不独占
    [InlineData(Position.Static, Clear.Left, AFloat.Left, false)]     // 同向浮动共享
    [InlineData(Position.Static, Clear.Left, AFloat.None, false)]     // 仅 Clear 左不独占
    public void IsBlockElement_Combinations_ShouldMatchJava(
        Position pos, Clear clear, AFloat f, bool expected)
    {
        var div = new Div { Position = pos, Clear = clear, Float = f };
        Assert.Equal(expected, div.IsBlockElement());
    }

    // ── IsNoBorder ──

    [Fact]
    public void IsNoBorder_AfterSetBorder_ShouldBeFalse()
    {
        var div = new Div();
        Assert.True(div.IsNoBorder());

        div.SetBorder(1.0);
        Assert.False(div.IsNoBorder());

        div.SetBorder(0.0);
        Assert.True(div.IsNoBorder());
    }

    // ── Integrity / Placeholder 方法对 ──

    [Fact]
    public void SetIntegrity_ShouldReturnSelfAndReflect()
    {
        var div = new Div();
        var ret = div.SetIntegrity(true);
        Assert.Same(div, ret); // 链式返回 Self
        Assert.True(div.IsIntegrity());

        div.SetIntegrity(false);
        Assert.False(div.IsIntegrity());
    }

    [Fact]
    public void SetPlaceholder_ShouldReturnSelfAndReflect()
    {
        var div = new Div();
        var ret = div.SetPlaceholder(true);
        Assert.Same(div, ret);
        Assert.True(div.IsPlaceholder());

        div.SetPlaceholder(false);
        Assert.False(div.IsPlaceholder());
    }

    // ── Placeholder 静态工厂 ──

    [Fact]
    public void Placeholder_StaticFactory_ShouldBuildPlaceholderDiv()
    {
        // Java: Div.placeholder(w,h,aFloat) = new Div().setPlaceholder(true).setWidth(w)
        //       .setHeight(h).setFloat(aFloat).setClear(Clear.none).setIntegrity(true)
        var ph = Div.Placeholder(10.0, 20.0, AFloat.Left);

        Assert.True(ph.IsPlaceholder());
        Assert.True(ph.IsIntegrity());
        Assert.Equal(10.0, ph.Width);
        Assert.Equal(20.0, ph.Height);
        Assert.Equal(AFloat.Left, ph.Float);
        Assert.Equal(Clear.None, ph.Clear);
    }

    [Fact]
    public void Placeholder_StaticFactory_WithRectangle_ShouldUseRectSize()
    {
        // Java: Div.placeholder(Rectangle rec, aFloat) = placeholder(rec.getWidth(), rec.getHeight(), aFloat)
        var ph = Div.Placeholder(new Rectangle(10, 20, 30, 40), AFloat.Right);

        Assert.True(ph.IsPlaceholder());
        Assert.Equal(30.0, ph.Width);
        Assert.Equal(40.0, ph.Height);
        Assert.Equal(AFloat.Right, ph.Float);
        Assert.Equal(Clear.None, ph.Clear);
        Assert.Equal(Display.InlineBlock, ph.GetDisplay()); // Clear.None 推导
    }
}
