namespace NetOfd.Layout;

/// <summary>
/// 矩形区域（宽高 + 可选的左上角坐标）。
/// 对应 Java: org.ofdrw.layout.Rectangle
/// </summary>
public class Rectangle
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public Rectangle(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public Rectangle(double x, double y, double width, double height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public override string ToString() => $"Rectangle[{X},{Y} {Width}x{Height}]";
}