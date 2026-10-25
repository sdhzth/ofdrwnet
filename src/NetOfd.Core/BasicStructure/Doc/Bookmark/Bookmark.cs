using System.Xml.Linq;
using NetOfd.Core.Action.ActionType.ActionGoto;

namespace NetOfd.Core.BasicStructure.Doc.Bookmark;

/// <summary>
/// 本标准支持书签，可以将常用位置定义为书签，文档可以包含一组书签（7.5 图 11 书签结构）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.bookmark.Bookmark
/// </summary>
public class Bookmark : OFDElement
{
    public Bookmark() : base("Bookmark") { }
    public Bookmark(XElement element) : base(element) { }

    public Bookmark(string name, CT_Dest dest) : this()
    {
        BookmarkName = name;
        Dest = dest;
    }

    /// <summary>【必选 属性】书签名称</summary>
    public string? BookmarkName
    {
        get => GetAttr("Name");
        set => SetAttr("Name", value);
    }

    /// <summary>【必选】书签对应的文档版位置</summary>
    public CT_Dest? Dest
    {
        get
        {
            var e = GetChildElement("Dest");
            return e is null ? null : new CT_Dest(e);
        }
        set
        {
            if (value is null) GetChildElement("Dest")?.Remove();
            else SetOFDEntity(value);
        }
    }
}
