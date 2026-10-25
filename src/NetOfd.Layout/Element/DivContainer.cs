namespace NetOfd.Layout.Element;

/// <summary>
/// 控制 Div 元素的逻辑标记接口——Div 元素实现此接口后被视作<b>纯逻辑容器</b>，
/// 渲染器不绘制 Div 相关的边框/背景/外边距等任何属性，仅作为布局容器存在。
/// <para>对应 Java: <c>org.ofdrw.layout.element.DivContainer</c>（Java 库中已定义但
/// 实际无 Div 子类实现，预留扩展点）。NetOfd 同形态对齐，保持 API 一致性。</para>
/// </summary>
public interface DivContainer
{
}