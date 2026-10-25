using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// Layout Clear 枚举 + Div.Clear 属性 + DivContainer 标记接口测试——对齐 Java
/// <c>org.ofdrw.layout.element.Clear</c> / <c>DivContainer</c> 同形态公开 API。
/// </summary>
public class ClearAndDivContainerTest
{
    // ── Clear 枚举：与 Java element.Clear 完全一致 ──

    [Fact]
    public void Clear_EnumValues_ShouldMatchJava()
    {
        // Java 顺序：none / left / right / both
        Assert.Equal(4, Enum.GetValues(typeof(Clear)).Length);
        Assert.Equal(0, (int)Clear.None);
        Assert.Equal(1, (int)Clear.Left);
        Assert.Equal(2, (int)Clear.Right);
        Assert.Equal(3, (int)Clear.Both);
    }

    // ── Div 默认 Clear = Both（与 Java Div.clear 默认一致） ──

    [Fact]
    public void Div_DefaultClear_ShouldBeBoth()
    {
        var div = new Div();
        Assert.Equal(Clear.Both, div.Clear);
    }

    [Fact]
    public void Div_SetClear_ShouldReturnSelfForChaining()
    {
        var div = new Div();
        var ret = div.SetClear(Clear.Left);
        Assert.Same(div, ret);
        Assert.Equal(Clear.Left, div.Clear);

        div.SetClear(Clear.None).SetClear(Clear.Right).SetClear(Clear.Both);
        Assert.Equal(Clear.Both, div.Clear);
    }

    // ── 已有 Div 子类继承 Clear（默认 Both） ──

    [Fact]
    public void Paragraph_DefaultClear_ShouldBeBoth()
    {
        var para = new Paragraph();
        Assert.Equal(Clear.Both, para.Clear);
    }

    [Fact]
    public void Paragraph_SetClear_ShouldBeChainable()
    {
        var para = new Paragraph();
        para.SetClear(Clear.Left);
        Assert.Equal(Clear.Left, para.Clear);
    }

    // ── DivContainer 标记接口 ──

    [Fact]
    public void DivContainer_IsInterface_ShouldBeImplementable()
    {
        // Java ofdrw-layout DivContainer 是空标记接口，Java 实际无 Div 子类实现
        // NetOfd 同形态对齐：定义接口保留扩展点
        Assert.True(typeof(DivContainer).IsInterface);

        // 自定义 Div 子类实现 DivContainer（验证接口可被实现）
        var logicalDiv = new LogicalDiv();
        Assert.IsAssignableFrom<DivContainer>(logicalDiv);
    }

    private sealed class LogicalDiv : Div, DivContainer
    {
    }
}