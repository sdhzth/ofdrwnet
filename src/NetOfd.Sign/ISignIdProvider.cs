namespace NetOfd.Sign;

/// <summary>
/// 签名 ID 提供器。
/// <para>调用方可根据实际需要自定义签名 ID 的格式。</para>
/// 对应 Java: org.ofdrw.sign.SignIDProvider
/// </summary>
public interface ISignIdProvider
{
    /// <summary>
    /// 设置当前最大签名 ID 值（调用方需自行解析并设置计数器）。
    /// </summary>
    void SetCurrentMaxSignId(string maxSignId);

    /// <summary>
    /// 递增并获取新的签名 ID。
    /// </summary>
    /// <returns>签名 ID，例如 "s001"</returns>
    string IncrementAndGet();

    /// <summary>
    /// 获取当前签名 ID（不递增）。
    /// </summary>
    string Get();

    /// <summary>
    /// 解析签章 ID 中的数字序号。
    /// </summary>
    int Parse(string id);
}