namespace NetOfd.Layout.Element;

/// <summary>
/// 占位符（空白占位文字单元）：占据指定宽度但不渲染任何文字内容。
/// 对齐 Java: org.ofdrw.layout.element.PlaceholderSpan。
/// 典型用途：签名区留白、对齐占位、模板填空位（与段落文字同行参与行宽/行对齐计算）。
/// 宽度两种构造方式（HoldWidth 优先于 HoldChars，Java 同规则）。
/// </summary>
public class PlaceholderSpan : Span
{
    /// <summary>占位数量（字符位，仅按 <see cref="PlaceholderSpan(int, double)"/> 构造有效）</summary>
    public int HoldNum { get; private set; }

    /// <summary>占位符总宽度（mm）；<see cref="SetHoldWidth"/> 后 HoldChars 失效（Java 同规则）</summary>
    public double HoldWidth { get; private set; }

    /// <summary>按绝对宽度创建占位符（高度 = 字身高度）</summary>
    /// <param name="holdWidth">占位宽度，mm</param>
    /// <param name="height">占位高度（作字身高度），mm</param>
    public PlaceholderSpan(double holdWidth, double height)
    {
        HoldWidth = holdWidth;
        FontSize = height;
    }

    /// <summary>按字符位数量创建占位符（宽度 = 数量 × (字号 + 字间距)）</summary>
    /// <param name="holdNum">占位字符数</param>
    /// <param name="fontSize">字身大小，mm</param>
    public PlaceholderSpan(int holdNum, double fontSize)
    {
        FontSize = fontSize;
        SetHoldChars(holdNum);
    }

    /// <summary>按字符位数量创建占位符，字号/字间距复制自参考 Span（宽度 = 数量 × (字号 + 字间距)）</summary>
    /// <param name="holdNum">占位字符数</param>
    /// <param name="sp">参考 Span（取其字号与字间距）</param>
    public PlaceholderSpan(int holdNum, Span sp)
    {
        FontSize = sp.FontSize;
        LetterSpacing = sp.LetterSpacing;
        SetHoldChars(holdNum);
    }

    /// <summary>重设占位字符数并重算宽度（宽度 = 数量 × (字号 + 字间距)）</summary>
    public PlaceholderSpan SetHoldChars(int holdNum)
    {
        HoldNum = holdNum;
        HoldWidth = holdNum * (FontSize + LetterSpacing);
        return this;
    }

    /// <summary>重设占位符宽度（mm）；设置后 <see cref="HoldNum"/> 失效（Java 同规则）</summary>
    public PlaceholderSpan SetHoldWidth(double holdWidth)
    {
        HoldWidth = holdWidth;
        return this;
    }
}
