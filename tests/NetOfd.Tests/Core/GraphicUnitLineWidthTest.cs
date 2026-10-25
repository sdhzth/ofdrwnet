using NetOfd.Core.PageDescription;
using System.Xml.Linq;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// CTGraphicUnit.LineWidth 规范对齐：GB/T 33190 与 Java CT_GraphicUnit 均以**属性**表达线宽，
/// 旧版 NetOfd 曾写 <see cref="System.Xml.Linq.XElement"/> 子元素 <c>&lt;LineWidth&gt;</c>（跨语言不可读）。
/// 修复后：写一律属性；读回退旧子元素。
/// </summary>
public class GraphicUnitLineWidthTest
{
    [Fact]
    public void LineWidth_Set_ShouldSerializeAsAttribute()
    {
        var t = new CTText { LineWidth = 1.5 };
        var xml = XDocument.Parse(t.Element.ToString());
        Assert.NotNull(xml.Root!.Attribute("LineWidth"));
        Assert.Equal("1.5", xml.Root.Attribute("LineWidth")!.Value);
        // 不再产生私有子元素写法
        Assert.DoesNotContain(xml.Descendants(), e => e.Name.LocalName == "LineWidth");
    }

    [Fact]
    public void LineWidth_Get_ShouldReadAttribute()
    {
        // Java / 规范形式：属性
        var t = new CTText(XElement.Parse("<TextObject LineWidth=\"2\"/>"));
        Assert.Equal(2d, t.LineWidth!.Value);
    }

    [Fact]
    public void LineWidth_Get_ShouldFallbackToLegacyChildElement()
    {
        // 旧版 NetOfd 产物：<LineWidth> 子元素（无命名空间 与 ofd 命名空间两种写法均应可读）
        var noNs = new CTText(XElement.Parse("<TextObject><LineWidth>2</LineWidth></TextObject>"));
        Assert.Equal(2d, noNs.LineWidth!.Value);

        var ofdNs = new CTText(XElement.Parse(
            "<TextObject xmlns=\"ofd\"><ofd:LineWidth xmlns:ofd=\"ofd\">3</ofd:LineWidth></TextObject>"));
        Assert.Equal(3d, ofdNs.LineWidth!.Value);
    }

    [Fact]
    public void LineWidth_Null_ShouldRemoveAttributeAndPreferAttribute()
    {
        // 同时存在 attr 与旧子元素时，属性优先（规范形态为准）
        var t = new CTText(XElement.Parse(
            "<TextObject LineWidth=\"5\"><LineWidth>2</LineWidth></TextObject>"));
        Assert.Equal(5d, t.LineWidth!.Value);

        t.LineWidth = null;
        Assert.Null(t.LineWidth);
        Assert.Null(t.Element.Attribute("LineWidth"));
    }

    [Fact]
    public void LineWidth_OnPathObject_ShouldStayAttribute()
    {
        // CTPath 覆盖实现（new double LineWidth）早已属性化——回归保护
        var p = new CTPath { Stroke = true, LineWidth = 1 };
        var xml = XDocument.Parse(p.Element.ToString());
        Assert.Equal("1", xml.Root!.Attribute("LineWidth")!.Value);
    }
}
