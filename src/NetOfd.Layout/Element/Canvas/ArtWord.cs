using System;
using System.Globalization;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 艺术字绘制器。
/// <para>
/// 在 <see cref="Span"/> 效果基础上附加艺术字常见效果：斜体、文字左右/垂直拉伸、水平/垂直倾斜、
/// 字间距、下划线、水平对齐，并支持整体偏移。
/// </para>
/// <para>
/// 对应 Java: org.ofdrw.layout.element.canvas.ArtWord（extends CanvasBase）。
/// 与 Java 的差异：Java 经 <c>Canvas</c> 元素包裹、其内部坐标系原点为元素左上角、由引擎负责定位；
/// NetOfd 遵循全库一致的「页面绝对 mm」约定，本类直接实现 <see cref="IDrawer"/>，以
/// (X, Y)（文字盒左上角，页面绝对 mm）定位内容。可作 <c>Canvas(w, h, artWord)</c> 的绘制器使用。
/// </para>
/// </summary>
public class ArtWord : IDrawer
{
    /// <summary>文字盒左上角 X（页面绝对 mm）。</summary>
    public double X { get; set; }
    /// <summary>文字盒左上角 Y（页面绝对 mm）。</summary>
    public double Y { get; set; }
    /// <summary>声明盒宽度（mm），用于水平对齐参照（Java getWidth()）。</summary>
    public double Width { get; set; }
    /// <summary>声明盒高度（mm）。</summary>
    public double Height { get; set; }

    /// <summary>字体名称，默认宋体。</summary>
    public string FontName { get; set; } = "SimSun";
    /// <summary>字号（mm），默认 3。</summary>
    public double FontSize { get; set; } = 3;
    /// <summary>字间距（mm），默认 0。负值修正为 0（同 Java FontSetting.setLetterSpacing）。</summary>
    public double _letterSpacing;
    /// <summary>是否加粗。</summary>
    public bool Bold { get; set; }
    /// <summary>字体粗细（100-900），null 表示未显式指定。</summary>
    public int? Weight { get; set; }
    /// <summary>是否斜体。</summary>
    public bool Italic { get; set; }
    /// <summary>是否启用下划线。</summary>
    public bool Underline { get; set; }
    /// <summary>下划线与文字基线偏移量（mm），默认 1.2。</summary>
    public double UnderlineOffset { get; set; } = 1.2;
    /// <summary>下划线线宽（mm），0 表示默认（字号 × 0.05）。</summary>
    public double UnderlineWidth { get; set; }

    /// <summary>字体颜色，CSS 颜色字符串，默认黑色。</summary>
    public string Color { get; set; } = "#000000";

    /// <summary>文本内容对齐方式（绘制起始位置），默认左对齐。</summary>
    public NetOfd.Layout.Element.TextAlign TextAlign { get; set; } = NetOfd.Layout.Element.TextAlign.Left;

    /// <summary>水平缩放：&lt;1 挤压、1 正常、&gt;1 拉伸。</summary>
    public double HorizontalScaling { get; set; } = 1D;
    /// <summary>垂直缩放：&lt;1 挤压、1 正常、&gt;1 拉伸。</summary>
    public double VerticalScaling { get; set; } = 1D;
    /// <summary>水平倾斜：上边框固定、水平移动下边框，正右负左（mm）。</summary>
    public double HorizontalInclination { get; set; }
    /// <summary>垂直倾斜：左边框固定、垂直移动右边框，正下负上（mm）。</summary>
    public double VerticalInclination { get; set; }
    /// <summary>水平偏移量（mm）。</summary>
    public double OffsetX { get; set; }
    /// <summary>垂直偏移量（mm）。</summary>
    public double OffsetY { get; set; }

    public ArtWord(double width, double height)
    {
        Width = width;
        Height = height;
    }

    /// <summary>设置文字内容盒左上角（页面绝对 mm）。</summary>
    public ArtWord SetPosition(double x, double y) { X = x; Y = y; return this; }
    /// <summary>设置字体名称。</summary>
    public ArtWord SetFontName(string fontName) { FontName = fontName; return this; }
    /// <summary>设置字号（mm）。</summary>
    public ArtWord SetFontSize(double fontSize) { FontSize = fontSize; return this; }
    /// <summary>设置字间距（mm，负值修正为 0）。</summary>
    public ArtWord SetLetterSpacing(double letterSpacing) { _letterSpacing = letterSpacing < 0 ? 0 : letterSpacing; return this; }
    /// <summary>设置是否加粗。</summary>
    public ArtWord SetBold(bool bold) { Bold = bold; return this; }
    /// <summary>设置字体粗细（100-900）。</summary>
    public ArtWord SetWeight(int weight) { Weight = weight; return this; }
    /// <summary>设置是否斜体。</summary>
    public ArtWord SetItalic(bool italic) { Italic = italic; return this; }
    /// <summary>设置是否启用下划线（默认偏移 1.2、默认线宽）。</summary>
    public ArtWord SetUnderline(bool underline) { Underline = underline; return this; }
    /// <summary>设置下划线及偏移、线宽（mm）。</summary>
    public ArtWord SetUnderline(bool underline, double offset, double width)
    {
        Underline = underline; UnderlineOffset = offset; UnderlineWidth = width; return this;
    }
    /// <summary>设置字体颜色（CSS 颜色字符串）。</summary>
    public ArtWord SetColor(string color) { Color = color; return this; }
    /// <summary>设置文本内容对齐方式。</summary>
    public ArtWord SetTextAlign(NetOfd.Layout.Element.TextAlign align) { TextAlign = align; return this; }
    /// <summary>设置水平缩放（&lt;1 挤压、1 正常、&gt;1 拉伸）。</summary>
    public ArtWord SetHorizontalScaling(double scale) { HorizontalScaling = scale; return this; }
    /// <summary>设置垂直缩放（&lt;1 挤压、1 正常、&gt;1 拉伸）。</summary>
    public ArtWord SetVerticalScaling(double scale) { VerticalScaling = scale; return this; }
    /// <summary>设置水平倾斜度（mm）。</summary>
    public ArtWord SetHorizontalInclination(double inclination) { HorizontalInclination = inclination; return this; }
    /// <summary>设置垂直倾斜度（mm）。</summary>
    public ArtWord SetVerticalInclination(double inclination) { VerticalInclination = inclination; return this; }
    /// <summary>设置水平偏移量（mm）。</summary>
    public ArtWord SetOffsetX(double offsetX) { OffsetX = offsetX; return this; }
    /// <summary>设置垂直偏移量（mm）。</summary>
    public ArtWord SetOffsetY(double offsetY) { OffsetY = offsetY; return this; }

