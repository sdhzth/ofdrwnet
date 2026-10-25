using System.Xml.Linq;
using NetOfd.Core.Annotation.PageAnnot;

namespace NetOfd.Core.Annotation;

/// <summary>
/// 注释入口文件（Annotations.xml）。
/// <para>
/// 注释是板式文档形成后附加的图文信息，用户可通过鼠标和键盘与之进行交互。
/// 本标准中，页面内容与注释内容是分文件描述的。文档的注释在注释列表文件中
/// 按照页面进行组织索引，注释的内容在分页注释文件中描述。
/// </para>
/// <para>GB/T 33190-2016 第 15.1 节</para>
/// 对应 Java: org.ofdrw.core.annotation.Annotations
/// </summary>
public class Annotations : OFDElement
{
    public Annotations() : base("Annotations") { }

    public Annotations(XElement element) : base(element) { }

    /// <summary>【可选】增加注释所在页。</summary>
    public Annotations AddPage(AnnPage page)
    {
        if (page is null) return this;
        _el.Add(page.Element);
        return this;
    }

    /// <summary>根据页面 ID 获取注释所在页条目（不存在返回 null）。</summary>
    public AnnPage? GetByPageId(string id)
    {
        foreach (var page in GetPages())
        {
            var pid = page.PageID;
            if (pid != null && pid.Value.ToString().Equals(id?.Trim(), StringComparison.Ordinal))
                return page;
        }
        return null;
    }
    /// <summary>【可选】获取注释所在页序列。</summary>
    public List<AnnPage> GetPages()
    {
        return GetChildElements("Page")
            .Select(e => new AnnPage(e))
            .ToList();
    }
}
