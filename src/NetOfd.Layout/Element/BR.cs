namespace NetOfd.Layout.Element;

/// <summary>
/// 换行符（强制断行/断段）。
/// <para>解析/排版引擎遇到该元素时自动结束当前浮动行（Segment），后续元素从行底以下开新行。
/// 仅在流式布局中有效。</para>
/// 对应 Java: <c>org.ofdrw.layout.element.BR</c>（空 <c>Div</c> 子类，
/// SegmentationEngine 遇到 BR 即封段开新段；段为空时忽略）。
/// </summary>
public class BR : Div
{
    public BR() { }
}
