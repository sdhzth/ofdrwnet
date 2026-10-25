using NetOfd.Layout.Element.Canvas;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 画板（Div 的一种），用于更加自由地向页面中加入内容。
/// <para>
/// Canvas 的宽度和高度必须在创建时指定，绘制行为由 <see cref="IDrawer"/> 提供。
/// </para>
/// 对应 Java: org.ofdrw.layout.element.canvas.Canvas
/// </summary>
public class Canvas : Div
{
    /// <summary>绘制器</summary>
    public IDrawer? Drawer { get; set; }

    public override string ElementType => "Canvas";

    public Canvas(double width, double height, IDrawer drawer)
    {
        Width = width;
        Height = height;
        Drawer = drawer;
    }

    public Canvas(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public Canvas(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public Canvas(PageLayout style) : this(style.Width, style.Height) { }

    public Canvas SetDrawer(IDrawer drawer)
    {
        Drawer = drawer;
        return this;
    }

    public override Rectangle DoPrepare(double widthLimit)
    {
        double w = (Width ?? 0) + MarginLeft + MarginRight;
        double h = (Height ?? 0) + MarginTop + MarginBottom;
        return new Rectangle(w, h);
    }
}