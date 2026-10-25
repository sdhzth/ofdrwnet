using NetOfd.Core.BasicType;

namespace NetOfd.Core.Action.ActionType.ActionMovie;

/// <summary>
/// Movie 动作用于播放视频（图 79 播放视频动作属性）。
/// 对应 Java: org.ofdrw.core.action.actionType.actionMovie.Movie
/// </summary>
public class Movie : OFDElement, IOFDAction
{
    public Movie() : base("Movie") { }
    public Movie(System.Xml.Linq.XElement element) : base(element) { }

    public Movie(string resourceId) : this()
    {
        SetResourceID(STRefID.Parse(resourceId));
    }

    public Movie(STRefID resourceID) : this()
    {
        SetResourceID(resourceID);
    }

    /// <summary>【必选 属性】引用资源文件中定义的视频资源标识</summary>
    public STRefID? ResourceID
    {
        get
        {
            var v = GetAttr("ResourceID");
            return string.IsNullOrEmpty(v) ? null : STRefID.Parse(v!);
        }
        set => SetAttr("ResourceID", value is null ? null : value.ToString());
    }

    public Movie SetResourceID(STRefID resourceID)
    {
        ResourceID = resourceID;
        return this;
    }

    /// <summary>【可选 属性】放映参数（默认 Play），参见 <see cref="PlayType"/></summary>
    public PlayType? Operator
    {
        get
        {
            var v = GetAttr("Operator");
            if (string.IsNullOrWhiteSpace(v)) return null;
            return v!.ToPlayType();
        }
        set => SetAttr("Operator", value is null ? null : value.ToString());
    }

    public Movie SetOperator(PlayType? oper)
    {
        Operator = oper;
        return this;
    }
}
