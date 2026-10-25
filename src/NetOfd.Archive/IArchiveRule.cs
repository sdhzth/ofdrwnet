using System.Collections.Generic;

namespace NetOfd.Archive;

/// <summary>
/// OFD-A 检查规则（无状态，只读）。
/// <para>每条规则验证 OFD 文档的某一个约束是否满足 GB/T 42133-2022。
/// 实现类命名格式 XxxRule，与对应的处理器（XxxHandler）对应。对齐 Java ArchiveRule。</para>
/// </summary>
public abstract class ArchiveRule
{
    /// <summary>规则标识，如 "DOC_TYPE"、"IMAGE_FORMAT"。</summary>
    public abstract string RuleName { get; }

    /// <summary>对打开的 OFD 文档执行检查，返回违规项列表（非 null）。</summary>
    public abstract IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx);
}
