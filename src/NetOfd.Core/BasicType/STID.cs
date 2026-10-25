namespace NetOfd.Core.BasicType;

/// <summary>
/// OFD 对象标识符，必须大于 0（0 为无效值）。
/// 对应 Java: org.ofdrw.core.basicType.ST_ID
/// </summary>
public sealed class STID : STBase, IEquatable<STID>, IComparable<STID>
{
    private long _value;

    /// <summary>无效 ID（值为 0）</summary>
    public static readonly STID Invalid = new(0);

    /// <summary>ID 值</summary>
    public long Value
    {
        get => _value;
        set
        {
            if (value < 0)
                throw new ArgumentException($"ST_ID 值必须 >= 0，实际: {value}");
            _value = value;
        }
    }

    /// <summary>是否有效（> 0）</summary>
    public bool IsValid => _value > 0;

    public STID(long value) { Value = value; }

    /// <summary>从字符串解析</summary>
    public static STID Parse(string value) => new(ToLong(value));

    /// <summary>获取对应的引用 ID</summary>
    public STRefID Ref() => new(_value);

    public override string ToString() => _value.ToString();

    public bool Equals(STID? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => Equals(obj as STID);
    public override int GetHashCode() => _value.GetHashCode();
    public int CompareTo(STID? other) => other is null ? 1 : _value.CompareTo(other._value);

    public static implicit operator long(STID id) => id._value;
    public static implicit operator STID(long value) => new(value);
    public static bool operator ==(STID? a, STID? b) => a?.Equals(b) ?? b is null;
    public static bool operator !=(STID? a, STID? b) => !(a == b);
    public static bool operator <(STID a, STID b) => a._value < b._value;
    public static bool operator >(STID a, STID b) => a._value > b._value;
}
