using NetOfd.Core.BasicType;

namespace NetOfd.Reader.Model;

/// <summary>
/// 签名描述文件结构化实体——单条 <c>&lt;Signature&gt;</c> 描述文件的解析结果。
/// <para>
/// 对应 Java ofdrw-core <c>org.ofdrw.core.signatures.sig.Signature</c>（签名描述文件根），
/// 区别于 <see cref="SignatureInfo"/>（签名列表文件 Signatures.xml 摘要条目）。
/// </para>
/// <para>
/// NetOfd 的 <see cref="SignatureInfo"/> 仅承载 ID/Type/BaseLoc 等列表层信息；
/// 本实体补齐 SignedInfo（Provider/SignatureMethod/SignatureDateTime/Seal/StampAnnot/References）等
/// 签名描述文件层信息，供 OFD 校验、签章可视化、签名审计等下游消费。
/// </para>
/// </summary>
public sealed class SignatureDescEntity
{
    /// <summary>签名描述文件根元素 ID 属性（可选；很多文档根元素无 ID）。</summary>
    public string? Id { get; init; }

    /// <summary>签名值文件相对/绝对路径（SignedValue 元素文本）。</summary>
    public string? SignedValueLoc { get; init; }

    /// <summary>签名要保护的原文及本次签名相关信息。</summary>
    public SignedInfoSummary? SignedInfo { get; init; }
}

/// <summary>签名描述文件中的 SignedInfo 子元素摘要。</summary>
public sealed class SignedInfoSummary
{
    /// <summary>签章组件提供者信息（&lt;Provider ProviderName/Company/Version&gt;）。</summary>
    public ProviderSummary? Provider { get; init; }

    /// <summary>签名算法（&lt;SignatureMethod&gt; 文本，通常为 ASN.1 OID）。</summary>
    public string? SignatureMethod { get; init; }

    /// <summary>签名时间（&lt;SignatureDateTime&gt; 文本）。</summary>
    public string? SignatureDateTime { get; init; }

    /// <summary>电子印章文件路径（&lt;Seal&gt;/&lt;BaseLoc&gt; 文本）。</summary>
    public string? SealLoc { get; init; }

    /// <summary>签名外观列表（&lt;StampAnnot&gt;，可能多个）。</summary>
    public IReadOnlyList<StampAnnotSummary> StampAnnots { get; init; } = Array.Empty<StampAnnotSummary>();

    /// <summary>签名覆盖范围（&lt;References&gt; 内 Reference 列表）。</summary>
    public IReadOnlyList<ReferenceSummary> References { get; init; } = Array.Empty<ReferenceSummary>();

    /// <summary>杂凑方法（&lt;References CheckMethod&gt; 属性，如 SM3 / MD5）。</summary>
    public string? ReferencesCheckMethod { get; init; }
}

/// <summary>签章组件提供者摘要。</summary>
public sealed class ProviderSummary
{
    /// <summary>提供者名称（ProviderName 属性）。</summary>
    public string? ProviderName { get; init; }

    /// <summary>提供者公司（Company 属性）。</summary>
    public string? Company { get; init; }

    /// <summary>提供者版本（Version 属性）。</summary>
    public string? Version { get; init; }
}

/// <summary>签名的外观注释摘要（&lt;StampAnnot ID="..." Boundary="..." PageRef="..."/&gt;）。</summary>
public sealed class StampAnnotSummary
{
    /// <summary>外观注释标识（ID 属性，推荐 "sNNN"）。</summary>
    public string? Id { get; init; }

    /// <summary>外观注释所在页面的标识符（PageRef 属性）。</summary>
    public string? PageRef { get; init; }

    /// <summary>外观边框位置（Boundary 属性 "x y w h"），未解析为 STBox 时为 null。</summary>
    public STBox? Boundary { get; init; }

    /// <summary>Boundary 原始文本（无法解析为 STBox 时仅保留原文）。</summary>
    public string? BoundaryText { get; init; }

    /// <summary>裁剪区域 Clip 属性（可选）。</summary>
    public string? Clip { get; init; }
}

/// <summary>签名覆盖范围条目（&lt;Reference FileRef="..."&gt;&lt;CheckValue&gt;...&lt;/CheckValue&gt;&lt;/Reference&gt;）。</summary>
public sealed class ReferenceSummary
{
    /// <summary>指向包内的文件路径（FileRef 属性，绝对路径）。</summary>
    public string FileRef { get; init; } = string.Empty;

    /// <summary>杂凑值原始 Base64 字符串（&lt;CheckValue&gt; 文本）。</summary>
    public string CheckValueBase64 { get; init; } = string.Empty;

    /// <summary>杂凑值解码后的字节（按 CheckMethod 计算得到的实际摘要）。</summary>
    public byte[] CheckValue { get; init; } = Array.Empty<byte>();
}