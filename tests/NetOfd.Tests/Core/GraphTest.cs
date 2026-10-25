using System.Linq;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicType;
using NetOfd.Core.Graph.PathObj;
using NetOfd.Core.Graph.Tight;
using NetOfd.Core.Graph.Tight.Method;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 图形（Graph）子系统测试：紧缩轮廓数据 AbbreviatedData / OptVal + XML 负载型区域
/// CT_Region / CT_Area + 六种绘制指令（Move/Line/Close/Arc/Q/B）往返。
/// 对齐 Java: org.ofdrw.core.graph（GB/T 33190 表 35-37、图 49/51-56）。
/// </summary>
public class GraphTest
{
    private static XElement RoundTrip(OFDElement e)
    {
        var copy = new XElement(e.Element);
        return copy;
    }

    // ────────────────────────────────────────────────────────
    //  OptVal
    // ────────────────────────────────────────────────────────

    [Fact]
    public void OptVal_ToString_FormatsByOperator()
    {
        Assert.Equal("M 10 20", new OptVal("M", new[] { 10d, 20d }).ToString());
        Assert.Equal("L 1 2", new OptVal("L", new[] { 1d, 2d }).ToString());
        Assert.Equal("Q 1 2 3 4", new OptVal("Q", new[] { 1d, 2d, 3d, 4d }).ToString());
        Assert.Equal("B 1 2 3 4 5 6", new OptVal("B", new[] { 1d, 2d, 3d, 4d, 5d, 6d }).ToString());
        Assert.Equal("A 5 3 30 1 0 7 8", new OptVal("A", new[] { 5d, 3d, 30d, 1d, 0d, 7d, 8d }).ToString());
        Assert.Equal("C", new OptVal("C").ToString());
        Assert.Equal("", new OptVal("Z", new[] { 1d }).ToString());
    }

    [Fact]
    public void OptVal_ToCmd_MapsAllOperators()
    {
        Assert.IsType<Move>(new OptVal("M", new[] { 1d, 2d }).ToCmd());
        Assert.IsType<Move>(new OptVal("S", new[] { 1d, 2d }).ToCmd());
        Assert.IsType<Line>(new OptVal("L", new[] { 1d, 2d }).ToCmd());
        Assert.IsType<QuadraticBezier>(new OptVal("Q", new[] { 1d, 2d, 3d, 4d }).ToCmd());
        Assert.IsType<CubicBezier>(new OptVal("B", new[] { 1d, 2d, 3d, 4d, 5d, 6d }).ToCmd());
        Assert.IsType<Arc>(new OptVal("A", new[] { 1d, 2d, 3d, 1d, 1d, 4d, 5d }).ToCmd());
        Assert.IsType<Close>(new OptVal("C").ToCmd());
        Assert.Null(new OptVal("Z").ToCmd());
    }

    [Fact]
    public void OptVal_Filling_PadsWithZeros()
    {
        Assert.Equal(new[] { 1d, 2d, 0d, 0d }, OptVal.Filling(new[] { 1d, 2d }, 4));
        Assert.Equal(new[] { 1d, 2d }, OptVal.Filling(new[] { 1d, 2d }, 2));
        Assert.Equal(new double[3], OptVal.Filling(null, 3));
        // 长度超过期待时原样返回
        Assert.Equal(new[] { 1d, 2d, 3d }, OptVal.Filling(new[] { 1d, 2d, 3d }, 2));
    }

    [Fact]
    public void OptVal_Parse_UnparsableValueBecomesZero()
    {
        var ov = new OptVal("M", new[] { "abc", "3.5" });
        Assert.Equal(0d, ov.Values[0]);
        Assert.Equal(3.5d, ov.Values[1]);
    }

    // ────────────────────────────────────────────────────────
    //  AbbreviatedData
    // ────────────────────────────────────────────────────────

    [Fact]
    public void AbbreviatedData_Parse_SplitsOperators()
    {
        var list = AbbreviatedData.ParseData("S 0 0 M 10 10 L 20 20 C");
        Assert.Equal(4, list.Count);
        Assert.Equal("S", list[0].Opt);
        Assert.Equal(new[] { 0d, 0d }, list[0].Values);
        Assert.Equal("M", list[1].Opt);
        Assert.Equal("L", list[2].Opt);
        Assert.Equal("C", list[3].Opt);
        Assert.Empty(list[3].Values);
    }

    [Fact]
    public void AbbreviatedData_Fluent_RoundTrips()
    {
        var data = new AbbreviatedData()
            .S(0, 0)
            .M(10, 10)
            .L(20, 20)
            .Q(1, 2, 3, 4)
            .B(1, 2, 3, 4, 5, 6)
            .A(5, 3, 30, 1, 0, 7, 8)
            .C();

        var text = data.Flush().ToDataString();
        Assert.Equal("S 0 0 M 10 10 L 20 20 Q 1 2 3 4 B 1 2 3 4 5 6 A 5 3 30 1 0 7 8 C", text);

        // 元素文本应同步写入，且可被重新解析
        var reparsed = new AbbreviatedData(RoundTrip(data));
        Assert.Equal(7, reparsed.Size);
        Assert.Equal(text, reparsed.ToDataString());
    }

    [Fact]
    public void AbbreviatedData_Empty_ReturnsEmptyString()
    {
        Assert.Equal("", new AbbreviatedData().ToDataString());
        Assert.Empty(AbbreviatedData.ParseData(null));
        Assert.Empty(AbbreviatedData.ParseData("   "));
    }

