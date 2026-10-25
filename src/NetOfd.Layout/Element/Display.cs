namespace NetOfd.Layout.Element;

/// <summary>
/// Div 在行内的表现形式（对应 Java: <c>org.ofdrw.layout.element.Display</c>）。
/// <para>注意：Java 的 <c>Div.setDisplay</c> 并不存储 display 值，而是<b>联动改写 Clear</b>——
/// <see cref="InlineBlock"/> → Clear.None（允许行内共享），<see cref="Block"/> → Clear.Both
/// （独占一段）；<c>getDisplay()</c> 由 isBlockElement/clear 推导。NetOfd 对齐该语义。</para>
/// </summary>
public enum Display
{
    /// <summary>
    /// 块级元素——生成块级盒，在正常流中前后产生换行（独占段）。
    /// </summary>
    Block,

    /// <summary>
    /// 内联块级元素——正常流中不产生换行，除非前一个元素是块级盒。
    /// </summary>
    InlineBlock,
}
