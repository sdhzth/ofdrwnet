using System.IO;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Layout.Engine;
using NetOfd.Layout.Exceptions;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// Layout 模块缺口补齐测试：DocReadException / ExistCTFont / GraphHelper /
/// CanvasBase / IElementSplit / MeasureBody / TextMetricsArea。
/// 对齐 Java: org.ofdrw.layout.*
/// </summary>
public class LayoutGapsTest
{
    // ────────────────────────────────────────────────────────
    //  DocReadException
    // ────────────────────────────────────────────────────────

    [Fact]
    public void DocReadException_IsIOException()
    {
        var ex = new DocReadException("文件损坏");
        Assert.IsAssignableFrom<IOException>(ex);
        Assert.Equal("文件损坏", ex.Message);
    }

    [Fact]
    public void DocReadException_KeepsInnerException()
    {
        var inner = new IOException("底层IO失败");
        var ex = new DocReadException("解析失败", inner);
        Assert.Same(inner, ex.InnerException);
    }

    [Fact]
    public void DocReadException_Parameterless()
    {
        Assert.NotNull(new DocReadException().Message);
    }

    // ────────────────────────────────────────────────────────
    //  ExistCTFont
    // ────────────────────────────────────────────────────────

    [Fact]
    public void ExistCTFont_HoldsFontAndPath()
    {
        var font = new CT_Font { ID = new NetOfd.Core.BasicType.STID(3), FontName = "SimSun" };
        var exist = new ExistCTFont(font, @"C:\Windows\Fonts\simsun.ttc");

        Assert.Same(font, exist.Font);
        Assert.Equal(@"C:\Windows\Fonts\simsun.ttc", exist.AbsPath);
    }

    [Fact]
    public void ExistCTFont_SystemFont_PathCanBeNull()
    {
        // 系统自带字体无需随文档打包字体文件，路径允许为空
        var exist = new ExistCTFont(new CT_Font(), null);
        Assert.Null(exist.AbsPath);
    }

    // ────────────────────────────────────────────────────────
    //  GraphHelper
    // ────────────────────────────────────────────────────────

    [Fact]
    public void GraphHelper_Rect_ProducesClosedRectanglePath()
    {
        var data = GraphHelper.Rect(0, 0, 10, 5);

        // M 起点 → L 右上 → L 右下 → L 左下 → C 闭合
        Assert.Equal("M 0 0 L 10 0 L 10 5 L 0 5 C", data.ToDataString());
    }

    [Fact]
    public void GraphHelper_Rect_HonorsOrigin()
    {
        var data = GraphHelper.Rect(2, 3, 10, 5);
        Assert.Equal("M 2 3 L 12 3 L 12 8 L 2 8 C", data.ToDataString());
    }

    [Fact]
    public void GraphHelper_Rect_IsFlushedToElementText()
    {
        // 必须已 Flush，否则元素文本为空、写出的 OFD 会丢失图形
        var data = GraphHelper.Rect(0, 0, 4, 4);
        Assert.False(string.IsNullOrWhiteSpace(data.Value));
        Assert.Equal(data.ToDataString(), data.Value);
    }

    [Fact]
    public void GraphHelper_Rect_FromRectangle()
    {
        var data = GraphHelper.Rect(new NetOfd.Layout.Rectangle(1, 2, 6, 7));
        Assert.Equal("M 1 2 L 7 2 L 7 9 L 1 9 C", data.ToDataString());
    }

    [Fact]
    public void GraphHelper_Rect_HasFiveOperators()
    {
        var data = GraphHelper.Rect(0, 0, 1, 1);
        Assert.Equal(5, data.Size);
        Assert.Equal("M", data.RawOptVal[0].Opt);
        Assert.Equal("C", data.RawOptVal[4].Opt);
    }

    // ────────────────────────────────────────────────────────
    //  CanvasBase
    // ────────────────────────────────────────────────────────

    private sealed class SampleCanvas : CanvasBase
    {
        public bool Drawn { get; private set; }

        public SampleCanvas(double width, double height) : base(width, height) { }

