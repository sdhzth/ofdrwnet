namespace NetOfd.Archive.Convert;

/// <summary>
/// OFD-A 去技术化处理器。
/// <para>每个处理器完成一项具体的去技术化操作，<b>就地修改</b> <see cref="ArchiveWorkspace"/> 中的内容。
/// 处理器按编排顺序依次执行（如必须先解密再改 DocType），实现应保持无状态、可重复调用。</para>
/// <para>命名与检查规则一一对应：XxxHandler ↔ XxxRule。</para>
/// 对应 Java: org.ofdrw.archive.convert.ArchiveHandler
/// </summary>
public abstract class ArchiveHandler
{
    /// <summary>处理器名称，与对应检查规则的 RuleName 保持一致。</summary>
    public abstract string HandlerName { get; }

    /// <summary>执行去技术化转换，结果直接写入工作区。</summary>
    public abstract void Handle(ArchiveWorkspace ws);
}
