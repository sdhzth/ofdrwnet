using System;

namespace NetOfd.Graphics2D.AWT;

/// <summary>
/// OFD 描边属性（替代 Java AWT BasicStroke）
/// </summary>
public class OFDStroke : ICloneable
{
    public float LineWidth { get; set; } = 1.0f;
    public int EndCap { get; set; } = CAP_BUTT;
    public int LineJoin { get; set; } = JOIN_MITER;
    public float MiterLimit { get; set; } = 10.0f;
    public float[]? DashArray { get; set; }
    public float DashPhase { get; set; } = 0.0f;

    // 线端样式
    public const int CAP_BUTT = 0;
    public const int CAP_ROUND = 1;
    public const int CAP_SQUARE = 2;

    // 线连接样式
    public const int JOIN_MITER = 0;
    public const int JOIN_ROUND = 1;
    public const int JOIN_BEVEL = 2;

    public OFDStroke() { }

    public OFDStroke(float lineWidth)
    {
        LineWidth = lineWidth;
    }

    public OFDStroke(float lineWidth, int endCap, int lineJoin)
    {
        LineWidth = lineWidth;
        EndCap = endCap;
        LineJoin = lineJoin;
    }

    public OFDStroke(float lineWidth, int endCap, int lineJoin, float miterLimit)
    {
        LineWidth = lineWidth;
        EndCap = endCap;
        LineJoin = lineJoin;
        MiterLimit = miterLimit;
    }

    public OFDStroke(float lineWidth, int endCap, int lineJoin, float miterLimit, float[] dash, float dashPhase)
    {
        LineWidth = lineWidth;
        EndCap = endCap;
        LineJoin = lineJoin;
        MiterLimit = miterLimit;
        DashArray = dash;
        DashPhase = dashPhase;
    }

    public int GetEndCap() => EndCap;
    public int GetLineJoin() => LineJoin;
    public float GetLineWidth() => LineWidth;
    public float GetMiterLimit() => MiterLimit;
    public float[]? GetDashArray() => DashArray;
    public float GetDashPhase() => DashPhase;

    public object Clone() => new OFDStroke(LineWidth, EndCap, LineJoin, MiterLimit,
        DashArray != null ? (float[])DashArray.Clone() : Array.Empty<float>(), DashPhase);

    public override string ToString() =>
        $"OFDStroke[width={LineWidth:F3}, cap={EndCap}, join={LineJoin}, miter={MiterLimit:F1}]";
}

/// <summary>
/// 默认描边（OFD 标准线宽）
/// </summary>
public class BasicStroke : OFDStroke
{
    public const float DEFAULT = 0.353f;

    public BasicStroke() : base(DEFAULT) { }
    public BasicStroke(float lineWidth) : base(lineWidth) { }
    public BasicStroke(float lineWidth, int endCap, int lineJoin) : base(lineWidth, endCap, lineJoin) { }
    public BasicStroke(float lineWidth, int endCap, int lineJoin, float miterLimit) : base(lineWidth, endCap, lineJoin, miterLimit) { }
    public BasicStroke(float lineWidth, int endCap, int lineJoin, float miterLimit, float[] dash, float dashPhase)
        : base(lineWidth, endCap, lineJoin, miterLimit, dash, dashPhase) { }
}