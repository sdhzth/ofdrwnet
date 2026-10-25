using System;

namespace NetOfd.Graphics2D.AWT;

/// <summary>
/// 图形接口（替代 Java AWT Shape）
/// </summary>
public interface IShape
{
    bool Contains(double x, double y);
    bool Contains(double x, double y, double w, double h);
    bool Intersects(double x, double y, double w, double h);
    Rectangle2D GetBounds();
    PathIterator GetPathIterator(AffineTransform? at);
}

public class Rectangle2D : IShape
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public Rectangle2D() { }
    public Rectangle2D(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; }

    public double MinX => X;
    public double MinY => Y;
    public double MaxX => X + Width;
    public double MaxY => Y + Height;
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public void SetRect(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; }
    Rectangle2D IShape.GetBounds() => new(X, Y, Width, Height);
    public Rectangle2D GetBounds() => new(X, Y, Width, Height);

    public bool Contains(double x, double y) => x >= MinX && x < MaxX && y >= MinY && y < MaxY;
    public bool Contains(double x, double y, double w, double h)
        => Contains(x, y) && Contains(x + w, y) && Contains(x, y + h) && Contains(x + w, y + h);

    public bool Intersects(double x, double y, double w, double h)
    {
        if (w <= 0 || h <= 0) return false;
        return x < MaxX && (x + w) > MinX && y < MaxY && (y + h) > MinY;
    }

    public PathIterator GetPathIterator(AffineTransform? at)
    {
        var p = new GeneralPath();
        p.AppendRect(X, Y, Width, Height, false);
        return at != null ? p.GetPathIterator(at) : p.GetPathIterator(null);
    }

    public override string ToString() => $"Rectangle2D[{X:F2}, {Y:F2}, {Width:F2}x{Height:F2}]";
}

public class Rectangle2D_Double : Rectangle2D
{
    public Rectangle2D_Double() { }
    public Rectangle2D_Double(double x, double y, double w, double h) : base(x, y, w, h) { }
}

public class Point2D
{
    public double X { get; set; }
    public double Y { get; set; }

    public Point2D() { }
    public Point2D(double x, double y) { X = x; Y = y; }

    public double Distance(Point2D p) => Math.Sqrt((X - p.X) * (X - p.X) + (Y - p.Y) * (Y - p.Y));
    public double Distance(double px, double py) => Math.Sqrt((X - px) * (X - px) + (Y - py) * (Y - py));

    public override string ToString() => $"Point2D[{X:F2}, {Y:F2}]";
}

public class Point2D_Double : Point2D
{
    public Point2D_Double() { }
    public Point2D_Double(double x, double y) : base(x, y) { }
}

public class Line2D
{
    public double X1 { get; set; }
    public double Y1 { get; set; }
    public double X2 { get; set; }
    public double Y2 { get; set; }

    public Line2D() { }
    public Line2D(double x1, double y1, double x2, double y2) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }

    public void SetLine(double x1, double y1, double x2, double y2) { X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }

    public Point2D GetP1() => new(X1, Y1);
    public Point2D GetP2() => new(X2, Y2);

    public Rectangle2D GetBounds()
    {
        double minx = Math.Min(X1, X2), miny = Math.Min(Y1, Y2);
        double maxx = Math.Max(X1, X2), maxy = Math.Max(Y1, Y2);
        return new Rectangle2D(minx, miny, maxx - minx, maxy - miny);
    }

    public bool Contains(double x, double y)
    {
        double a = X2 - X1, b = Y2 - Y1;
        double t = ((x - X1) * a + (y - Y1) * b) / (a * a + b * b);
        if (t < 0 || t > 1) return false;
        double projX = X1 + t * a, projY = Y1 + t * b;
        double dist = Math.Sqrt((x - projX) * (x - projX) + (y - projY) * (y - projY));
        return dist < 0.01;
    }

    public PathIterator GetPathIterator(AffineTransform? at)
    {
        var p = new GeneralPath();
        p.MoveTo(X1, Y1);
        p.LineTo(X2, Y2);
        return at != null ? p.GetPathIterator(at) : p.GetPathIterator(null);
    }
}

public class Line2D_Double : Line2D
{
    public Line2D_Double() { }
    public Line2D_Double(double x1, double y1, double x2, double y2) : base(x1, y1, x2, y2) { }
}

