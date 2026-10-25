using NetOfd.Font;

namespace NetOfd.Layout.Element;

/// <summary>
/// 文本段落元素。
/// 对应 Java: org.ofdrw.layout.element.Paragraph
/// </summary>
public class Paragraph : Div
{
    /// <summary>默认字体</summary>
    public OFDFont DefaultFont { get; set; } = new OFDFont(FontName.SimSun);

    /// <summary>默认字体大小（磅值，1pt ≈ 0.353mm）</summary>
    public double DefaultFontSize { get; set; } = 9.0;

    /// <summary>行间距（mm），0 表示自动</summary>
    public double LineSpace { get; set; } = 0;

    /// <summary>首行缩进（字符数）。当 <see cref="FirstLineIndentWidth"/> &gt; 0 时被忽略。</summary>
    public int FirstLineIndent { get; set; } = 0;

    /// <summary>首行缩进绝对宽度（mm）。&gt; 0 时优先于 <see cref="FirstLineIndent"/> 字符数，
    /// 用于混合字号段落「段首固定 mm 缩进」精确控制（对齐 Java <c>Paragraph.setFirstLineIndentWidth</c>）。</summary>
    public double FirstLineIndentWidth { get; set; } = 0;

    /// <summary>文字对齐方式</summary>
    public TextAlign TextAlign { get; set; } = TextAlign.Left;

    private readonly List<Span> _spans = new();
    public IReadOnlyList<Span> Spans => _spans;

    /// <summary>无参构造。</summary>
    public Paragraph() { }

    /// <summary>以一段纯文本初始化段落（对齐 Java <c>Paragraph(String)</c>）。</summary>
    public Paragraph(string text) { AddText(text); }

    // ── 添加文字内容 ─────────────────────────────────────────
    /// <summary>添加 Span</summary>
    public Paragraph Add(Span span) { _spans.Add(span); return this; }

    /// <summary>添加纯文本（使用默认样式）</summary>
    public Paragraph AddText(string text)
    {
        _spans.Add(new Span(text));
        return this;
    }

    /// <summary>添加换行</summary>
    public Paragraph AddLineBreak()
    {
        _spans.Add(Span.LineBreak());
        return this;
    }

    // ── Fluent API ────────────────────────────────────────────
    public new Paragraph SetWidth(double w) { Width = w; return this; }
    public new Paragraph SetHeight(double h) { Height = h; return this; }
    public Paragraph SetFont(OFDFont font) { DefaultFont = font; return this; }
    public Paragraph SetFont(FontName name) { DefaultFont = new OFDFont(name); return this; }
    public Paragraph SetFontSize(double size) { DefaultFontSize = size; return this; }
    public Paragraph SetLineSpace(double space) { LineSpace = space; return this; }
    public Paragraph SetFirstLineIndent(int chars) { FirstLineIndent = chars; return this; }

    /// <summary>设置首行缩进绝对宽度（mm），对齐 Java <c>Paragraph.setFirstLineIndentWidth(double)</c>。
    /// 与 <see cref="SetFirstLineIndent"/> 互斥，绝对宽度优先。</summary>
    public Paragraph SetFirstLineIndentWidth(double widthMm)
    {
        if (widthMm < 0) throw new ArgumentOutOfRangeException(nameof(widthMm), "FirstLineIndentWidth must be >= 0");
        FirstLineIndentWidth = widthMm;
        return this;
    }

    /// <summary>清除首行缩进（绝对宽度归零 + 字符数归零），对齐 Java <c>Paragraph.clearFirstLineIndent</c>。</summary>
    public Paragraph ClearFirstLineIndent() { FirstLineIndent = 0; FirstLineIndentWidth = 0; return this; }
    public Paragraph SetTextAlign(TextAlign align) { TextAlign = align; return this; }
}

/// <summary>文本对齐方式（横向文本语义；Start/End 由单元格绘制器对齐 Java canvas.TextAlign 引入）</summary>
public enum TextAlign { Left, Center, Right, Justify, Start, End }
