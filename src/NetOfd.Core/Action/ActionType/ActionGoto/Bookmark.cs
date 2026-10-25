using System.Xml.Linq;

namespace NetOfd.Core.Action.ActionType.ActionGoto;

/// <summary>
/// 跳转的目的书签（表 53 跳转动作属性）。引用文档书签中的名称。
/// 对应 Java: org.ofdrw.core.action.actionType.actionGoto.Bookmark
/// </summary>
public class Bookmark : OFDElement, IOFDGotoTarget
{
    public Bookmark() : base("Bookmark") { }
    public Bookmark(XElement element) : base(element) { }

    public Bookmark(string name) : this()
    {
        BookmarkName = name;
    }

    /// <summary>【必选 属性】目标书签的名称，引用文档书签中的名称</summary>
    public string? BookmarkName
    {
        get => GetAttr("Name");
        set => SetAttr("Name", value);
    }
}