public class RoundRectangle2D : IShape
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double ArcWidth { get; set; }
    public double ArcHeight { get; set; }

    public RoundRectangle2D() { }
    public RoundRectangle2D(double x, double y, double w, double h, double aw, double ah)
    { X = x; Y = y; Width = w; Height = h; ArcWidth = aw; ArcHeight = ah; }

    public void SetRoundRect(double x, double y, double w, double h, double aw, double ah)
    { X = x; Y = y; Width = w; Height = h; ArcWidth = aw; ArcHeight = ah; }

    public Rectangle2D GetBounds() => new(X, Y, Width, Height);

    public bool Contains(double x, double y) => GetBounds().Contains(x, y);
    public bool Contains(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);
    public bool Intersects(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);

    public PathIterator GetPathIterator(AffineTransform? at)
    {
        var p = new GeneralPath();
        p.AppendRoundRect(X, Y, Width, Height, ArcWidth, ArcHeight, false);
        return at != null ? p.GetPathIterator(at) : p.GetPathIterator(null);
    }
}

public class RoundRectangle2D_Double : RoundRectangle2D
{
    public RoundRectangle2D_Double() { }
    public RoundRectangle2D_Double(double x, double y, double w, double h, double aw, double ah)
        : base(x, y, w, h, aw, ah) { }
}

public class Ellipse2D : IShape
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }

    public Ellipse2D() { }
    public Ellipse2D(double x, double y, double w, double h) { X = x; Y = y; Width = w; Height = h; }

    public Rectangle2D GetBounds() => new(X, Y, Width, Height);

    public bool Contains(double x, double y) => GetBounds().Contains(x, y);
    public bool Contains(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);
    public bool Intersects(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);

    public PathIterator GetPathIterator(AffineTransform? at)
    {
        var p = new GeneralPath();
        p.AppendEllipse(X, Y, Width, Height, false);
        return at != null ? p.GetPathIterator(at) : p.GetPathIterator(null);
    }
}

public class Ellipse2D_Double : Ellipse2D
{
    public Ellipse2D_Double() { }
    public Ellipse2D_Double(double x, double y, double w, double h) : base(x, y, w, h) { }
}

public class Arc2D : IShape
{
    public double X { get; set; }
    public double Y { get; set; }
    public double Width { get; set; }
    public double Height { get; set; }
    public double StartAngle { get; set; }
    public double Extent { get; set; }
    public int Type { get; set; }

    public const int OPEN = 0;
    public const int CHORD = 1;
    public const int PIE = 2;

    public Arc2D() { Type = OPEN; }
    public Arc2D(double x, double y, double w, double h, double sa, double ext, int type)
    { X = x; Y = y; Width = w; Height = h; StartAngle = sa; Extent = ext; Type = type; }

    public void SetArc(double x, double y, double w, double h, double sa, double ext, int type)
    { X = x; Y = y; Width = w; Height = h; StartAngle = sa; Extent = ext; Type = type; }

    public Rectangle2D GetBounds() => new(X, Y, Width, Height);

    public bool Contains(double x, double y) => GetBounds().Contains(x, y);
    public bool Contains(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);
    public bool Intersects(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);

    public PathIterator GetPathIterator(AffineTransform? at)
    {
        var p = new GeneralPath();
        p.AppendArc(X, Y, Width, Height, StartAngle, Extent, Type, false);
        return at != null ? p.GetPathIterator(at) : p.GetPathIterator(null);
    }
}

public class Arc2D_Double : Arc2D
{
    public Arc2D_Double() { }
    public Arc2D_Double(double x, double y, double w, double h, double sa, double ext, int type)
        : base(x, y, w, h, sa, ext, type) { }
}

/// <summary>
/// 通用路径（替代 Java AWT GeneralPath）
/// </summary>
public class GeneralPath : IShape
{
    private readonly List<PathOp> _ops = new();

    public List<PathOp> Operations => _ops;

    public void MoveTo(double x, double y) => _ops.Add(new PathOp(PathOpType.M, x, y));
    public void LineTo(double x, double y) => _ops.Add(new PathOp(PathOpType.L, x, y));
    public void QuadTo(double x1, double y1, double x2, double y2) => _ops.Add(new PathOp(PathOpType.Q, x1, y1, x2, y2));
    public void CurveTo(double x1, double y1, double x2, double y2, double x3, double y3)
        => _ops.Add(new PathOp(PathOpType.C, x1, y1, x2, y2, x3, y3));
    public void Close() => _ops.Add(new PathOp(PathOpType.Z));

