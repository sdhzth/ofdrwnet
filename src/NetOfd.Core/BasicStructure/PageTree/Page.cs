using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.PageTree;

/// <summary>
/// 页面树节点，记录 Page ID 和页面文件位置。
/// 对应 Java: org.ofdrw.core.basicStructure.pageTree.Page
/// </summary>
public class Page : OFDElement
{
    public Page() : base("Page") { }
    public Page(XElement element) : base(element) { }

    /// <summary>页面 ID</summary>
    public STID ID
    {
        get => new STID(GetLongAttr("ID"));
        set => SetLongAttr("ID", value.Value);
    }

    /// <summary>页面内容文件的基本路径（相对于 Doc 目录）</summary>
    public STLoc? BaseLoc
    {
        get
        {
            var v = GetAttr("BaseLoc");
            return v is null ? null : new STLoc(v);
        }
        set => SetAttr("BaseLoc", value?.Value);
    }
}
