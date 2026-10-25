namespace NetOfd.Archive;

/// <summary>
/// OFD-A 不合规项描述（对齐 Java ofdrw ArchiveViolation）。
/// <para>检查器对单条规则检查后产生的结果，描述文档中一处不符合 GB/T 42133-2022 的位置与原因。</para>
/// </summary>
public sealed class ArchiveViolation
{
    /// <summary>违规严重程度。</summary>
    public enum Severity
    {
        /// <summary>错误：不符合 OFD-A 标准的核心约束，必须修复。</summary>
        Error,
        /// <summary>警告：不符合标准建议，但可接受。</summary>
        Warn,
        /// <summary>信息：提示性信息。</summary>
        Info
    }

    /// <summary>规则标识，如 "DOC_TYPE"。</summary>
    public string RuleName { get; }

    /// <summary>严重程度。</summary>
    public Severity Level { get; }

    /// <summary>人类可读的问题描述。</summary>
    public string Description { get; }

    /// <summary>文档中的位置（OFD 容器内绝对路径），可为 null。</summary>
    public string? Location { get; }

    /// <summary>实际检测到的值，可为 null。</summary>
    public string? ActualValue { get; }

    /// <summary>标准期望的值，可为 null。</summary>
    public string? ExpectedValue { get; }

    public ArchiveViolation(string ruleName, Severity level, string description,
        string? location = null, string? actualValue = null, string? expectedValue = null)
    {
        RuleName = ruleName ?? throw new ArgumentNullException(nameof(ruleName));
        Level = level;
        Description = description ?? throw new ArgumentNullException(nameof(description));
        Location = location;
        ActualValue = actualValue;
        ExpectedValue = expectedValue;
    }

    public override string ToString()
    {
        var sb = new System.Text.StringBuilder();
        sb.Append('[').Append(Level).Append("] ");
        sb.Append(RuleName).Append(": ").Append(Description);
        if (Location != null) sb.Append(" (位置: ").Append(Location).Append(')');
        if (ActualValue != null) sb.Append(" 实际: ").Append(ActualValue);
        if (ExpectedValue != null) sb.Append(" 期望: ").Append(ExpectedValue);
        return sb.ToString();
    }
}
