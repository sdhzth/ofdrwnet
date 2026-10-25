using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// DeltaX/DeltaY 展开工具测试（对齐 Java org.ofdrw.reader.DeltaTool）。
/// 覆盖 "g n v" 压缩语法、按文本长度补齐、空/缺失、混合与分隔符兼容。
/// </summary>
public class DeltaToolTest
{
    [Fact]
    public void NullOrBlank_ShouldReturnEmpty()
    {
        Assert.Empty(DeltaTool.GetDelta(null, 3));
        Assert.Empty(DeltaTool.GetDelta("", 3));
        Assert.Empty(DeltaTool.GetDelta("   ", 3));
    }

    [Fact]
    public void PlainShorterThanContent_ShouldPadWithLast()
    {
        // 1 2 3 → 缺 2 个 → 补最后值 3 → [1,2,3,3,3]
        Assert.Equal(new double[] { 1, 2, 3, 3, 3 }, DeltaTool.GetDelta("1 2 3", 5));
    }

    [Fact]
    public void SingleValue_ShouldPadWithItself()
    {
        Assert.Equal(new double[] { 7, 7, 7 }, DeltaTool.GetDelta("7", 3));
    }

    [Fact]
    public void GRepeat_ShorterThanContent_ShouldExpandAndPad()
    {
        // g 3 5.5 → [5.5,5.5,5.5]，文本长 4 → 补 1 个
        Assert.Equal(new double[] { 5.5, 5.5, 5.5, 5.5 }, DeltaTool.GetDelta("g 3 5.5", 4));
    }

    [Fact]
    public void GRepeat_ExactLength_ShouldNotPad()
    {
        Assert.Equal(new double[] { 5, 5, 5, 5 }, DeltaTool.GetDelta("g 4 5", 4));
    }

    [Fact]
    public void GRepeat_ZeroCount_ShouldYieldEmpty()
    {
        Assert.Empty(DeltaTool.GetDelta("g 0 9", 3));
    }

    [Fact]
    public void Mixed_PlainAndG_ShouldExpandInOrder()
    {
        // 1 g 2 3 4 → [1, 3, 3, 4]，文本长 6 → 补 2 个 4
        Assert.Equal(new double[] { 1, 3, 3, 4, 4, 4 }, DeltaTool.GetDelta("1 g 2 3 4", 6));
    }

    [Fact]
    public void LongerThanContent_ShouldNotTruncate()
    {
        Assert.Equal(new double[] { 1, 2, 3 }, DeltaTool.GetDelta("1 2 3", 2));
    }

    [Fact]
    public void CommaSeparated_ShouldBeAccepted()
    {
        Assert.Equal(new double[] { 1, 2, 3, 3, 3 }, DeltaTool.GetDelta("1,2,3", 5));
    }

    [Fact]
    public void MultipleG_ShouldExpandSequentially()
    {
        // g 2 1 g 3 2 → [1,1,2,2,2]，长 5 恰好
        Assert.Equal(new double[] { 1, 1, 2, 2, 2 }, DeltaTool.GetDelta("g 2 1 g 3 2", 5));
    }

    [Fact]
    public void TrailingGWithoutValue_ShouldIgnoreSafely()
    {
        // 残缺 g 语法（缺 v）：展开 "1" 后忽略残缺段；长 3 → 补 [1,1]
        Assert.Equal(new double[] { 1, 1, 1 }, DeltaTool.GetDelta("1 g 2", 3));
    }
}
