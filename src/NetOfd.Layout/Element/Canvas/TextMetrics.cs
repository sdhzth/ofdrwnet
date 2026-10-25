namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 文字度量结果。
/// 对应 Java: org.ofdrw.layout.element.canvas.TextMetrics
/// </summary>
public class TextMetrics
{
    /// <summary>文字宽度（mm）</summary>
    public double Width { get; set; }

    /// <summary>字号（mm）</summary>
    public double FontSize { get; set; }

    /// <summary>字体上行高度（mm）</summary>
    public double Ascent { get; set; }

    /// <summary>字体下行深度（mm）</summary>
    public double Descent { get; set; }

    public TextMetrics() { }

    public TextMetrics(double width, double fontSize)
    {
        Width = width;
        FontSize = fontSize;
        Ascent = fontSize;
        Descent = 0;
    }
}