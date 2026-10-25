using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Annotation.PageAnnot;

/// <summary>
/// 注释所在页（注释入口文件中的分页索引条目）。
/// <para>GB/T 33190-2016 第 15.1 节</para>
/// 对应 Java: org.ofdrw.core.annotation.pageannot.AnnPage
/// </summary>
public class AnnPage : OFDElement
{
    public AnnPage() : base("Page") { }

    public AnnPage(XElement element) : base(element) { }

    /// <summary>【必选】引用注释所在页面的标识（PageID 属性）。</summary>
    public STRefID? PageID
    {
        get
        {
            var v = GetAttr("PageID");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("PageID", value?.ToString());
    }

    /// <summary>【必选】指向包内的分页注释文件。</summary>
    public STLoc? FileLoc
    {
        get
        {
            var v = GetChildText("FileLoc");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("FileLoc", value?.Value);
    }
}
