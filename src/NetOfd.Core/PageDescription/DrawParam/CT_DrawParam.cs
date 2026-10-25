using System.Xml.Linq;
using NetOfd.Core.BasicType;
using Color = NetOfd.Core.PageDescription.Color;

namespace NetOfd.Core.PageDescription.DrawParam;

/// <summary>
/// 绘制参数
/// </summary>
public class CT_DrawParam : OFDElement
{
    public CT_DrawParam() : base("DrawParam") { }

    public CT_DrawParam(XElement xe) : base(xe) { }

    public string? Id { get => GetAttr("ID"); set => SetAttr("ID", value); }

    /// <summary>
    /// 父参数 ID（GB/T 33190 §8.1.2）：未在本参数中指定的属性继承自父参数。
    /// </summary>
    public string? ParentID { get => GetAttr("ParentID"); set => SetAttr("ParentID", value); }

    // ── 原始值探测（区分"未设置（应继承父值）"与默认值）─────────
    internal double? RawLineWidth => GetChildText("LineWidth") is null ? null : LineWidth;
    internal LineJoinType? RawJoin => GetChildText("Join") is null ? null : Join;
    internal LineCapType? RawCap => GetChildText("Cap") is null ? null : Cap;
    internal double? RawAlpha => GetAttr("Alpha") is null ? null : Alpha;
    internal bool? RawVisible => GetAttr("Visible") is null ? null : Visible;
    internal Color.CT_Color? RawStrokeColor => GetChildElement("StrokeColor") is null ? null : StrokeColor;
    internal Color.CT_Color? RawFillColor => GetChildElement("FillColor") is null ? null : FillColor;

    // 仅供 DrawParamResolver 合并写入（null = 移除，回退继承默认）
    internal void RawSetLineWidth(double? v) => SetChildText("LineWidth", v is null ? null : STBase.Fmt(v.Value));
    internal void RawSetJoin(LineJoinType? v) => SetChildText("Join", v?.ToString());
    internal void RawSetCap(LineCapType? v) => SetChildText("Cap", v?.ToString());
    internal void RawSetAlpha(double? v) => SetAttr("Alpha", v?.ToString());
    internal void RawSetVisible(bool? v) => SetAttr("Visible", v?.ToString());

    // ── 透明度（0-255，缺省 255 完全不透明）────────────────────
    public double? Alpha
    {
        get
        {
            var v = GetAttr("Alpha");
            return v is null ? 255 : STBase.ToDouble(v);
        }
        set => SetAttr("Alpha", value?.ToString());
    }

    // ── 可见性（缺省可见）────────────────────────────────────
    public bool Visible
    {
        get => GetBoolAttr("Visible", true);
        set => SetBoolAttr("Visible", value);
    }

    /// <summary>
    /// 混合模式（BlendMode），如 Normal / Multiply 等。
    /// 当前渲染器仅消费 Alpha 与 Visible，混合模式暂未参与合成。
    /// </summary>
    public string? Composite
    {
        get => GetAttr("Composite");
        set => SetAttr("Composite", value);
    }

    public double LineWidth
    {
        get
        {
            var v = GetChildText("LineWidth");
            return v is null ? 0.353 : STBase.ToDouble(v);
        }
        set => SetChildText("LineWidth", STBase.Fmt(value));
    }

    /// <summary>描边颜色（可被子参数/图元覆盖）</summary>
    public Color.CT_Color? StrokeColor
    {
        get
        {
            var e = GetChildElement("StrokeColor");
            return e is null ? null : new Color.CT_Color(e);
        }
        set
        {
            if (value is null) GetChildElement("StrokeColor")?.Remove();
            else
            {
                value._el.Name = OfdNs + "StrokeColor";
                SetOFDEntity(value);
            }
        }
    }

    /// <summary>填充颜色（可被子参数/图元覆盖）</summary>
    public Color.CT_Color? FillColor
    {
        get
        {
            var e = GetChildElement("FillColor");
            return e is null ? null : new Color.CT_Color(e);
        }
        set
        {
            if (value is null) GetChildElement("FillColor")?.Remove();
            else
            {
                value._el.Name = OfdNs + "FillColor";
                SetOFDEntity(value);
            }
        }
    }

    public LineJoinType Join
    {
        get
        {
            var v = GetChildText("Join");
            return v is null ? LineJoinType.Miter : Enum.Parse<LineJoinType>(v, ignoreCase: true);
        }
        set => SetChildText("Join", value.ToString());
    }

    public LineCapType Cap
    {
        get
        {
            var v = GetChildText("Cap");
            return v is null ? LineCapType.Butt : Enum.Parse<LineCapType>(v, ignoreCase: true);
        }
        set => SetChildText("Cap", value.ToString());
    }
}

/// <summary>
/// 线帽样式
/// </summary>
public enum LineCapType
{
    Butt,
    Square,
    Round
}

/// <summary>
/// 线连接样式
/// </summary>
public enum LineJoinType
{
    Miter,
    Round,
    Bevel
}
