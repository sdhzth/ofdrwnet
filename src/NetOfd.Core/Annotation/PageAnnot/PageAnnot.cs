using System.Xml.Linq;

namespace NetOfd.Core.Annotation.PageAnnot;

/// <summary>
/// 分页注释文件（PageAnnot.xml），包含若干注释对象。
/// <para>GB/T 33190-2016 第 15.2 节</para>
/// 对应 Java: org.ofdrw.core.annotation.pageannot.PageAnnot
/// </summary>
public class PageAnnot : OFDElement
{
    public PageAnnot() : base("PageAnnot") { }

    public PageAnnot(XElement element) : base(element) { }

    /// <summary>【必选】增加注释对象。</summary>
    public PageAnnot AddAnnot(Annot annot)
    {
        if (annot is null) return this;
        _el.Add(annot.Element);
        return this;
    }

    /// <summary>【必选】获取注释对象列表。</summary>
    public List<Annot> GetAnnots()
    {
        return GetChildElements("Annot")
            .Select(e => new Annot(e))
            .ToList();
    }
}