    public void AppendRect(double x, double y, double w, double h, bool close)
    {
        MoveTo(x, y); LineTo(x + w, y); LineTo(x + w, y + h); LineTo(x, y + h);
        if (close) Close();
    }

    public void AppendRoundRect(double x, double y, double w, double h, double aw, double ah, bool close)
    {
        // 简化为普通矩形（完整实现需要更复杂的圆弧逻辑）
        AppendRect(x, y, w, h, close);
    }

    public void AppendEllipse(double x, double y, double w, double h, bool close)
    {
        // 四段三次贝塞尔逼近椭圆（标准 kappa 常量）
        double cx = x + w / 2, cy = y + h / 2;
        double rx = w / 2, ry = h / 2;
        const double k = 0.5522847498307936;
        double kx = rx * k, ky = ry * k;
        MoveTo(cx + rx, cy);
        CurveTo(cx + rx, cy - ky, cx + kx, cy - ry, cx, cy - ry);
        CurveTo(cx - kx, cy - ry, cx - rx, cy - ky, cx - rx, cy);
        CurveTo(cx - rx, cy + ky, cx - kx, cy + ry, cx, cy + ry);
        CurveTo(cx + kx, cy + ry, cx + rx, cy + ky, cx + rx, cy);
        if (close) Close();
    }

    public void AppendArc(double x, double y, double w, double h, double sa, double ext, int type, bool close)
    {
        // 椭圆弧的贝塞尔逼近：按 ≤90° 分段，控制点沿切线方向偏移 4/3·tan(Δ/4)
        double cx = x + w / 2, cy = y + h / 2;
        double rx = w / 2, ry = h / 2;
        double start = sa * Math.PI / 180.0;
        double extent = ext * Math.PI / 180.0;

        double p0x = cx + rx * Math.Cos(start), p0y = cy + ry * Math.Sin(start);
        MoveTo(p0x, p0y);

        if (Math.Abs(extent) > 1e-9)
        {
            int segs = Math.Max(1, (int)Math.Ceiling(Math.Abs(extent) / (Math.PI / 4)));
            double step = extent / segs;
            for (int i = 0; i < segs; i++)
            {
                double a0 = start + step * i, a1 = start + step * (i + 1);
                double kk = 4.0 / 3.0 * Math.Tan((a1 - a0) / 4);
                double ax0 = cx + rx * Math.Cos(a0), ay0 = cy + ry * Math.Sin(a0);
                double ax1 = cx + rx * Math.Cos(a1), ay1 = cy + ry * Math.Sin(a1);
                double d0x = -rx * Math.Sin(a0), d0y = ry * Math.Cos(a0);
                double d1x = -rx * Math.Sin(a1), d1y = ry * Math.Cos(a1);
                CurveTo(ax0 + kk * d0x, ay0 + kk * d0y,
                        ax1 - kk * d1x, ay1 - kk * d1y,
                        ax1, ay1);
            }
        }

        if (type == Arc2D.CHORD) Close();
        else if (type == Arc2D.PIE) { LineTo(cx, cy); Close(); }
    }

    public Rectangle2D GetBounds()
    {
        if (_ops.Count == 0) return new Rectangle2D();
        double minx = double.MaxValue, miny = double.MaxValue, maxx = double.MinValue, maxy = double.MinValue;
        foreach (var op in _ops)
        {
            if (op.Type == PathOpType.Z) continue;
            minx = Math.Min(minx, op.X ?? 0); maxx = Math.Max(maxx, op.X ?? 0);
            miny = Math.Min(miny, op.Y ?? 0); maxy = Math.Max(maxy, op.Y ?? 0);
        }
        return new Rectangle2D(minx, miny, maxx - minx, maxy - miny);
    }

    public bool Contains(double x, double y) => GetBounds().Contains(x, y);
    public bool Contains(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);
    public bool Intersects(double x, double y, double w, double h) => GetBounds().Intersects(x, y, w, h);

    public PathIterator GetPathIterator(AffineTransform? at)
    {
        var ops = _ops.ToArray();
        return new PathIterator(ops, at);
    }

    public GeneralPath Clone() { var g = new GeneralPath(); g._ops.AddRange(_ops); return g; }
}

public enum PathOpType { M, L, Q, C, Z }

public class PathOp
{
    public PathOpType Type { get; }
    public double? X { get; }
    public double? Y { get; }
    public double? X1 { get; }
    public double? Y1 { get; }
    public double? X2 { get; }
    public double? Y2 { get; }
    public double? X3 { get; }
    public double? Y3 { get; }

