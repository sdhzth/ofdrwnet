using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Action.ActionType.ActionGoto;

/// <summary>
/// 目标区域描述方法（表 54）。
/// 对应 Java: org.ofdrw.core.action.actionType.actionGoto.DestType
/// </summary>
public enum DestType
{
    /// <summary>由左上角位置（Left,Top）以及页面缩放比例（Zoom）确定</summary>
    XYZ,
    /// <summary>适合整个窗口区域</summary>
    Fit,
    /// <summary>适合窗口宽度，目标区域由 Top 确定</summary>
    FitH,
    /// <summary>适合窗口高度，目标区域由 Left 确定</summary>
    FitV,
    /// <summary>适合窗口内的目标区域（Left,Top,Right,Bottom 矩形）</summary>
    FitR
}

/// <summary>
/// 目标区域（图 75 目标区域结构）。
/// 对应 Java: org.ofdrw.core.action.actionType.actionGoto.CT_Dest
/// </summary>
public class CT_Dest : OFDElement, IOFDGotoTarget
{
    public CT_Dest() : base("Dest") { }
    public CT_Dest(XElement element) : base(element) { }

    /// <summary>【必选】目标区域的描述方法</summary>
    public DestType Type
    {
        get
        {
            var v = GetAttr("Type");
            if (v is null) return DestType.XYZ;
            return Enum.TryParse<DestType>(v, true, out var t) ? t : DestType.XYZ;
        }
        set => SetAttr("Type", value.ToString());
    }

    /// <summary>【必选】引用跳转目标页面的标识</summary>
    public STRefID? PageID
    {
        get
        {
            var v = GetAttr("PageID");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("PageID", value?.ToString());
    }

    /// <summary>【可选】目标区域左上角 x 坐标，默认 0</summary>
    public double Left
    {
        get => GetDoubleAttr("Left");
        set => SetDoubleAttr("Left", value);
    }

    /// <summary>【可选】目标区域右上角 x 坐标，默认 0</summary>
    public double Right
    {
        get => GetDoubleAttr("Right");
        set => SetDoubleAttr("Right", value);
    }

    /// <summary>【可选】目标区域左上角 y 坐标，默认 0</summary>
    public double Top
    {
        get => GetDoubleAttr("Top");
        set => SetDoubleAttr("Top", value);
    }

    /// <summary>【可选】目标区域右下角 y 坐标，默认 0</summary>
    public double Bottom
    {
        get => GetDoubleAttr("Bottom");
        set => SetDoubleAttr("Bottom", value);
    }

    /// <summary>【可选】目标区域页面缩放比例，默认 0（按当前缩放比例）</summary>
    public double Zoom
    {
        get => GetDoubleAttr("Zoom");
        set => SetDoubleAttr("Zoom", value);
    }
}
