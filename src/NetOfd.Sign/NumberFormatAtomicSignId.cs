namespace NetOfd.Sign;

/// <summary>
/// 数字格式原子签名 ID。
/// <para>无前缀格式为："N"，例如 "1"（默认）；开启 0 前缀后为 "001"。</para>
/// 对应 Java: org.ofdrw.sign.NumberFormatAtomicSignID
/// </summary>
public class NumberFormatAtomicSignId : ISignIdProvider
{
    private int _provider;
    private readonly object _lock = new();

    /// <summary>是否启用 0 前缀（默认关闭）</summary>
    public bool EnableZeroPrefix { get; set; } = false;

    public NumberFormatAtomicSignId() { }

    public NumberFormatAtomicSignId(bool enableZeroPrefix)
    {
        EnableZeroPrefix = enableZeroPrefix;
    }

    /// <summary>按指定最大签名 ID 初始化</summary>
    public NumberFormatAtomicSignId(string maxSignId)
    {
        _provider = Parse(maxSignId);
    }

    public void SetCurrentMaxSignId(string maxSignId)
    {
        lock (_lock) _provider = Parse(maxSignId);
    }

    public string IncrementAndGet()
    {
        lock (_lock)
        {
            _provider++;
            return Format(_provider);
        }
    }

    public string Get()
    {
        lock (_lock) return Format(_provider);
    }

    public int Parse(string id)
    {
        if (string.IsNullOrEmpty(id)) return 0;
        // 去除所有非数字字符
        string numStr = System.Text.RegularExpressions.Regex.Replace(id, @"\D", "");
        if (numStr.Length == 0) return 0;
        return int.Parse(numStr);
    }

    private string Format(int value)
        => EnableZeroPrefix ? value.ToString("D3") : value.ToString();
}