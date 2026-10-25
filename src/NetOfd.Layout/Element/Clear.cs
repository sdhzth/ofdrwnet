namespace NetOfd.Layout.Element;

/// <summary>
/// Div 元素对浮动元素（<see cref="AFloat"/>）的占用策略——控制左右两侧是否允许出现浮动元素。
/// <para>对应 Java: <c>org.ofdrw.layout.element.Clear</c>。</para>
/// <para>
/// NetOfd 当前仅在 <b>API 层面</b>与 Java 对齐（Div 暴露 <see cref="Clear"/> 属性 + <see cref="Div.SetClear"/> 链式）；
/// 浮动布局引擎（PageLayout 处理 Float/Clear 语义）尚未实现，属更大缺口，留作后续。
/// </para>
/// </summary>
public enum Clear
{
    /// <summary>共享：两侧都允许出现浮动元素（默认值由具体 Div 决定：Java 与 NetOfd 一致为 <see cref="Both"/>）。</summary>
    None,

    /// <summary>左侧不允许出现浮动元素。</summary>
    Left,

    /// <summary>右侧不允许出现浮动元素。</summary>
    Right,

    /// <summary>两侧均不允许出现浮动元素。</summary>
    Both
}