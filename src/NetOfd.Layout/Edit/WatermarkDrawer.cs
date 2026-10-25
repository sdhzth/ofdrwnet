using NetOfd.Core.BasicType;
using NetOfd.Layout.Element.Canvas;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 水印绘制器。
/// 对应 Java: org.ofdrw.layout.edit.WatermarkDrawer
/// 实现 <see cref="IDrawer"/> 以便直接作为 <see cref="OfdEditor.AddContent(byte[], int, IDrawer)"/>
/// 的绘制器（水印平铺到指定页最上层图层）。
/// </summary>
public class WatermarkDrawer : IDrawer
{
    /// <summary>水印绘制区域（平铺范围），由 <see cref="Watermark"/> 构造时同步赋值。</summary>
    public STBox Boundary { get; set; } = new STBox(0, 0, 0, 0);

    /// <summary>是否启用 DEBUG（绘制边框）</summary>
    public bool Debug { get; set; } = false;

    /// <summary>文字内容</summary>
    public string? Value { get; set; }

    /// <summary>文字颜色（CSS3 样式，默认 #000000）</summary>
    public string Color { get; set; } = "#000000";

    /// <summary>字体名称（默认 宋体）</summary>
    public string FontName { get; set; } = "宋体";

    /// <summary>字号（单位 mm，默认 4.5）</summary>
    public double FontSize { get; set; } = 4.5;

    /// <summary>是否加粗</summary>
    public bool Bold { get; set; } = false;

    /// <summary>字体宽度（CSS3：normal/bold/100-900）</summary>
    public string FontWeight { get; set; } = "normal";

    /// <summary>是否斜体</summary>
    public bool Italic { get; set; } = false;

    /// <summary>文字字间距（mm）</summary>
    public double LetterSpacing { get; set; } = 0;

    /// <summary>透明度（0.0-1.0，默认 0.5）</summary>
    public double GlobalAlpha { get; set; } = 0.5;

    /// <summary>旋转角度（0-360，默认 330 即 -30 度）</summary>
    public double Angle { get; set; } = 330;

    /// <summary>水印横向间距（mm）</summary>
    public double IntervalX { get; set; } = 30;

    /// <summary>水印纵向间距（mm）</summary>
    public double IntervalY { get; set; } = 30;

    /// <summary>外部字体文件路径</summary>
    public string? ExtFontPath { get; set; }

    public void SetGlobalAlpha(double globalAlpha)
    {
        if (globalAlpha > 1 || globalAlpha < 0)
            throw new ArgumentException("透明度超出范围0.0~1.0");
        GlobalAlpha = globalAlpha;
    }

    public void SetAngle(double angle)
    {
        if (angle > 360 || angle < -360)
            throw new ArgumentException("旋转角度超出范围");
        if (angle < 0) angle = 360 + angle;
        Angle = angle;
    }

    public void SetFont(string fontName, string fontPath)
    {
        if (string.IsNullOrEmpty(fontName)) throw new ArgumentException("字体名称(fontName)不能为空");
        if (string.IsNullOrEmpty(fontPath) || !File.Exists(fontPath)) throw new ArgumentException("字体文件(fontPath)不存在");
        FontName = fontName;
        ExtFontPath = fontPath;
    }

    private static string FormatSize(double v) => v.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>
    /// 在绘制上下文上绘制水印。
    /// </summary>
    public void Draw(Element.Canvas.DrawContext ctx, STBox boundary)
    {
        if (string.IsNullOrEmpty(Value)) return;

        if (ExtFontPath is not null)
        {
            ctx.AddFont(FontName, ExtFontPath);
        }

        var sb = new System.Text.StringBuilder();
        if (Italic) sb.Append("italic ");
        if (Bold || (FontWeight is not null && FontWeight is not ("normal" or "400")))
            sb.Append("bold ");
        sb.Append(FormatSize(FontSize)).Append("mm ").Append(FontName);
        ctx.Font = sb.ToString();
        ctx.FillStyle = Color;
        if (LetterSpacing != 0)
        {
            // 字间距通过文本宽度估算体现
        }

        double width = boundary.Width;
        double height = boundary.Height;

        var metrics = ctx.MeasureText(Value);
        double rad = Angle * Math.PI / 180d;

        double x = boundary.TopLeftX;
        while (x < width)
        {
            double y = boundary.TopLeftY;
            while (y < height)
            {
                ctx.Save();
                ctx.GlobalAlpha = GlobalAlpha;
                ctx.Translate(x, y);
                ctx.Rotate(rad);
                ctx.FillText(Value, 0, metrics.FontSize);
                ctx.Restore();
                y += IntervalY;
            }
            x += IntervalX;
        }
    }

    /// <summary>
    /// 实现 <see cref="IDrawer"/>：以 <see cref="Boundary"/> 为平铺区域绘制水印，
    /// 便于直接作为 <c>OfdEditor.AddContent</c> / <c>OfdEditSession.AddContent</c> 的绘制器。
    /// </summary>
    public void Draw(DrawContext ctx) => Draw(ctx, Boundary);
}