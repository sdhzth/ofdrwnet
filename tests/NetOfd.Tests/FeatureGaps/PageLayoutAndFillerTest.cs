using System;
using System.IO;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// 页面布局与填充元素测试（覆盖率缺口：PageLayout/ArrayParamTool/PageAreaFiller）。
/// </summary>
public class PageLayoutAndFillerTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_layout_gap_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void ArrayParamTool_Variants_ShouldNormalizeTo4()
    {
        Assert.Equal(new[] { 0.0, 0, 0, 0 }, ArrayParamTool.Arr4p(Array.Empty<double>()));
        Assert.Equal(new[] { 5.0, 5, 5, 5 }, ArrayParamTool.Arr4p(new[] { 5d }));
        Assert.Equal(new[] { 1d, 2, 1, 2 }, ArrayParamTool.Arr4p(new[] { 1d, 2 }));
        Assert.Equal(new[] { 1d, 2, 3, 4 }, ArrayParamTool.Arr4p(new[] { 1d, 2, 3, 4 }));
        Assert.Throws<ArgumentException>(() => ArrayParamTool.Arr4p(new[] { 1d, 2, 3 }));
        Assert.Throws<ArgumentException>(() => ArrayParamTool.Arr4p(new[] { 1d, 2, 3, 4, 5 }));
    }

    [Fact]
    public void PageLayout_Margins_WorkerArea_Clone()
    {
        var layout = PageLayout.A4();
        Assert.Equal(210, layout.Width);
        // 默认边距：上下 25.4，左右 31.7
        Assert.Equal(25.4, layout.MarginTop);
        Assert.Equal(31.7, layout.MarginLeft);
        Assert.Equal(210 - 31.7 * 2, layout.ContentWidth(), 6);

        layout.SetMargin(10);                       // 四边相同
        Assert.Equal(new[] { 10d, 10, 10, 10 }, layout.Margin);
        layout.SetMargin(5, 8);                     // 上下 5，左右 8
        Assert.Equal(new[] { 5d, 8, 5, 8 }, layout.Margin);
        layout.SetMargin(1, 2, 3, 4);               // 上右下左
        Assert.Equal((1, 2), Start(layout));
        Assert.Equal(210 - 6, layout.ContentWidth(), 6);
        Assert.Equal(297 - 4, layout.ContentHeight(), 6);

        // 工作区原点 = 左/上边距（SetMargin 顺序为 上右下左 → 左=4，上=1）
        var worker = layout.GetWorkerArea();
        Assert.Equal(4, worker.X);
        Assert.Equal(1, worker.Y);

        // 页面区域：物理框与应用框均为整页（兼容骑缝章）
        var area = layout.GetPageArea();
        Assert.Equal(new STBoxNet(0, 0, 210, 297), ToNet(area.PhysicalBox!));

        // Clone 独立性
        var clone = layout.Clone();
        clone.MarginTop = 99;
        Assert.NotEqual(clone.MarginTop, layout.MarginTop);
        Assert.Equal(layout, PageLayout.A4().SetMargin(1, 2, 3, 4));
    }

    private static (double, double) Start(PageLayout l) => (l.MarginTop, l.MarginRight);

    // 避免直接依赖 NetOfd.Core.BasicType.STBox 的相等实现差异
    private record STBoxNet(double X, double Y, double W, double H);
    private static STBoxNet ToNet(NetOfd.Core.BasicType.STBox b)
        => new(b.TopLeftX, b.TopLeftY, b.Width, b.Height);

    [Fact]
    public void PageAreaFiller_ShouldPushFollowingContentToNewPage()
    {
        var path = Path.Combine(OutDir(), "filler.ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("第一段").SetFontSize(5d));
            doc.Add(new PageAreaFiller());
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("第二段应在下一页").SetFontSize(5d));
        }

        using var reader = new OFDReader(path);
        Assert.Equal(2, reader.GetPageCount());
        var texts = ContentExtractor.ExtractAllPagesText(reader.GetPages().ToList());
        Assert.Contains("第一段", texts[0]);
        Assert.DoesNotContain("第二段", texts[0]);
        Assert.Contains(texts, pageText => pageText.Contains("第二段应在下一页"));
    }
}
