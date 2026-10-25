using System.Xml.Linq;
using NetOfd.Core.Action.ActionType.ActionGoto;
using NetOfd.Core.Action.ActionType.ActionMovie;

namespace NetOfd.Core.Action.ActionType;

/// <summary>
/// 动作类型标记接口。
/// 对应 Java: org.ofdrw.core.action.actionType.OFDAction
/// </summary>
public interface IOFDAction
{
}

/// <summary>
/// 未明确支持的具体动作类型时，保留原始元素的通用包装，保证序列化往返不丢信息。
/// 对应 Java 中除 Goto 外的其它动作（URI/GotoA/Sound/Movie 等）。
/// </summary>
public class GenericAction : OFDElement, IOFDAction
{
    public GenericAction(XElement element) : base(element) { }
    public GenericAction(string localName) : base(localName) { }
}

/// <summary>
/// 动作类型实例工厂。
/// 对应 Java: org.ofdrw.core.action.actionType.OFDAction#getInstance
/// </summary>
public static class OFDAction
{
    /// <summary>
    /// 根据元素获取动作实例：Goto/GotoA/URI/Sound/Movie 解析为对应强类型，
    /// 其它未知类型包装为 <see cref="GenericAction"/>（保留原始元素，序列化往返不丢信息）。
    /// </summary>
    public static IOFDAction? GetInstance(XElement element)
    {
        if (element is null) return null;
        return element.Name.LocalName switch
        {
            "Goto" => new Goto(element),
            "GotoA" => new GotoA(element),
            "URI" => new URI(element),
            "Sound" => new Sound(element),
            "Movie" => new ActionMovie.Movie(element),
            _ => new GenericAction(element)
        };
    }
}
