using System.Globalization;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 类单元格特殊绘制器（区域占位区块便捷填充）。
/// <para>
/// 将文字/图片在给定矩形区域内自动换行，并按水平（<see cref="NetOfd.Layout.Element.TextAlign"/>）/
/// 垂直（<see cref="VerticalAlign"/>）对齐方式居中摆放，同时支持字号、字体、颜色、加粗、斜体、
/// 字间距、下划线、删除线等样式。
/// </para>
/// <para>
/// 对应 Java: org.ofdrw.layout.element.canvas.CellContentDrawer。
/// 与 Java 的差异：Java 通过包裹 <c>Canvas</c> 元素（其内部坐标系原点为单元左上角）绘制，由元素
/// 负责定位；NetOfd 无 Canvas 元素，因此本类直接实现 <see cref="IDrawer"/>，按全库一致的
/// 「页面绝对 mm」约定，将单元放置在 (X, Y)（单元左上角），并在绝对坐标系内绘制内容。
/// 可直接作为 <c>IDrawer</c> 喂给 <c>OfdEditor.FillAreaHolder</c>，或经
/// <c>FillAreaHolder(name, Action&lt;CellContentDrawer&gt;)</c> 自动按登记区域边界定位。
/// </para>
/// </summary>
public class CellContentDrawer : IDrawer
{
    /// <summary>单元格左下角 X 坐标（页面绝对 mm）。</summary>
    public double X { get; set; }
    /// <summary>单元格左下角 Y 坐标（页面绝对 mm）。</summary>
    public double Y { get; set; }
    /// <summary>单元格宽度（mm）。</summary>
    public double Width { get; set; }
    /// <summary>单元格高度（mm）。</summary>
    public double Height { get; set; }

    /// <summary>单元格文字内容（与图片互斥）。</summary>
    private string? _value;
    /// <summary>文字水平对齐方式，默认左对齐。</summary>
    public NetOfd.Layout.Element.TextAlign TextAlign { get; set; } = NetOfd.Layout.Element.TextAlign.Left;
    /// <summary>文字垂直对齐方式，默认居中。</summary>
    public VerticalAlign VerticalAlign { get; set; } = VerticalAlign.Center;
    /// <summary>文字颜色，CSS 颜色字符串（#000000 / rgb() / rgba() / 颜色名），默认黑色。</summary>
    public string Color { get; set; } = "#000000";
    /// <summary>字体名称，默认宋体。</summary>
    public string FontName { get; set; } = "宋体";
    /// <summary>字号（mm），默认 3。</summary>
    public double FontSize { get; set; } = 3;
    /// <summary>行间距（mm），默认 0.6。</summary>
    public double LineSpace { get; set; } = 0.6;
    /// <summary>是否加粗。</summary>
    public bool Bold { get; set; }
    /// <summary>是否斜体。</summary>
    public bool Italic { get; set; }
    /// <summary>字间距（mm，可为负），默认 0。</summary>
    public double LetterSpacing { get; set; }
    /// <summary>是否启用下划线。</summary>
    public bool Underline { get; set; }
    /// <summary>是否启用删除线。</summary>
    public bool DeleteLine { get; set; }
    /// <summary>图片内容（与文字互斥）。</summary>
    private Img? _img;

    /// <summary>
    /// 以矩形区域构造单元格绘制器（单元左上角定位在 (x, y)，页面绝对 mm）。
    /// </summary>
    public CellContentDrawer(double x, double y, double width, double height)
    {
        X = x; Y = y; Width = width; Height = height;
    }

    /// <summary>获取单元格文字内容。</summary>
    public string? GetValue() => _value;
    /// <summary>设置单元格文字内容（清空既有图片）。</summary>
    public CellContentDrawer SetValue(string value) { _value = value; _img = null; return this; }
    /// <summary>设置图片内容（清空既有文字），宽高由调用方指定（mm）。</summary>
    public CellContentDrawer SetImage(string imgPath, double w, double h)
    {
        _img = new Img(imgPath, w, h);
        _value = null;
        return this;
    }
    /// <summary>设置图片内容（清空既有文字），宽高由图片自身像素尺寸换算（mm）。</summary>
    public CellContentDrawer SetImage(string imgPath)
    {
        _img = new Img(imgPath, 0, 0);
        _value = null;
        return this;
    }