        public SampleCanvas(double x, double y, double w, double h) : base(x, y, w, h) { }

        public override void Draw(DrawContext ctx) => Drawn = true;
    }

    [Fact]
    public void CanvasBase_AutoRegistersSelfAsDrawer()
    {
        var canvas = new SampleCanvas(20, 10);

        // 构造时已 SetDrawer(this)，无需外部再挂
        Assert.Same(canvas, canvas.Drawer);
        Assert.Equal(20, canvas.Width);
        Assert.Equal(10, canvas.Height);
    }

    [Fact]
    public void CanvasBase_PositionCtor_SetsXY()
    {
        var canvas = new SampleCanvas(5, 6, 20, 10);
        Assert.Equal(5, canvas.X);
        Assert.Equal(6, canvas.Y);
        Assert.Same(canvas, canvas.Drawer);
    }

    [Fact]
    public void CanvasBase_InheritsCanvasElementType()
    {
        Assert.Equal("Canvas", new SampleCanvas(1, 1).ElementType);
    }

    // ────────────────────────────────────────────────────────
    //  Java ElementSplit 的能力已由 Div.Split 内建（无需移植接口）
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Div_Split_CoversJavaElementSplitContract()
    {
        var div = new Div { Width = 50, Height = 40 };
        var parts = div.Split(15);

        Assert.Equal(2, parts.Length);
        // 上半段布局占位高度应恰等于切分高度
        Assert.Equal(15d, parts[0].Height!.Value + parts[0].HeightPlus());
    }

    [Fact]
    public void Div_Split_ReturnsSelfWhenFitsRemainingSpace()
    {
        var div = new Div { Width = 50, Height = 40 };
        var parts = div.Split(100);

        Assert.Single(parts);
        Assert.Same(div, parts[0]);
    }

    [Fact]
    public void Div_Split_RequiresFixedSize()
    {
        // 与 Java 语义一致：宽度/高度未确定时不允许切分
        var div = new Div { Width = 50 };
        Assert.Throws<System.InvalidOperationException>(() => div.Split(10));
    }

    // ────────────────────────────────────────────────────────
    //  MeasureBody
    // ────────────────────────────────────────────────────────

    [Fact]
    public void MeasureBody_DefaultsAreZero()
    {
        var body = new MeasureBody();
        Assert.Empty(body.Offset);
        Assert.Equal(0d, body.Width);
        Assert.Equal(0d, body.FirstCharOffsetX);
        Assert.Equal(0d, body.FirstCharOffsetY);
    }

    [Fact]
    public void MeasureBody_With_SumsAbsoluteOffsetsPlusCharLen()
    {
        var body = new MeasureBody { Offset = new[] { 1.5, -2.5 } };

        // |1.5| + |-2.5| + 3 = 7
        body.With(3);

        Assert.Equal(7d, body.Width);
    }

    [Fact]
    public void MeasureBody_With_EmptyOffset_EqualsCharLen()
    {
        var body = new MeasureBody();
        body.With(4.2);
        Assert.Equal(4.2d, body.Width);
    }

    // ────────────────────────────────────────────────────────
    //  TextMetricsArea
    // ────────────────────────────────────────────────────────

    [Fact]
    public void TextMetricsArea_HoldsPerCharAreasAndTotals()
    {
        var area = new TextMetricsArea
        {
            Width = 42,
            Height = 8,
            LetterSpacing = 0.5
        };
        area.CharAreas.Add(new NetOfd.Layout.Rectangle(0, 0, 6, 8));
        area.CharAreas.Add(new NetOfd.Layout.Rectangle(6.5, 0, 6, 8));

        Assert.Equal(2, area.CharAreas.Count);
        Assert.Equal(42d, area.Width);
        Assert.Equal(8d, area.Height);
        Assert.Equal(0.5d, area.LetterSpacing);
        Assert.Equal(6d, area.CharAreas[0].Width);
    }

    [Fact]
    public void TextMetricsArea_DefaultsToEmptyCharAreas()
    {
        Assert.Empty(new TextMetricsArea().CharAreas);
    }
}
