namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 线条绘制器，用于快速绘制一条线。
/// <para>
/// 对应 Java: org.ofdrw.layout.element.canvas.Line（extends CanvasBase）。
/// 与 Java 的差异：Java 经 <c>Canvas</c> 元素包裹、起点/终点为元素局部坐标、由引擎定位；
/// NetOfd 遵循「页面绝对 mm」约定，本类直接实现 <see cref="IDrawer"/>，(X, Y) 为画线区域
/// 左上角（页面绝对 mm），起点/终点为相对 (X, Y) 的局部坐标。可作 <c>Canvas(w, h, line)</c> 的绘制器。
/// </para>
/// </summary>
public class Line : IDrawer
{
    /// <summary>画线区域左上角 X（页面绝对 mm）。</summary>
    public double X { get; set; }
    /// <summary>画线区域左上角 Y（页面绝对 mm）。</summary>
    public double Y { get; set; }
    /// <summary>画线区域宽度（mm）。</summary>
    public double Width { get; set; }
    /// <summary>画线区域高度（mm）。</summary>
    public double Height { get; set; }

    /// <summary>起点（相对 X/Y 的局部坐标）。</summary>
    private double[] _beginPoint = { 0, 0 };
    /// <summary>终点（相对 X/Y 的局部坐标）。</summary>
    private double[] _endPoint = { 0, 0 };

    /// <summary>线条颜色（#000000 / rgb() / rgba() / 颜色名），默认黑。</summary>
    public string LineColor { get; set; } = "#000000";
    /// <summary>线条宽度（mm），默认 0.353。</summary>
    public double LineWidth { get; set; } = 0.353d;
    /// <summary>线条透明度 [0,1]，默认 1。</summary>
    public double LineOpacity { get; set; } = 1.0d;

    public Line(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public Line(double x, double y, double w, double h)
    {
        X = x; Y = y; Width = w; Height = h;
    }

    /// <summary>设置画线区域左上角（页面绝对 mm）。</summary>
    public Line SetPosition(double x, double y) { X = x; Y = y; return this; }
    /// <summary>设置起点（局部坐标）。</summary>
    public Line SetBeginPoint(double beginX, double beginY) { _beginPoint = new[] { beginX, beginY }; return this; }
    /// <summary>设置终点（局部坐标）。</summary>
    public Line SetEndPoint(double endX, double endY) { _endPoint = new[] { endX, endY }; return this; }
    /// <summary>设置线条颜色。</summary>
    public Line SetLineColor(string color) { LineColor = color; return this; }
    /// <summary>设置线条宽度（mm）。</summary>
    public Line SetLineWidth(double width) { LineWidth = width; return this; }
    /// <summary>设置线条透明度 [0,1]。</summary>
    public Line SetLineOpacity(double opacity) { LineOpacity = opacity; return this; }

    /// <inheritdoc/>
    public void Draw(DrawContext ctx)
    {
        if (LineWidth <= 0.0d) return;
        if (_beginPoint.Length < 2 || _endPoint.Length < 2) return;
        double bx = _beginPoint[0], by = _beginPoint[1];
        double ex = _endPoint[0], ey = _endPoint[1];
        if (bx == ex && by == ey) return;

        // 绝对坐标 = 区域左上角 + 局部坐标（与 Java 元素定位语义一致）
        double ax = X + bx, ay = Y + by;
        double px = X + ex, py = Y + ey;

        ctx.Save();
        try
        {
            ctx.LineWidth = LineWidth;
            ctx.StrokeStyle = LineColor;
            if (LineOpacity < 1.0) ctx.GlobalAlpha = LineOpacity;
            ctx.BeginPath();
            ctx.MoveTo(ax, ay);
            ctx.LineTo(px, py);
            ctx.Stroke();
        }
        finally
        {
            ctx.Restore();
        }
    }
}
