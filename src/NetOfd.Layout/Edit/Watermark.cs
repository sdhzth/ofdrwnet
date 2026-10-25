using NetOfd.Core.BasicType;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 水印，是一种特殊的注释。
/// 对应 Java: org.ofdrw.layout.edit.Watermark
/// </summary>
public class Watermark
{
    /// <summary>水印绘制区域</summary>
    public STBox Boundary { get; set; }

    /// <summary>水印绘制器</summary>
    public WatermarkDrawer Drawer { get; set; }

    /// <summary>
    /// 根据页面布局样式构建水印对象（让绘制画布包裹页面）。
    /// </summary>
    public Watermark(PageLayout style)
        : this(0, 0, style.Width + 50, style.Height + 50)
    {
    }

    /// <summary>
    /// 根据水印绘制区域坐标和大小构建水印对象。
    /// </summary>
    public Watermark(double x, double y, double width, double height)
    {
        Boundary = new STBox(x, y, width, height);
        Drawer = new WatermarkDrawer { Boundary = Boundary };
    }

    public Watermark SetValue(string value) { Drawer.Value = value; return this; }
    public string GetValue() => Drawer.Value ?? string.Empty;

    public Watermark SetColor(string color) { Drawer.Color = color; return this; }
    public string GetColor() => Drawer.Color;

    public Watermark SetFontName(string fontName) { Drawer.FontName = fontName; return this; }
    public string GetFontName() => Drawer.FontName;

    public Watermark SetFontSize(double fontSize) { Drawer.FontSize = fontSize; return this; }
    public double GetFontSize() => Drawer.FontSize;

    public Watermark SetBold(bool bold) { Drawer.Bold = bold; return this; }
    public bool GetBold() => Drawer.Bold;

    public Watermark SetFontWeight(string fontWeight) { Drawer.FontWeight = fontWeight; return this; }
    public string GetFontWeight() => Drawer.FontWeight;

    public Watermark SetItalic(bool italic) { Drawer.Italic = italic; return this; }
    public bool GetItalic() => Drawer.Italic;

    public Watermark SetLetterSpacing(double letterSpacing) { Drawer.LetterSpacing = letterSpacing; return this; }
    public double GetLetterSpacing() => Drawer.LetterSpacing;

    public Watermark SetGlobalAlpha(double globalAlpha) { Drawer.SetGlobalAlpha(globalAlpha); return this; }
    public double GetGlobalAlpha() => Drawer.GlobalAlpha;

    public Watermark SetAngle(double angle) { Drawer.SetAngle(angle); return this; }
    public double GetAngle() => Drawer.Angle;

    public Watermark SetIntervalX(double intervalX) { Drawer.IntervalX = intervalX; return this; }
    public double GetIntervalX() => Drawer.IntervalX;

    public Watermark SetIntervalY(double intervalY) { Drawer.IntervalY = intervalY; return this; }
    public double GetIntervalY() => Drawer.IntervalY;

    public Watermark SetFont(string fontName, string fontPath) { Drawer.SetFont(fontName, fontPath); return this; }

    public Watermark Debug() { Drawer.Debug = true; return this; }
}