    /// <summary>文本内容。</summary>
    private string _text = "";

    /// <summary>获取文本内容。</summary>
    public string GetText() => _text;
    /// <summary>设置文本内容。</summary>
    public ArtWord SetText(string text) { _text = text ?? ""; return this; }

    /// <summary>字符宽度（mm）（与 DrawContext 启发式一致：ASCII 0.6em / CJK 1.0em）。</summary>
    private double CharWidth(char c) => FontSize * (c > 255 ? 1.0 : 0.6);

    private double LetterSpacing => _letterSpacing;

    /// <summary>文本总宽度（mm），含字间距。</summary>
    public double MeasureWidth()
    {
        if (string.IsNullOrEmpty(_text)) return 0;
        double w = 0;
        for (int i = 0; i < _text.Length; i++)
        {
            w += CharWidth(_text[i]);
            if (i < _text.Length - 1) w += LetterSpacing;
        }
        return w;
    }

    /// <inheritdoc/>
    public void Draw(DrawContext ctx)
    {
        if (string.IsNullOrEmpty(_text)) return;

        ctx.Save();
        try
        {
            // 字体：斜体/加粗 前缀 + 字号(mm) + 字族（同 CellContentDrawer 惯例）
            var fontStr = "";
            if (Italic) fontStr += "italic ";
            if (Bold || (Weight is int w && w >= 700)) fontStr += "bold ";
            ctx.Font = $"{fontStr}{FontSize.ToString(CultureInfo.InvariantCulture)}mm {FontName}";
            ctx.FillStyle = Color;

            double textWidth = MeasureWidth();

            // 水平对齐：计算局部起始 X（Java textLayoutAlign：left/center/right）
            double x = 0;
            switch (TextAlign)
            {
                case NetOfd.Layout.Element.TextAlign.Right:
                case NetOfd.Layout.Element.TextAlign.End:
                    x = Width - textWidth;
                    break;
                case NetOfd.Layout.Element.TextAlign.Center:
                    x = (Width - textWidth) / 2d;
                    break;
                default:
                    x = 0;
                    break;
            }

            // 艺术字变换：a=水平缩放, b=垂直倾斜, c=水平倾斜, d=垂直缩放, e/f=平移(盒原点 + 偏移)
            // 对应 Java ArtWord.draw 的 ctx.setTransform(hs, vi, hi, vs, offsetX, offsetY)，
            // 此处将元素绝对位置 (X,Y) 并入平移项。
            ctx.SetTransform(HorizontalScaling, VerticalInclination,
                HorizontalInclination, VerticalScaling,
                X + OffsetX, Y + OffsetY);

            // 基线 Y = 字号（盒局部坐标，由变换映射到绝对）
            var baseline = FontSize;
            FillTextSpaced(ctx, x, baseline);

            if (Underline)
            {
                double lw = UnderlineWidth == 0 ? FontSize * 0.05 : UnderlineWidth;
                ctx.Save();
                ctx.LineWidth = lw;
                ctx.StrokeStyle = Color;
                ctx.GlobalAlpha = 1D;
                ctx.BeginPath();
                ctx.MoveTo(x, baseline + OffsetY + UnderlineOffset);
                ctx.LineTo(x + textWidth, baseline + OffsetY + UnderlineOffset);
                ctx.Stroke();
                ctx.Restore();
            }
        }
        finally
        {
            ctx.Restore();
        }
    }

    private void FillTextSpaced(DrawContext ctx, double x, double baselineY)
    {
        if (string.IsNullOrEmpty(_text)) return;
        if (LetterSpacing == 0)
        {
            ctx.FillText(_text, x, baselineY);
            return;
        }
        double cx = x;
        for (int i = 0; i < _text.Length; i++)
        {
            ctx.FillText(_text[i].ToString(), cx, baselineY);
            cx += CharWidth(_text[i]) + LetterSpacing;
        }
    }
}
