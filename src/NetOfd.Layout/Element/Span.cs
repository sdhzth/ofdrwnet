using NetOfd.Core.PageDescription.Color;
using NetOfd.Font;

namespace NetOfd.Layout.Element;

/// <summary>
/// 文字跨越（文字片段），表示段落内的一段具有统一样式的文本。
/// 对应 Java: org.ofdrw.layout.element.Span
/// </summary>
public class Span
{
    // ── 文字内容 ─────────────────────────────────────────────
    /// <summary>文字内容，null 表示换行符</summary>
    public string? Content { get; set; }

    /// <summary>是否为换行符</summary>
    public bool IsLineBreak => Content is null;

    // ── 字体样式 ─────────────────────────────────────────────
    /// <summary>字体（null 表示继承段落默认）</summary>
    public OFDFont? Font { get; set; }

    /// <summary>字体大小（磅值），0 表示继承</summary>
    public double FontSize { get; set; } = 0;

    /// <summary>是否粗体</summary>
    public bool Bold { get; set; } = false;

    /// <summary>是否斜体</summary>
    public bool Italic { get; set; } = false;

    /// <summary>是否下划线</summary>
    public bool Underline { get; set; } = false;

    /// <summary>下划线与文字的偏移量（mm，可为负），仅 <see cref="Underline"/> = true 时生效。
    /// 对齐 Java <c>Span.setUnderline(boolean, double, double)</c> 第二参数，默认 1.2 mm。</summary>
    public double UnderlineOffset { get; set; } = 1.2d;

    /// <summary>下划线线宽（mm），仅 <see cref="Underline"/> = true 时生效；0 表示按字号自动（fontSize*0.05）。
    /// 对齐 Java <c>Span.setUnderline(boolean, double, double)</c> 第三参数。</summary>
    public double UnderlineWidth { get; set; } = 0d;

    /// <summary>字重（粗细值，可选 100/200/300/400/500/600/700/800/900，对应 OFD 表 45 Weight）。
    /// null 表示沿用 <see cref="Bold"/> 判定或字体资源默认（400）；非 null 时 OFD TextObject 直接写 Weight 属性。
    /// 对齐 Java <c>Span.setWeight(Weight)</c>。</summary>
    public int? Weight { get; set; } = null;

    /// <summary>是否删除线</summary>
    public bool StrikeThrough { get; set; } = false;

    /// <summary>字间距（相邻字符 X 方向推进增量，mm；0=字身宽度，对齐 Java <c>Span.setLetterSpacing</c>）。
    /// 病历表格紧凑排版、签名区下划线间距、方块字体方块间距等高频场景。</summary>
    public double LetterSpacing { get; set; } = 0;

    /// <summary>文字颜色（null 表示继承，默认黑色）</summary>
    public CT_Color? Color { get; set; }

    /// <summary>是否填充文字（OFD TextObject Fill 属性直 map，默认 true）。
    /// false 时文字不填充——需配合 <see cref="Stroke"/> = true 显示空心/轮廓字，
    /// 否则文字完全不可见（与 Java <c>Span.setFill(false)</c> 净语义一致）。
    /// 对齐 Java <c>Span.setFill(boolean)</c>（ParagraphRender 中
    /// <c>if (!s.isFill()) txtObj.setFill(false)</c> 直写 CT_Text）。</summary>
    public bool Fill { get; set; } = true;

    /// <summary>是否描边文字（OFD TextObject Stroke 属性直 map，默认 false）。
    /// 注：Java Span 未暴露此开关，为 NetOfd 扩展（对齐 CT_Text.setStroke(boolean) 规范属性）——
    /// 仅 <c>Fill=false</c> 时 OFD 渲染器按规范既无填充也无描边，文字不可见；
    /// 需同时 <c>Stroke=true</c>（可配 <see cref="StrokeColor"/>）才呈现空心轮廓字。</summary>
    public bool Stroke { get; set; } = false;

    /// <summary>描边颜色（OFD TextObject StrokeColor 子元素；null 时渲染层回退到填充色）。
    /// 注：Java Span 未暴露，为 NetOfd 扩展（对齐 CT_Text.setStrokeColor(CT_Color) 规范属性）。</summary>
    public CT_Color? StrokeColor { get; set; }

    // ── 构造 ─────────────────────────────────────────────────
    public Span() { }

    public Span(string content)
    {
        Content = content;
    }

    /// <summary>创建换行符 Span</summary>
    public static Span LineBreak() => new Span { Content = null };

    // ── Fluent API ────────────────────────────────────────────
    public Span SetContent(string content) { Content = content; return this; }
    public Span SetFont(OFDFont font) { Font = font; return this; }
    public Span SetFont(FontName name) { Font = new OFDFont(name); return this; }
    public Span SetFontSize(double size) { FontSize = size; return this; }
    public Span SetBold(bool bold = true) { Bold = bold; return this; }
    public Span SetItalic(bool italic = true) { Italic = italic; return this; }
    public Span SetUnderline(bool underline = true) { Underline = underline; return this; }

    /// <summary>启用下划线并指定偏移量（mm）与线宽（mm；0=按字号自动）。
    /// 对齐 Java <c>Span.setUnderline(boolean, double, double)</c>。</summary>
    public Span SetUnderline(bool underline, double offset, double width)
    {
        Underline = underline;
        UnderlineOffset = offset;
        UnderlineWidth = width;
        return this;
    }

    /// <summary>设置字重（粗细值，可选 100/200/300/400/500/600/700/800/900）。
    /// 对齐 Java <c>Span.setWeight(Weight)</c>。</summary>
    public Span SetWeight(int weight)
    {
        if (weight < 100 || weight > 900 || weight % 100 != 0)
            throw new ArgumentOutOfRangeException(nameof(weight), "Weight must be one of 100/200/300/400/500/600/700/800/900");
        Weight = weight;
        return this;
    }
    public Span SetLetterSpacing(double spacing) { LetterSpacing = spacing; return this; }
    public Span SetColor(CT_Color color) { Color = color; return this; }

    /// <summary>设置是否填充文字（对齐 Java <c>Span.setFill(boolean)</c>）。
    /// false 时需配合 <see cref="SetStroke"/> 呈现空心字。</summary>
    public Span SetFill(bool fill = true) { Fill = fill; return this; }

    /// <summary>设置是否描边文字（NetOfd 扩展，对应 OFD TextObject Stroke 属性）。</summary>
    public Span SetStroke(bool stroke = true) { Stroke = stroke; return this; }

    /// <summary>设置描边颜色（NetOfd 扩展，对应 OFD TextObject StrokeColor 子元素）。</summary>
    public Span SetStrokeColor(CT_Color strokeColor) { StrokeColor = strokeColor; return this; }

    public override string ToString() => Content ?? "\\n";
}