    /// <summary>设置水平对齐方式。</summary>
    public CellContentDrawer SetTextAlign(NetOfd.Layout.Element.TextAlign align) { TextAlign = align; return this; }
    /// <summary>设置垂直对齐方式。</summary>
    public CellContentDrawer SetVerticalAlign(VerticalAlign align) { VerticalAlign = align; return this; }
    /// <summary>设置文字颜色。</summary>
    public CellContentDrawer SetColor(string color)
    {
        if (string.IsNullOrEmpty(color)) throw new ArgumentException("颜色(color)不能为空", nameof(color));
        Color = color; return this;
    }
    /// <summary>设置字体名称。</summary>
    public CellContentDrawer SetFontName(string fontName) { FontName = fontName; return this; }
    /// <summary>设置字号（mm）。</summary>
    public CellContentDrawer SetFontSize(double fontSize)
    {
        if (fontSize <= 0) throw new ArgumentException("字号(fontSize)必须大于0", nameof(fontSize));
        FontSize = fontSize; return this;
    }
    /// <summary>设置行间距（mm）。</summary>
    public CellContentDrawer SetLineSpace(double lineSpace) { LineSpace = lineSpace; return this; }
    /// <summary>设置是否加粗。</summary>
    public CellContentDrawer SetBold(bool bold) { Bold = bold; return this; }
    /// <summary>设置是否斜体。</summary>
    public CellContentDrawer SetItalic(bool italic) { Italic = italic; return this; }
    /// <summary>设置字间距（mm）。</summary>
    public CellContentDrawer SetLetterSpacing(double letterSpacing) { LetterSpacing = letterSpacing; return this; }
    /// <summary>设置是否启用下划线。</summary>
    public CellContentDrawer SetUnderline(bool underline) { Underline = underline; return this; }
    /// <summary>设置是否启用删除线。</summary>
    public CellContentDrawer SetDeleteLine(bool deleteLine) { DeleteLine = deleteLine; return this; }

    /// <summary>获取图片路径（可能为 null）。</summary>
    public string? GetImgPath() => _img?.Path;

    /// <inheritdoc/>
    public void Draw(DrawContext ctx)
    {
        if (_img != null) DrawImg(ctx);
        else DrawText(ctx);
    }

    private void DrawImg(DrawContext ctx)
    {
        if (_img is null) return;
        double w = _img.Width, h = _img.Height;
        if (w <= 0 || h <= 0)
        {
            var px = ImageSizeProbe.TryGetPixelSize(_img.Path);
            if (px is { } p)
            {
                w = p.Width / CanvasPattern.Ppm;
                h = p.Height / CanvasPattern.Ppm;
            }
        }
        if (w <= 0 || h <= 0) return;

        double x = TextAlign switch
        {
            NetOfd.Layout.Element.TextAlign.Right or NetOfd.Layout.Element.TextAlign.End => Width - w,
            NetOfd.Layout.Element.TextAlign.Center => (Width - w) / 2d,
            _ => 0d,
        };
        double y = VerticalAlign switch
        {
            VerticalAlign.Bottom => Height - h,
            VerticalAlign.Top => 0d,
            _ => (Height - h) / 2d,
        };
        ctx.DrawImageFromFile(_img.Path, X + x, Y + y, w, h);
    }

