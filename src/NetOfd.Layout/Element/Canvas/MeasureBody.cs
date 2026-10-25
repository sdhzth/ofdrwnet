using System;
using System.Linq;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 文本测量结果：字符间相对偏移量 + 阅读方向上的总宽度。
/// <para>
/// 布局引擎测算一段文本时用它承载结果：<see cref="Offset"/> 记录每个字符相对前一个字符的
/// 偏移（含正负），<see cref="Width"/> 为整段文本在<b>阅读方向</b>上的总宽度。
/// </para>
/// 对应 Java: org.ofdrw.layout.element.canvas.MeasureBody
/// </summary>
public sealed class MeasureBody
{
    /// <summary>文本字符间相对偏移量。</summary>
    public double[] Offset { get; set; } = Array.Empty<double>();

    /// <summary>文本在阅读方向上的总宽度（mm）。</summary>
    public double Width { get; set; }

    /// <summary>文本第一个字符的相对偏移 X 坐标。</summary>
    public double FirstCharOffsetX { get; set; }

    /// <summary>文本第一个字符的相对偏移 Y 坐标。</summary>
    public double FirstCharOffsetY { get; set; }

    /// <summary>
    /// 以"偏移绝对值之和 + 字符宽度"计算并写入 <see cref="Width"/>。
    /// </summary>
    /// <param name="charLen">字符宽度（mm）</param>
    public void With(double charLen)
    {
        Width = Offset.Sum(Math.Abs) + charLen;
    }
}
