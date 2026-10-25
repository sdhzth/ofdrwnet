using System.Collections.Generic;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 测量的文字区域信息。
/// <para>
/// <see cref="Width"/> 的计算口径：每个字宽度之和 + 字间距 × (n - 1)，n 为文字数量。
/// </para>
/// 对应 Java: org.ofdrw.layout.element.canvas.TextMetricsArea
/// （Java 用 AWT <c>Rectangle2D</c> 描述单字区域，此处改用本项目的 <see cref="Rectangle"/>）
/// </summary>
public sealed class TextMetricsArea
{
    /// <summary>每个字符的大小（含位置）。</summary>
    public List<Rectangle> CharAreas { get; set; } = new();

    /// <summary>文字区域宽度（单位毫米 mm）。</summary>
    public double Width { get; set; }

    /// <summary>文字区域高度（单位毫米 mm）。</summary>
    public double Height { get; set; }

    /// <summary>字间距（mm）。</summary>
    public double LetterSpacing { get; set; }
}