    public PathOp(PathOpType type, double? x = null, double? y = null)
    { Type = type; X = x; Y = y; }
    public PathOp(PathOpType type, double? x1, double? y1, double? x2, double? y2)
    { Type = type; X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; }
    public PathOp(PathOpType type, double? x1, double? y1, double? x2, double? y2, double? x3, double? y3)
    { Type = type; X1 = x1; Y1 = y1; X2 = x2; Y2 = y2; X3 = x3; Y3 = y3; }

    public string ToAbbreviatedData() => Type switch
    {
        PathOpType.M => $"M {X:F2} {Y:F2}",
        PathOpType.L => $"L {X:F2} {Y:F2}",
        PathOpType.Q => $"Q {X1:F2} {Y1:F2} {X2:F2} {Y2:F2}",
        // GB/T 33190：三次贝塞尔 = B（6 参数）；闭合 = C（0 参数）
        PathOpType.C => $"B {X1:F2} {Y1:F2} {X2:F2} {Y2:F2} {X3:F2} {Y3:F2}",
        PathOpType.Z => "C",
        _ => ""
    };
}

public class PathIterator
{
    private readonly PathOp[] _ops;
    private int _index;
    private readonly AffineTransform? _transform;

    public PathIterator(PathOp[] ops, AffineTransform? transform)
    { _ops = ops; _transform = transform; _index = 0; }

    public bool IsDone => _index >= _ops.Length;
    public void Next() => _index++;

    public int CurrentSegment(double[] coords)
    {
        if (IsDone) return 0;
        var op = _ops[_index];
        var xf = _transform;

        int segType = op.Type switch
        {
            PathOpType.M => 0,
            PathOpType.L => 1,
            PathOpType.Q => 2,
            PathOpType.C => 3,
            PathOpType.Z => 4,
            _ => 0
        };

        if (xf != null) xf = xf.Clone();

        switch (segType)
        {
            case 0: // MOVETO
            case 1: // LINETO
                coords[0] = op.X ?? 0;
                coords[1] = op.Y ?? 0;
                if (xf != null) { var pt = xf.Transform(coords[0], coords[1]); coords[0] = pt.X; coords[1] = pt.Y; }
                break;
            case 2: // QUADTO: 控制点 + 终点
                coords[0] = op.X1 ?? 0; coords[1] = op.Y1 ?? 0;
                coords[2] = op.X2 ?? 0; coords[3] = op.Y2 ?? 0;
                if (xf != null)
                {
                    var p0 = xf.Transform(coords[0], coords[1]); coords[0] = p0.X; coords[1] = p0.Y;
                    var p1 = xf.Transform(coords[2], coords[3]); coords[2] = p1.X; coords[3] = p1.Y;
                }
                break;
            case 3: // CUBICTO: 两控制点 + 终点
                coords[0] = op.X1 ?? 0; coords[1] = op.Y1 ?? 0;
                coords[2] = op.X2 ?? 0; coords[3] = op.Y2 ?? 0;
                coords[4] = op.X3 ?? 0; coords[5] = op.Y3 ?? 0;
                if (xf != null)
                {
                    var p0 = xf.Transform(coords[0], coords[1]); coords[0] = p0.X; coords[1] = p0.Y;
                    var p1 = xf.Transform(coords[2], coords[3]); coords[2] = p1.X; coords[3] = p1.Y;
                    var p2 = xf.Transform(coords[4], coords[5]); coords[4] = p2.X; coords[5] = p2.Y;
                }
                break;
        }

        return segType;
    }
}

public class Area : IShape
{
    private readonly IShape _shape;

    public Area() { _shape = new Rectangle2D(); }
    public Area(IShape shape) { _shape = shape; }

    public bool Contains(double x, double y) => _shape.Contains(x, y);
    public bool Contains(double x, double y, double w, double h)
    {
        return _shape.Contains(x, y) && _shape.Contains(x + w, y + h);
    }
    public void Intersect(Area rhs) { /* 简化实现 */ }
    public void Add(Area rhs) { /* 简化实现 */ }
    public void Subtract(Area rhs) { /* 简化实现 */ }

    public Rectangle2D GetBounds() => _shape.GetBounds();
    public bool Intersects(double x, double y, double w, double h) => _shape.Intersects(x, y, w, h);

    public PathIterator GetPathIterator(AffineTransform? at) => _shape.GetPathIterator(at);
}