    [Fact]
    public void AbbreviatedData_Arc_RejectsInvalidFlags()
    {
        var data = new AbbreviatedData();
        Assert.Throws<System.FormatException>(() => data.A(1, 1, 0, 2, 0, 0, 0));
        Assert.Throws<System.FormatException>(() => data.A(1, 1, 0, 0, 5, 0, 0));
    }

    [Fact]
    public void AbbreviatedData_AppendAndUndo()
    {
        var a = new AbbreviatedData().M(1, 1);
        var b = new AbbreviatedData().L(2, 2);
        a.Append(b);
        Assert.Equal(2, a.Size);
        Assert.Equal("M 1 1 L 2 2", a.ToDataString());

        a.Undo();
        Assert.Equal(1, a.Size);
        Assert.Equal("M 1 1", a.ToDataString());
    }

    [Fact]
    public void AbbreviatedData_ToCommands_AndBack()
    {
        var data = new AbbreviatedData()
            .S(0, 0).M(10, 10).L(20, 20).C().Flush();

        var cmds = data.ToCommands();
        Assert.Equal(4, cmds.Count);
        Assert.IsType<Move>(cmds[0]);   // S → Move
        Assert.IsType<Move>(cmds[1]);
        Assert.IsType<Line>(cmds[2]);
        Assert.IsType<Close>(cmds[3]);

        // 反向：指令序列 → 紧缩数据
        var back = AbbreviatedData.FromCommands(cmds);
        Assert.Equal("M 0 0 M 10 10 L 20 20 C", back.ToDataString());
    }

    // ────────────────────────────────────────────────────────
    //  绘制指令（Command）
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Commands_ToPathData_UsesOperatorPrefixes()
    {
        Assert.Equal("M 1 2", new Move(1, 2).ToPathData());
        Assert.Equal("L 3 4", new Line(3, 4).ToPathData());
        Assert.Equal("C", new Close().ToPathData());
        Assert.Equal("Q 1 2 3 4", new QuadraticBezier(1, 2, 3, 4).ToPathData());
        Assert.Equal("B 1 2 3 4 5 6", new CubicBezier(1, 2, 3, 4, 5, 6).ToPathData());
        Assert.Equal("A 5 3 30 1 0 7 8", new Arc(5, 3, 30, 1, 0, 7, 8).ToPathData());
    }

    [Fact]
    public void Commands_MissingPoint_FallsBackToZero()
    {
        Assert.Equal("M 0 0", new Move().ToPathData());
        Assert.Equal("L 0 0", new Line().ToPathData());
        Assert.Equal("A 0 0 0 0 0 0 0", new Arc().ToPathData());
    }

    [Fact]
    public void Arc_RotationAngle_Modulo360()
    {
        var arc = new Arc();
        arc.RotationAngle = 450;
        Assert.Equal(90, arc.RotationAngle, 6);
        Assert.Equal("90", arc.Element.Attribute("RotationAngle")!.Value);

        arc.RotationAngle = -30;
        Assert.Equal(-30, arc.RotationAngle, 6);
    }

    [Fact]
    public void Command_Serialization_RoundTrip()
    {
        var arc = new Arc(5, 3, 30, 1, 0, 7, 8);
        var el = RoundTrip(arc);
        var rt = new Arc(el);
        Assert.True(rt.LargeArc);
        Assert.False(rt.SweepDirection);
        Assert.Equal(30, rt.RotationAngle, 6);
        Assert.Equal(new STPos(7, 8), rt.EndPoint);
        Assert.Equal(2, rt.EllipseSize!.Count);
        Assert.Equal(5, rt.EllipseSize[0]);
        Assert.Equal(3, rt.EllipseSize[1]);
    }

    // ────────────────────────────────────────────────────────
    //  CT_Area / CT_Region
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CT_Region_Area_RoundTrip()
    {
        var region = new CT_Region();
        var area = new CT_Area { Start = new STPos(0, 0) };
        area.AddCommand(new Move(10, 10))
            .Next(new Line(20, 10))
            .Next(new Line(20, 20))
            .Next(new Close());
        region.AddArea(area);

        var el = RoundTrip(region);
        var rt = new CT_Region(el);

        var areas = rt.GetAreas();
        Assert.Single(areas);
        Assert.Equal(new STPos(0, 0), areas[0].Start);

        var cmds = areas[0].GetCommands();
        Assert.Equal(4, cmds.Count);
        Assert.IsType<Move>(cmds[0]);
        Assert.IsType<Line>(cmds[1]);
        Assert.IsType<Line>(cmds[2]);
        Assert.IsType<Close>(cmds[3]);
    }

    [Fact]
    public void CT_Area_ToAbbreviatedData_EmitsStartAndCommands()
    {
        var area = new CT_Area { Start = new STPos(1, 2) };
        area.AddCommand(new Line(10, 10))
            .AddCommand(new Arc(5, 3, 30, 1, 0, 7, 8))
            .AddCommand(new Close());

        var data = area.ToAbbreviatedData();
        Assert.Equal("S 1 2 L 10 10 A 5 3 30 1 0 7 8 C", data.ToDataString());
    }

    [Fact]
    public void CT_Area_UnknownCommand_Throws()
    {
        var area = new CT_Area { Start = new STPos(0, 0) };
        area.Element.Add(new XElement(OFDElement.OfdNs + "UnknownCmd"));
        Assert.Throws<System.ArgumentException>(() => area.GetCommands());
    }

    [Fact]
    public void CT_Region_AddArea_NullIgnored()
    {
        var region = new CT_Region();
        region.AddArea(null!);
        Assert.Empty(region.GetAreas());
    }
}
