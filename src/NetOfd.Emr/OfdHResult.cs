namespace NetOfd.Emr;

/// <summary>OFD-H 电子病历版式文档生成结果。</summary>
public sealed class OfdHResult
{
    /// <summary>是否生成成功（签名自检通过或显式跳过签名）。</summary>
    public bool Success { get; init; }

    /// <summary>输出的 .ofd 文件绝对路径。</summary>
    public string OutputPath { get; init; } = "";

    /// <summary>生成的 OFD 字节内容（与落盘文件一致）。</summary>
    public byte[]? OfdBytes { get; init; }

    /// <summary>文档页数。</summary>
    public int PageCount { get; init; }

    /// <summary>文件字节数。</summary>
    public long SizeBytes { get; init; }

    /// <summary>文档唯一识别码（EMR- 前缀 + DocID 前 16 位）。</summary>
    public string UniqueId { get; init; } = "";

    /// <summary>文档标识 DocID。</summary>
    public string DocumentId { get; init; } = "";

    /// <summary>数字签名数量。</summary>
    public int SignatureCount { get; init; }

    /// <summary>全部签名是否验签通过。</summary>
    public bool AllSignaturesValid { get; init; }

    /// <summary>防夹带 / 完整性校验是否通过（跳过签名时为 false）。</summary>
    public bool IntegrityVerified { get; init; }

    /// <summary>版本快照索引（对应 <c>Doc_0/V_N/</c>）。</summary>
    public int VersionIndex { get; init; }

    /// <summary>字型是否已嵌入文档。</summary>
    public bool FontEmbedded { get; init; }

    /// <summary>字型相关警告（如未找到可嵌入字型）。</summary>
    public string? FontWarning { get; init; }

    /// <summary>包内条目清单（对照 GB/T 48666—2026 表 1）。</summary>
    public IReadOnlyList<(string Name, long Length)> PackageEntries { get; init; }
        = Array.Empty<(string Name, long Length)>();

    /// <summary>失败原因；<see cref="Success"/> 为 true 时为 null。</summary>
    public string? Error { get; init; }
}
