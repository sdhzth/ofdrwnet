namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// Canvas 扩展基类，用于快速构建基于 Canvas 的自定义元素。
/// <para>
/// 继承本类后只需实现 <see cref="IDrawer.Draw"/>，构造时会自动
/// <c>SetDrawer(this)</c>，省去"先 new Canvas 再挂绘制器"的样板代码。
/// </para>
/// <para>
/// 一个最简单的实现可参考 <see cref="Line"/>。
/// </para>
/// 对应 Java: org.ofdrw.layout.element.canvas.CanvasBase
/// </summary>
public abstract class CanvasBase : Canvas, IDrawer
{
    protected CanvasBase(double width, double height) : base(width, height)
    {
        SetDrawer(this);
    }

    protected CanvasBase(double x, double y, double w, double h) : base(x, y, w, h)
    {
        SetDrawer(this);
    }

    protected CanvasBase(PageLayout style) : base(style)
    {
        SetDrawer(this);
    }

    /// <summary>子类在此实现自定义绘制逻辑。</summary>
    public abstract void Draw(DrawContext ctx);
}
