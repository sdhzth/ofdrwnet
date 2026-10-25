namespace NetOfd.Reader.Model;

/// <summary>
/// 签名摘要条目：签名列表文件（Signatures.xml）中一条 <c>&lt;Signature&gt;</c> 记录的
/// 标识/类型/位置信息，不含签名值内容本身。
/// <para>对应 Java: <c>org.ofdrw.core.signatures.Signature</c>（摘要条目，区别于
/// <c>...signatures.sig.Signature</c> 签名描述文件根元素）。</para>
/// </summary>
public class SignatureInfo
{
    /// <summary>签名或签章的标识（ID 属性，推荐 "sNNN"）</summary>
    public string Id { get; init; } = "";

    /// <summary>签名类型原始取值（"Seal" 安全签章 / "Sign" 纯数字签名；宽容保留未知值）</summary>
    public string Type { get; init; } = "Seal";

    /// <summary>签名描述文件（Signature.xml）位置（BaseLoc 属性）</summary>
    public string BaseLoc { get; init; } = "";

    /// <summary>【可选】相对位置（Relative 属性，签名保护范围），无则 null</summary>
    public string? Relative { get; init; }
}
