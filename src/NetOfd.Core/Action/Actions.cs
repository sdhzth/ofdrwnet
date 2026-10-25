using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

namespace NetOfd.Core.Action;

/// <summary>
/// 动作序列（图 19 大纲节点结构中的 Actions）。
/// 对应 Java: org.ofdrw.core.action.Actions
/// </summary>
public class Actions : OFDElement
{
    public Actions() : base("Actions") { }
    public Actions(XElement element) : base(element) { }

    /// <summary>【必选】增加动作到列表</summary>
    public Actions AddAction(CT_Action action)
    {
        AddOFDEntity(action);
        return this;
    }

    /// <summary>【必选】获取动作列表</summary>
    public List<CT_Action> GetActions() =>
        GetChildElements("Action").Select(e => new CT_Action(e)).ToList();
}
