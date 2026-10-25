namespace NetOfd.Emr;

/// <summary>
/// OFD-H（GB/T 48666—2026《电子病历版式文档技术要求》）电子病历版式文档生成参数。
/// <para>
/// 典型用法：
/// <code>
/// var result = OfdHDocumentGenerator.Generate(new OfdHOptions
/// {
///     OutputPath = "病历.ofd",
///     CaseData   = myCaseData,
/// });
/// if (!result.Success) Console.Error.WriteLine(result.Error);
/// </code>
/// </para>
/// </summary>
public sealed class OfdHOptions
{
    /// <summary>输出 .ofd 文件路径（必填）。相对路径按当前工作目录解析。</summary>
    public required string OutputPath { get; init; }

    /// <summary>中间产物工作目录（心电图 PNG、阶段文件等）；为 null 时取「输出目录/_emr_work」。</summary>
    public string? WorkDirectory { get; init; }

    /// <summary>
    /// 跳过国密签名与防夹带完整性保护。
    /// <para>仅用于调试文档结构；正式文档必须签名（否则不满足 GB/T 48666—2026 第 6.3 / 8 条要求）。</para>
    /// </summary>
    public bool SkipSignature { get; init; }

    /// <summary>病历数据；为 null 时使用内置演示数据 <see cref="EmrCaseData"/>（虚构患者）。</summary>
    public EmrCaseData? CaseData { get; init; }

    /// <summary>
    /// 文档标识（写入 OFD.xml 的 DocInfo/DocID）；为 null 时自动生成一个 GUID（32 位十六进制）。
    /// <para>演示场景可传入固定值以获得可复现输出。</para>
    /// </summary>
    public string? DocumentId { get; init; }

    /// <summary>
    /// 需嵌入文档的字型文件路径（.ttf/.ttc）；为 null 时按运行平台自动探测仿宋/中文字型。
    /// <para>GB/T 48666—2026 第 7.2 b 条要求「显示和打印所需的全部字型数据包含在文件中」。</para>
    /// </summary>
    public string? FontPath { get; init; }

    /// <summary>字型族名（写入 PublicRes.xml 的 Font/@FontName），默认「仿宋」。</summary>
    public string FontFamily { get; init; } = "仿宋";

    /// <summary>
    /// 找不到可嵌入字型时的行为：
    /// <para>true（默认）= 记录警告并跳过字型嵌入，文档仍可生成；false = 抛出异常。</para>
    /// </summary>
    public bool SkipFontEmbeddingWhenMissing { get; init; } = true;

    /// <summary>签名时间；为 null 时取当前时间。</summary>
    public DateTime? SigningTime { get; init; }

    /// <summary>签章主体（自签 SM2 证书的主体名称）。</summary>
    public string SignerName { get; init; } = "NetOfd OFD-H 生成器（演示签章者）";

    /// <summary>日志输出目标；为 null 时写入 <see cref="Console.Out"/>。</summary>
    public TextWriter? Log { get; init; }
}
