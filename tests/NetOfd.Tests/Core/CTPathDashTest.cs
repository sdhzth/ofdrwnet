using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// CTPath 虚线属性测试：规范双属性 DashPattern/DashOffset（对齐 GB/T 33190 与 Java CT_GraphicUnit），
/// 以及旧私有写法 "Dash" 属性的读兼容。
/// </summary>
public class CTPathDashTest
{
    [Fact]
    public void DashPatternAndOffset_ShouldSerializeToNormativeAttributes()
    {
        var path = new CTPath
        {
            Stroke = true,
            LineWidth = 1,
            DashPattern = new STArray([3, 2]),
            DashOffset = 1.5,
        };

        Assert.Equal("3 2", path.Element.Attribute("DashPattern")?.Value);
        Assert.Equal("1.5", path.Element.Attribute("DashOffset")?.Value);
        // 不应再产生历史私有属性
        Assert.Null(path.Element.Attribute("Dash"));
    }

    [Fact]
    public void DashPatternAndOffset_ShouldRoundTripViaElement()
    {
        var path = new CTPath(XElement.Parse(
            "<PathObject Stroke=\"true\" DashPattern=\"4 1 2 1\" DashOffset=\"2\"/>"));
        Assert.NotNull(path.DashPattern);
        Assert.True(new[] { 4d, 1d, 2d, 1d }.SequenceEqual(path.DashPattern.ToDoubleArray()));
        Assert.Equal(2d, path.DashOffset);
        // 兼容访问器读同源
        Assert.NotNull(path.Dash);
        Assert.True(new[] { 4d, 1d, 2d, 1d }.SequenceEqual(path.Dash!.ToDoubleArray()));
    }

    [Fact]
    public void LegacyDashAttribute_ShouldStillBeReadable()
    {
        // 旧版 NetOfd 曾写私有属性 Dash="3 2"：读取应回退兼容（DashPattern/Dash 均可见）
        var path = new CTPath(XElement.Parse(
            "<PathObject Stroke=\"true\" Dash=\"3 2\"/>"));
        Assert.NotNull(path.DashPattern);
        Assert.True(new[] { 3d, 2d }.SequenceEqual(path.DashPattern.ToDoubleArray()));
        Assert.Null(path.DashOffset);
    }

    [Fact]
    public void DashSetter_ShouldWriteDashPatternAttribute()
    {
        // 兼容访问器写入 → 落规范属性 DashPattern
        var path = new CTPath { Dash = new STArray([5, 2]) };
        Assert.Equal("5 2", path.Element.Attribute("DashPattern")?.Value);
        Assert.Null(path.Element.Attribute("Dash"));
    }

    [Fact]
    public void NoDash_ShouldHaveNoAttributes()
    {
        var path = new CTPath { Stroke = true, LineWidth = 1 };
        Assert.Null(path.DashPattern);
        Assert.Null(path.DashOffset);
        Assert.Null(path.Element.Attribute("DashPattern"));
        Assert.Null(path.Element.Attribute("DashOffset"));
    }

    [Fact]
    public void DashOffsetZero_ShouldWriteZero()
    {
        // 显式写 0 偏移 → 属性为 "0"（合规；但 Layout 层仅在 >0 时写出，这里仅验证模型行为）
        var path = new CTPath { DashPattern = new STArray([3, 3]), DashOffset = 0 };
        Assert.Equal("0", path.Element.Attribute("DashOffset")?.Value);
    }
}
