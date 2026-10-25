using System.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.DrawParam;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// 绘制参数 ParentID 继承链解析测试（GB/T 33190 §8.1.2）。
/// </summary>
public class DrawParamResolverTest
{
    private static CT_DrawParam Make(string id, string? parent = null)
    {
        var dp = new CT_DrawParam { Id = id, ParentID = parent };
        return dp;
    }

    [Fact]
    public void DirectId_ShouldReturnCopy()
    {
        var dp = Make("10");
        dp.LineWidth = 0.7;

        var resolved = DrawParamResolver.Resolve(new[] { dp }, new STRefID(10));
        Assert.NotNull(resolved);
        Assert.Equal("10", resolved!.Id);
        Assert.Equal(0.7, resolved.LineWidth);
    }

    [Fact]
    public void MissingId_ShouldReturnNull()
    {
        Assert.Null(DrawParamResolver.Resolve(new[] { Make("10") }, new STRefID(99)));
        Assert.Null(DrawParamResolver.Resolve(System.Array.Empty<CT_DrawParam>(), null));
    }

    [Fact]
    public void OneLevelInheritance_ChildShouldInheritParentLineWidth()
    {
        var parent = Make("10");
        parent.LineWidth = 0.9;
        parent.Join = LineJoinType.Round;
        var child = Make("11", parent: "10");
        child.Cap = LineCapType.Round; // 子自有属性

        var r = DrawParamResolver.Resolve(new[] { parent, child }, new STRefID(11))!;
        Assert.Equal(0.9, r.LineWidth);      // 继承
        Assert.Equal(LineJoinType.Round, r.Join); // 继承
        Assert.Equal(LineCapType.Round, r.Cap);   // 自有
    }

    [Fact]
    public void MultiLevel_ChildOverrideWinsOverAncestor()
    {
        var gp = Make("1");
        gp.LineWidth = 0.35;
        var mid = Make("2", parent: "1");
        mid.LineWidth = 0.8;
        var leaf = Make("3", parent: "2");
        leaf.LineWidth = 0.53;

        var r = DrawParamResolver.Resolve(new[] { gp, mid, leaf }, new STRefID(3))!;
        Assert.Equal(0.53, r.LineWidth);

        // 中间节点解析：继承祖父，被叶子无关
        var rm = DrawParamResolver.Resolve(new[] { gp, mid, leaf }, new STRefID(2))!;
        Assert.Equal(0.8, rm.LineWidth);

        // 叶子未设 Join → 取中间 → 再取祖父
        gp.Join = LineJoinType.Bevel;
        var r3 = DrawParamResolver.Resolve(new[] { gp, mid, leaf }, new STRefID(3))!;
        Assert.Equal(LineJoinType.Bevel, r3.Join);
    }

    [Fact]
    public void Cycle_ShouldNotHang_AndUseAvailableParts()
    {
        var a = Make("1", parent: "2");
        a.LineWidth = 0.5;
        var b = Make("2", parent: "1"); // 环

        var r = DrawParamResolver.Resolve(new[] { a, b }, new STRefID(1))!;
        Assert.Equal(0.5, r.LineWidth);
    }

    [Fact]
    public void BrokenParentChain_ShouldReturnChildValuesOnly()
    {
        var child = Make("11", parent: "404");
        child.Cap = LineCapType.Square;

        var r = DrawParamResolver.Resolve(new[] { child }, new STRefID(11))!;
        Assert.Equal(LineCapType.Square, r.Cap);
    }

    [Fact]
    public void AlphaAndVisible_InheritWhenAbsent()
    {
        var parent = Make("1");
        parent.Alpha = 128;
        var child = Make("2", parent: "1");
        child.Visible = false;

        var r = DrawParamResolver.Resolve(new[] { parent, child }, new STRefID(2))!;
        Assert.Equal(128, r.Alpha);
        Assert.False(r.Visible);
    }
}
