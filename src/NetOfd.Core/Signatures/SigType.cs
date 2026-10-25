namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名节点的类型（GB/T 33190-2016 §18.1 签名列表 图 85 表 66）。
/// 对应 Java: org.ofdrw.core.signatures.SigType
/// </summary>
public enum SigType
{
    /// <summary>安全签章（缺省值）</summary>
    Seal,

    /// <summary>纯数字签名</summary>
    Sign,
}

/// <summary><see cref="SigType"/> 与 XML 字符串的互转。</summary>
public static class SigTypeUtil
{
    /// <summary>
    /// 解析签名节点类型。null / 空串按 Java <c>getInstance</c> 的约定视为 <see cref="SigType.Seal"/>；
    /// 大小写不敏感；未知取值抛 <see cref="ArgumentException"/>。
    /// </summary>
    public static SigType Parse(string? value)
    {
        string v = (value ?? "").Trim();
        if (v.Length == 0 || v.Equals("Seal", StringComparison.OrdinalIgnoreCase))
            return SigType.Seal;
        if (v.Equals("Sign", StringComparison.OrdinalIgnoreCase))
            return SigType.Sign;

        throw new ArgumentException($"未知的签名节点的类型：{value}", nameof(value));
    }

    /// <summary>转为 XML 中的写法（与枚举名同名）。</summary>
    public static string ToXml(SigType type) => type.ToString();
}
