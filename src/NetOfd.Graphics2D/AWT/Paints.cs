using System;

namespace NetOfd.Graphics2D.AWT;

/// <summary>
/// 画笔/填充接口（替代 Java AWT Paint）
/// </summary>
public interface IPaint
{
    // Marker interface - implementations define their own color sources
}

/// <summary>
/// 纯色填充（替代 Java AWT Color）
/// </summary>
public class OFDSolidPaint : IPaint
{
    public OFDColor Color { get; }

    public OFDSolidPaint(OFDColor color) => Color = color;

    public OFDSolidPaint(int r, int g, int b, int a = 255) => Color = new OFDColor(r, g, b, a);

    public static implicit operator OFDColor(OFDSolidPaint p) => p.Color;
}

/// <summary>
/// 线性渐变填充（替代 Java AWT LinearGradientPaint）。
/// <para>注意：当前版本写入 OFD 时以中点插值纯色近似。</para>
/// </summary>
public class OFDLinearGradientPaint : IPaint
{
    public Point2D StartPoint { get; }
    public Point2D EndPoint { get; }
    public OFDColor[] Colors { get; }
    public float[] Fractions { get; }
    public int CycleMethod { get; }

    public const int NO_CYCLE = 0;
    public const int REFLECT = 1;
    public const int REPEAT = 2;

    public OFDLinearGradientPaint(Point2D start, Point2D end, OFDColor[] colors, float[] fractions, int cycle)
    {
        StartPoint = start; EndPoint = end; Colors = colors; Fractions = fractions; CycleMethod = cycle;
    }

    public OFDColor GetColor(double t)
    {
        if (Colors.Length == 0) return OFDColor.Black;
        if (Colors.Length == 1) return Colors[0];

        t = CycleMethod switch
        {
            REPEAT => t % 1.0,
            REFLECT => Math.Abs(t % 2.0 - 1.0),
            _ => Math.Clamp(t, 0, 1)
        };

        for (int i = 0; i < Fractions.Length - 1; i++)
        {
            if (t >= Fractions[i] && t <= Fractions[i + 1])
            {
                float f = (float)((t - Fractions[i]) / (Fractions[i + 1] - Fractions[i]));
                return Interpolate(Colors[i], Colors[i + 1], f);
            }
        }
        return Colors[^1];
    }

    private static OFDColor Interpolate(OFDColor c1, OFDColor c2, float t)
    {
        int r = (int)(c1.R + (c2.R - c1.R) * t);
        int g = (int)(c1.G + (c2.G - c1.G) * t);
        int b = (int)(c1.B + (c2.B - c1.B) * t);
        int a = (int)(c1.A + (c2.A - c1.A) * t);
        return new OFDColor(r, g, b, a);
    }
}

/// <summary>
/// 径向渐变填充（替代 Java AWT RadialGradientPaint）。
/// <para>注意：当前版本写入 OFD 时以中点插值纯色近似。</para>
/// </summary>
public class OFDRadialGradientPaint : IPaint
{
    public Point2D CenterPoint { get; }
    public Point2D FocusPoint { get; }
    public double Radius { get; }
    public OFDColor[] Colors { get; }
    public float[] Fractions { get; }
    public int CycleMethod { get; }

    public OFDRadialGradientPaint(Point2D center, double radius, Point2D focus, OFDColor[] colors, float[] fractions, int cycle)
    {
        CenterPoint = center; Radius = radius; FocusPoint = focus;
        Colors = colors; Fractions = fractions; CycleMethod = cycle;
    }

    public OFDColor GetColor(double t)
    {
        t = Math.Clamp(t, 0, 1);
        if (Colors.Length == 0) return OFDColor.Black;
        if (Colors.Length == 1) return Colors[0];

        int i = (int)(t * (Colors.Length - 1));
        i = Math.Min(i, Colors.Length - 2);
        float f = (float)(t * (Colors.Length - 1) - i);
        return new OFDColor(
            (int)(Colors[i].R + (Colors[i + 1].R - Colors[i].R) * f),
            (int)(Colors[i].G + (Colors[i + 1].G - Colors[i].G) * f),
            (int)(Colors[i].B + (Colors[i + 1].B - Colors[i].B) * f),
            (int)(Colors[i].A + (Colors[i + 1].A - Colors[i].A) * f));
    }
}

/// <summary>
/// 渐变填充工具类（替代 Java AWT GradientPaint）。
/// <para>注意：当前版本写入 OFD 时以中点插值纯色近似（渐变 Shading 规范尚未支持）。</para>
/// </summary>
public class OFDGradientPaint : IPaint
{
    public Point2D Point1 { get; }
    public Point2D Point2 { get; }
    public OFDColor Color1 { get; }
    public OFDColor Color2 { get; }
    public bool Cyclic { get; }

    public OFDGradientPaint(Point2D p1, OFDColor c1, Point2D p2, OFDColor c2, bool cyclic)
    { Point1 = p1; Point2 = p2; Color1 = c1; Color2 = c2; Cyclic = cyclic; }

    public OFDGradientPaint(double x1, double y1, OFDColor c1, double x2, double y2, OFDColor c2, bool cyclic)
        : this(new Point2D(x1, y1), c1, new Point2D(x2, y2), c2, cyclic) { }

    /// <summary>按参数 t ∈ [0,1] 取插值色；Cyclic 时 t 以 2 为周期反射。</summary>
    public OFDColor GetColor(double t)
    {
        if (Cyclic)
        {
            double m = t % 2.0;
            if (m < 0) m += 2.0;
            t = m <= 1.0 ? m : 2.0 - m;
        }
        else
        {
            t = Math.Clamp(t, 0, 1);
        }
        return new OFDColor(
            (int)(Color1.R + (Color2.R - Color1.R) * t),
            (int)(Color1.G + (Color2.G - Color1.G) * t),
            (int)(Color1.B + (Color2.B - Color1.B) * t),
            (int)(Color1.A + (Color2.A - Color1.A) * t));
    }

    public OFDColor GetColor1() => Color1;
    public OFDColor GetColor2() => Color2;
    public bool IsCyclic() => Cyclic;
    public Point2D GetPoint1() => Point1;
    public Point2D GetPoint2() => Point2;
}