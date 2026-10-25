namespace NetOfd.Core.BasicType;

/// <summary>
/// OFD 矩形区域类型：左上角坐标 + 宽高（单位：mm）。
/// 序列化格式：  "topLeftX topLeftY width height"
/// 对应 Java: org.ofdrw.core.basicType.ST_Box
/// </summary>
public sealed class STBox : STBase, IEquatable<STBox>
{
    /// <summary>左上角 X 坐标（mm）</summary>
    public double TopLeftX { get; set; }
    /// <summary>左上角 Y 坐标（mm）</summary>
    public double TopLeftY { get; set; }
    /// <summary>宽度（mm）</summary>
    public double Width { get; set; }
    /// <summary>高度（mm）</summary>
    public double Height { get; set; }

    public STBox() { }

    public STBox(double topLeftX, double topLeftY, double width, double height)
    {
        TopLeftX = topLeftX;
        TopLeftY = topLeftY;
        Width = width;
        Height = height;
    }

    /// <summary>深拷贝当前矩形</summary>
    public STBox Clone() => new(TopLeftX, TopLeftY, Width, Height);

    /// <summary>
    /// 从空格分隔的字符串解析 STBox，例如 "0 0 210 297"
    /// </summary>
    public static STBox Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("STBox 字符串不能为空", nameof(value));

        string[] parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4)
            throw new FormatException($"STBox 需要 4 个数值，实际: '{value}'");

        return new STBox(
            ToDouble(parts[0]),
            ToDouble(parts[1]),
            ToDouble(parts[2]),
            ToDouble(parts[3])
        );
    }

    /// <summary>尝试解析，失败返回 null</summary>
    public static STBox? TryParse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        try { return Parse(value); } catch { return null; }
    }

    /// <summary>右边界 X</summary>
    public double RightX => TopLeftX + Width;
    /// <summary>下边界 Y</summary>
    public double BottomY => TopLeftY + Height;

    public override string ToString() =>
        $"{Fmt(TopLeftX)} {Fmt(TopLeftY)} {Fmt(Width)} {Fmt(Height)}";

    public bool Equals(STBox? other) =>
        other is not null &&
        Math.Abs(TopLeftX - other.TopLeftX) < 1e-9 &&
        Math.Abs(TopLeftY - other.TopLeftY) < 1e-9 &&
        Math.Abs(Width - other.Width) < 1e-9 &&
        Math.Abs(Height - other.Height) < 1e-9;

    public override bool Equals(object? obj) => Equals(obj as STBox);
    public override int GetHashCode() => HashCode.Combine(TopLeftX, TopLeftY, Width, Height);
}
