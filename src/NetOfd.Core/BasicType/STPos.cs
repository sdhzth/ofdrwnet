namespace NetOfd.Core.BasicType;

/// <summary>
/// OFD 坐标点，格式：  "x y"
/// 对应 Java: org.ofdrw.core.basicType.ST_Pos
/// </summary>
public sealed class STPos : STBase, IEquatable<STPos>
{
    public double X { get; set; }
    public double Y { get; set; }

    public STPos() { }
    public STPos(double x, double y) { X = x; Y = y; }

    public static STPos Parse(string value)
    {
        var parts = value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) throw new FormatException($"ST_Pos 需要 2 个值，实际: '{value}'");
        return new STPos(ToDouble(parts[0]), ToDouble(parts[1]));
    }

    public override string ToString() => $"{Fmt(X)} {Fmt(Y)}";

    public bool Equals(STPos? other) =>
        other is not null && Math.Abs(X - other.X) < 1e-9 && Math.Abs(Y - other.Y) < 1e-9;

    public override bool Equals(object? obj) => Equals(obj as STPos);
    public override int GetHashCode() => HashCode.Combine(X, Y);
}