    private void DrawText(DrawContext ctx)
    {
        if (string.IsNullOrEmpty(_value)) return;

        var fontStr = "";
        if (Italic) fontStr += "italic ";
        if (Bold) fontStr += "bold ";
        ctx.Font = $"{fontStr}{FontSize.ToString(CultureInfo.InvariantCulture)}mm {FontName}";
        if (!string.IsNullOrEmpty(Color)) ctx.FillStyle = Color;

        var lines = BreakLines(Width);
        if (lines.Count == 0) return;

        double contentHeight = (FontSize + LineSpace) * lines.Count;
        double firstBaseline = VerticalAlign switch
        {
            VerticalAlign.Top => FontSize,
            VerticalAlign.Bottom => Height - contentHeight + FontSize,
            _ => Height / 2 - contentHeight / 2 + FontSize,
        };

        double baselineY = firstBaseline; // 单元局部坐标系内的基线 Y（累加每行）
        foreach (var line in lines)
        {
            double offsetX = TextAlign switch
            {
                NetOfd.Layout.Element.TextAlign.Center => (Width - line.Width) / 2d,
                NetOfd.Layout.Element.TextAlign.Right or NetOfd.Layout.Element.TextAlign.End => Width - line.Width,
                _ => 0d,
            };
            double ax = X + offsetX;                       // 绝对 X（文字内容左缘）
            double ayTop = Y + (baselineY - FontSize);    // 绝对 Y（Boundary 顶 = 基线 - 字号）
            DrawLine(ctx, line.Text, ax, ayTop);

            double fontLineWidth = FontSize / 30;
            if (Underline)
            {
                ctx.Save();
                ctx.LineWidth = fontLineWidth;
                ctx.BeginPath();
                ctx.MoveTo(X + offsetX, Y + baselineY + fontLineWidth * 3.2);
                ctx.LineTo(X + offsetX + line.Width, Y + baselineY + fontLineWidth * 3.2);
                ctx.Stroke();
                ctx.Restore();
            }
            if (DeleteLine)
            {
                ctx.Save();
                ctx.LineWidth = fontLineWidth;
                ctx.BeginPath();
                ctx.MoveTo(X + offsetX, Y + baselineY - FontSize * 5d / 18d);
                ctx.LineTo(X + offsetX + line.Width, Y + baselineY - FontSize * 5d / 18d);
                ctx.Stroke();
                ctx.Restore();
            }

            baselineY += FontSize + LineSpace;
        }
    }

    private void DrawLine(DrawContext ctx, string text, double ax, double ayTop)
    {
        if (LetterSpacing == 0)
        {
            ctx.FillText(text, ax, ayTop);
            return;
        }
        double x = ax;
        foreach (char c in text)
        {
            ctx.FillText(c.ToString(), x, ayTop);
            x += CharWidth(c) + LetterSpacing;
        }
    }

    /// <summary>逐字符宽度（mm），与 DrawContext.MeasureTextWidth 的启发式一致（ASCII 0.6em / CJK 1.0em）。</summary>
    private double CharWidth(char c) => FontSize * (c > 255 ? 1.0 : 0.6);

    private sealed record TextLine(string Text, double Width);

    private List<TextLine> BreakLines(double width)
    {
        var lines = new List<TextLine>();
        if (_value is null) return lines;
        int n = _value.Length;
        double textLineWidth = 0;
        int offset = 0;
        for (int i = 0; i < n; i++)
        {
            char c = _value[i];
            if (c == '\n')
            {
                lines.Add(new TextLine(_value.Substring(offset, i - offset), textLineWidth));
                offset = i + 1;
                textLineWidth = 0;
                continue;
            }
            double cWidth = CharWidth(c) + (LetterSpacing != 0 ? LetterSpacing : 0);
            if (textLineWidth + cWidth > width)
            {
                lines.Add(new TextLine(_value.Substring(offset, i - offset), textLineWidth));
                offset = i;
                textLineWidth = cWidth;
            }
            else
            {
                textLineWidth += cWidth;
            }
        }
        if (offset < n)
            lines.Add(new TextLine(_value.Substring(offset), textLineWidth));
        return lines;
    }

    private sealed class Img
    {
        public string Path { get; }
        public double Width { get; }
        public double Height { get; }
        public Img(string path, double width, double height) { Path = path; Width = width; Height = height; }
    }
}
