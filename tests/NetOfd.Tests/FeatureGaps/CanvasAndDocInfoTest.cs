using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Core.BasicStructure.OFD;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// 画布路径构造器与状态快照测试。
/// </summary>
public class CanvasPathTest
{
    [Fact]
    public void PathBuilder_Ops_ShouldSerializeToAbbreviatedData()
    {
        var p = new PathBuilder();
        p.MoveTo(0, 0);
        p.LineTo(10, 0);
        p.QuadraticBezier(5, -5, 10, 10);
        p.CubicBezier(1, 2, 3, 4, 5, 6);
        p.Arc(5, 5, 0, largeArc: true, sweep: false, 20, 20);
        p.Close();

        var data = string.Join(" ", p.Operations.Select(o => o.ToAbbreviatedData()));
        // GB/T 33190 图形轮廓数据命令集：B = 三次贝塞尔（6 参数），C = 闭合（0 参数）
        Assert.Equal("M 0 0 L 10 0 Q 5 -5 10 10 B 1 2 3 4 5 6 A 5 5 0 1 0 20 20 C", data);
        Assert.Equal(OpType.Z, p.LastOp.Type);
    }

    [Fact]
    public void PathBuilder_Transformed_ShouldApplyCtm()
    {
        var p = new PathBuilder();
        p.MoveTo(1, 1);
        p.LineTo(2, 2);

        // 平移 (10, 20)
        double[] translate = { 1, 0, 0, 1, 10, 20 };
        var moved = p.Transformed(translate);
        Assert.Equal(11, moved.Operations[0].Args[0]);
        Assert.Equal(21, moved.Operations[0].Args[1]);
        Assert.Equal(12, moved.Operations[1].Args[0]);

        // 恒等矩阵不变
        double[] identity = { 1, 0, 0, 1, 0, 0 };
        var same = p.Transformed(identity);
        Assert.Equal(p.Operations.Count, same.Operations.Count);
        Assert.Equal(1, same.Operations[0].Args[0]);
    }

    [Fact]
    public void CanvasState_Clone_ShouldBeIndependent()
    {
        var state = new CanvasState
        {
            FillStyle = "#FF0000",
            GlobalAlpha = 0.5,
            FontSetting = new FontSetting(4, "SimSun"),
        };
        var clone = state.Clone();

        Assert.Equal(state.FillStyle, clone.FillStyle);
        Assert.Equal(state.GlobalAlpha, clone.GlobalAlpha);
        Assert.Equal("SimSun", clone.FontSetting!.Family);

        // 修改克隆不影响原状态
        clone.FillStyle = "#00FF00";
        clone.FontSetting.Size = 99;
        Assert.Equal("#FF0000", state.FillStyle);
        Assert.Equal(4, state.FontSetting!.Size);
    }
}

/// <summary>
/// DocInfo 全字段序列化往返测试（覆盖率缺口：48%）。
/// </summary>
public class DocInfoRoundTripTest
{
    [Fact]
    public void AllFields_ShouldSurviveElementRoundtrip()
    {
        var info = new DocInfo
        {
            DocID = "b8ab8f57c5e94a9ea8a6b7e7a1f2b3c4",
            Title = "往返测试文档",
            Author = "张三",
            Subject = "测试主题",
            Abstract = "这是摘要",
            CreationDate = "2026-08-23",
            ModDate = "2026-08-24",
            Creator = "NetOfd",
            CreatorVersion = "1.0",
        };
        info.MaxUnitID = new NetOfd.Core.BasicType.STID(42);

        var round = new DocInfo(new XElement(info.Element));

        Assert.Equal(info.DocID, round.DocID);
        Assert.Equal(info.Title, round.Title);
        Assert.Equal(info.Author, round.Author);
        Assert.Equal(info.Subject, round.Subject);
        Assert.Equal(info.Abstract, round.Abstract);
        Assert.Equal(info.CreationDate, round.CreationDate);
        Assert.Equal(info.ModDate, round.ModDate);
        Assert.Equal(info.Creator, round.Creator);
        Assert.Equal(info.CreatorVersion, round.CreatorVersion);
        Assert.Equal(42u, round.MaxUnitID?.Value);
    }

    [Fact]
    public void EmptyDocInfo_ShouldYieldNulls()
    {
        var info = new DocInfo();
        var round = new DocInfo(new XElement(info.Element));
        Assert.Null(round.Title);
        Assert.Null(round.Author);
        Assert.Null(round.CreationDate);
        Assert.Null(round.MaxUnitID);
    }
}
