namespace NetOfd.Core.BasicType;

/// <summary>
/// OFD 对象引用 ID，指向另一个对象的 ST_ID。
/// 对应 Java: org.ofdrw.core.basicType.ST_RefID
/// </summary>
public sealed class STRefID : STBase, IEquatable<STRefID>
{
    private long _value;

    public long Value
    {
        get => _value;
        set
        {
            if (value < 0) throw new ArgumentException($"ST_RefID 值必须 >= 0，实际: {value}");
            _value = value;
        }
    }

    public STRefID(long value) { Value = value; }

    public static STRefID Parse(string value) => new(ToLong(value));

    public override string ToString() => _value.ToString();

    public bool Equals(STRefID? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => Equals(obj as STRefID);
    public override int GetHashCode() => _value.GetHashCode();

    public static implicit operator long(STRefID id) => id._value;
    public static implicit operator STRefID(long value) => new(value);
}
