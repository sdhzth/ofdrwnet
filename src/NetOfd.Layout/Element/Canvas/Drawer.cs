namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 绘制器接口。
/// 对应 Java: org.ofdrw.layout.element.canvas.Drawer
/// </summary>
public interface IDrawer
{
    /// <summary>在绘制上下文中绘制内容。</summary>
    void Draw(DrawContext ctx);
}