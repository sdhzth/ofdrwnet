namespace NetOfd.Core.Signatures;

/// <summary>
/// 摘要算法（GB/T 33190-2016 §18.2.2 表 68，<c>References</c> 的 <c>CheckMethod</c> 属性）。
/// 对应 Java: org.ofdrw.core.signatures.range.CheckMethod
/// </summary>
public enum CheckMethod
{
    /// <summary>默认值</summary>
    MD5,

    SHA1,

    SHA256,

    /// <summary>国密杂凑算法 SM3（国内电子签章的常用取值）</summary>
    SM3,
}

/// <summary><see cref="CheckMethod"/> 与 XML 字符串的互转。</summary>
public static class CheckMethodUtil
{
    /// <summary>
    /// 解析摘要算法，大小写不敏感；null / 空串返回 <see cref="CheckMethod.MD5"/>（Java 枚举首项即默认值）。
    /// </summary>
    public static CheckMethod Parse(string? value)
    {
        string v = (value ?? "").Trim();
        if (v.Length == 0) return CheckMethod.MD5;

        return v.ToUpperInvariant() switch
        {
            "MD5" => CheckMethod.MD5,
            "SHA1" => CheckMethod.SHA1,
            "SHA256" => CheckMethod.SHA256,
            "SM3" => CheckMethod.SM3,
            _ => throw new ArgumentException($"未知的摘要算法：{value}", nameof(value)),
        };
    }

    /// <summary>转为 XML 中的写法（与枚举名同名，故无需映射表）。</summary>
    public static string ToXml(CheckMethod method) => method.ToString();
}
