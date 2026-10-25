namespace NetOfd.Sign;

/// <summary>
/// 标准格式原子签名 ID，格式为 "sNNN"（NNN 为三位序号，从 001 开始）。
/// 对应 Java: org.ofdrw.sign.StandFormatAtomicSignID
/// </summary>
public class StandFormatAtomicSignId : ISignIdProvider
{
    private int _provider;
    private readonly object _lock = new();

    public StandFormatAtomicSignId() { }

    /// <summary>按指定最大签名 ID 初始化</summary>
    public StandFormatAtomicSignId(string maxSignId)
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
            return $"s{_provider:D3}";
        }
    }

    public string Get()
    {
        lock (_lock) return $"s{_provider:D3}";
    }

    public int Parse(string id) => SignIdParser.ParseIndex(id);
}