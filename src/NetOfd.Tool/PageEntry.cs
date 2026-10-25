using System.Collections.Generic;

namespace NetOfd.Tool;

/// <summary>
/// 待迁移的页面对象。
/// 对应 Java: org.ofdrw.tool.merge.PageEntry
/// </summary>
public sealed class PageEntry
{
    /// <summary>关联文档上下文</summary>
    public DocContext Ctx { get; }

    /// <summary>页面索引号（页码从 1 开始）</summary>
    public int Index { get; }

    /// <summary>是否复制模板（默认 true）</summary>
    public bool CopyTemplate { get; set; } = true;

    /// <summary>是否复制注释（默认 true）</summary>
    public bool CopyAnnotations { get; set; } = true;

    /// <summary>需要混合到本页的其他文档页面（叠放在本页之上）</summary>
    public List<PageEntry> MixPages { get; } = new();

    public PageEntry(DocContext ctx, int index)
    {
        Ctx = ctx;
        Index = index;
    }

    public PageEntry(DocContext ctx, int index, params PageEntry[] mixPages)
    {
        Ctx = ctx;
        Index = index;
        if (mixPages != null) MixPages.AddRange(mixPages);
    }
}
