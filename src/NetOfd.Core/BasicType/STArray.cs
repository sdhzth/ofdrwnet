namespace NetOfd.Core.BasicType;

/// <summary>
/// OFD 数组类型，空格分隔的一组数值（常用于变换矩阵 CTM，6个元素）。
/// 格式：  "a b c d e f"
/// 对应 Java: org.ofdrw.core.basicType.ST_Array
/// </summary>
public sealed class STArray : STBase
{
    private readonly List<double> _items;

    public STArray()
    {
        _items = new List<double>();
    }

    public STArray(IEnumerable<double> items)
    {
        _items = new List<double>(items);
    }

    public STArray(string value) : this()
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            foreach (var part in value.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
                _items.Add(ToDouble(part));
        }
    }

    public IReadOnlyList<double> Items => _items;

    public int Count => _items.Count;

    /// <summary>单位变换矩阵（恒等矩阵）: 1 0 0 1 0 0</summary>
    public static STArray UnitCTM() => new([1, 0, 0, 1, 0, 0]);

    /// <summary>转换为 double 数组</summary>
    public double[] ToDoubleArray() => _items.ToArray();

    /// <summary>
    /// 以 6 元素变换矩阵格式解释，返回 3x3 矩阵（行主序）
    /// [a c e]
    /// [b d f]
    /// [0 0 1]
    /// </summary>
    public double[,] ToMatrix3x3()
    {
        if (_items.Count < 6) throw new InvalidOperationException("CTM 需要 6 个元素");
        return new double[,]
        {
            { _items[0], _items[2], _items[4] },
            { _items[1], _items[3], _items[5] },
            { 0,         0,         1         }
        };
    }

    /// <summary>
    /// 矩阵乘法（用于变换矩阵合并）
    /// A * B，输入 a/b 都是 6 元素 CTM
    /// </summary>
    public static STArray MtxMul(STArray a, STArray b)
    {
        if (a.Count < 6 || b.Count < 6)
            throw new ArgumentException("矩阵乘法需要 6 元素 CTM");

        double a0 = a._items[0], a1 = a._items[1], a2 = a._items[2];
        double a3 = a._items[3], a4 = a._items[4], a5 = a._items[5];
        double b0 = b._items[0], b1 = b._items[1], b2 = b._items[2];
        double b3 = b._items[3], b4 = b._items[4], b5 = b._items[5];

        return new STArray([
            a0 * b0 + a2 * b1,
            a1 * b0 + a3 * b1,
            a0 * b2 + a2 * b3,
            a1 * b2 + a3 * b3,
            a0 * b4 + a2 * b5 + a4,
            a1 * b4 + a3 * b5 + a5,
        ]);
    }

    /// <summary>验证长度</summary>
    public void ExpectLength(int expectedLen)
    {
        if (_items.Count != expectedLen)
            throw new InvalidOperationException($"ST_Array 期望 {expectedLen} 个元素，实际: {_items.Count}");
    }

    public void Add(double value) => _items.Add(value);

    public double this[int index] => _items[index];

    public static STArray Parse(string value) => new(value);

    public override string ToString() =>
        string.Join(" ", _items.Select(Fmt));
}
