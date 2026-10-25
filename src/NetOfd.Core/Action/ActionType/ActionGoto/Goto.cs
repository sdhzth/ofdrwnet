using System.Xml.Linq;

namespace NetOfd.Core.Action.ActionType.ActionGoto;

/// <summary>
/// 跳转动作，表明同一文档内的跳转，包括一个目的区域或书签位置（图 74 跳转动作结构）。
/// 对应 Java: org.ofdrw.core.action.actionType.actionGoto.Goto
/// </summary>
public class Goto : OFDElement, IOFDAction
{
    public Goto() : base("Goto") { }
    public Goto(XElement element) : base(element) { }

    public Goto(CT_Dest dest) : this()
    {
        SetDest(dest);
    }

    public Goto(string bookmarkName) : this()
    {
        SetBookmark(bookmarkName);
    }

    /// <summary>【必选】设置跳转的目的区域</summary>
    public Goto SetDest(CT_Dest dest)
    {
        RemoveByName("Bookmark", "Dest");
        AddOFDEntity(dest);
        return this;
    }

    /// <summary>【必选】设置跳转的目标书签</summary>
    public Goto SetBookmark(string name)
    {
        RemoveByName("Bookmark", "Dest");
        AddOFDEntity(new Bookmark(name));
        return this;
    }

    /// <summary>获取跳转动作的目标（CT_Dest 或 Bookmark），可能为 null</summary>
    public IOFDGotoTarget? GetTarget()
    {
        var child = _el.Elements().FirstOrDefault();
        if (child is null) return null;
        return child.Name.LocalName switch
        {
            "Dest" => new CT_Dest(child),
            "Bookmark" => new Bookmark(child),
            _ => null
        };
    }

    private void RemoveByName(params string[] names)
    {
        foreach (var n in names)
            _el.Elements().Where(e => e.Name.LocalName == n).Remove();
    }
}
