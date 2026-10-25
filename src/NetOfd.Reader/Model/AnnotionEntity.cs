using NetOfd.Core.Annotation.PageAnnot;

namespace NetOfd.Reader.Model;

/// <summary>
/// 注释实体：一页的分页注释文件中包含的注释对象集合。
/// <para>对应 Java: <c>org.ofdrw.reader.model.AnnotionEntity</c>（pageId + annots）。
/// 命名沿用 Java 原拼写（AnnotionEntity，非 AnnotationEntity）。</para>
/// </summary>
public class AnnotionEntity
{
    /// <summary>注释所在页面 ID</summary>
    public string PageId { get; }

    /// <summary>该页注释对象列表（Annot 序列）</summary>
    public List<Annot> Annots { get; }

    public AnnotionEntity(string pageId, List<Annot> annots)
    {
        PageId = pageId;
        Annots = annots;
    }
}
