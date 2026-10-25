namespace NetOfd.Core.BasicStructure.OFD;

/// <summary>
/// 文档分类（表 4 文档元数据属性）。
/// 对应 Java: org.ofdrw.core.basicStructure.ofd.docInfo.DocUsage
/// </summary>
public enum DocUsage
{
    /// <summary>普通文档（默认值）</summary>
    Normal,
    /// <summary>电子书</summary>
    EBook,
    /// <summary>电子报纸</summary>
    ENewsPaper,
    /// <summary>电子期刊</summary>
    EMagzine
}

/// <summary>DocUsage 解析扩展（对齐 Java DocUsage.getInstance）。</summary>
public static class DocUsageExtensions
{
    /// <summary>
    /// 根据名称获取文档分类；未知或空值一律返回 <see cref="DocUsage.Normal"/>（对齐 Java 的 default 分支）。
    /// </summary>
    public static DocUsage ToDocUsage(this string? usage)
    {
        var s = usage?.Trim();
        if (!string.IsNullOrEmpty(s) &&
            Enum.TryParse<DocUsage>(s, ignoreCase: true, out var du))
            return du;

        return DocUsage.Normal;
    }
}
