using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using NetOfd.Core.Action.ActionType;

namespace NetOfd.Core.Action;

/// <summary>
/// 动作类型结构（图 73 动作类型结构）。包含事件类型与具体动作。
/// 对应 Java: org.ofdrw.core.action.CT_Action
/// </summary>
public class CT_Action : OFDElement
{
    public CT_Action() : base("Action") { }
    public CT_Action(XElement element) : base(element) { }

    public CT_Action(EventType type, OFDElement action) : this()
    {
        Event = type;
        SetAction(action);
    }

    /// <summary>【必选 属性】事件类型（触发动作的条件）</summary>
    public EventType Event
    {
        get
        {
            var v = GetAttr("Event");
            if (v is null) return EventType.DO;
            return Enum.TryParse<EventType>(v, true, out var t) ? t : EventType.DO;
        }
        set => SetAttr("Event", value.ToString());
    }

    /// <summary>【必选】设置动作（替换已有的 URI/Sound/GotoA/Movie/Goto 子节点）</summary>
    public CT_Action SetAction(OFDElement action)
    {
        _el.Elements()
            .Where(e => e.Name.LocalName is "URI" or "Sound" or "GotoA" or "Movie" or "Goto")
            .Remove();
        AddOFDEntity(action);
        return this;
    }

    /// <summary>【必选】获取动作（可能为 null）</summary>
    public IOFDAction? GetAction()
    {
        var child = _el.Elements().FirstOrDefault();
        return child is null ? null : ActionType.OFDAction.GetInstance(child);
    }
}
