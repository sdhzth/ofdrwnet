using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;

namespace NetOfd.Core.Image;

/// <summary>
/// 图像边框（10 表 43）。
/// 对应 Java: org.ofdrw.core.image.Border
/// </summary>
public class Border : OFDElement
{
    public Border() : base("Border") { }
    public Border(XElement element) : base(element) { }

    /// <summary>
    /// 【可选 属性】边框线宽；为 0 表示不绘制。
    /// <para>默认值为 0.353 mm</para>
    /// </summary>
    public double LineWidth
    {
        get => GetDoubleAttr("LineWidth", 0.353d);
        set => SetDoubleAttr("LineWidth", value);
    }

    /// <summary>【可选 属性】边框水平角半径，默认 0。</summary>
    public double HorizonalCornerRadius
    {
        get => GetDoubleAttr("HorizonalCornerRadius", 0d);
        set => SetDoubleAttr("HorizonalCornerRadius", value);
    }

    /// <summary>【可选 属性】边框垂直角半径，默认 0。</summary>
    public double VerticalCornerRadius
    {
        get => GetDoubleAttr("VerticalCornerRadius", 0d);
        set => SetDoubleAttr("VerticalCornerRadius", value);
    }

    /// <summary>
    /// 【可选 属性】边框虚线重复样式开始的位置。
    /// <para>边框的起点位置为左上角，绕行方向为顺时针；默认 0。</para>
    /// </summary>
    public double DashOffset
    {
        get => GetDoubleAttr("DashOffset", 0d);
        set => SetDoubleAttr("DashOffset", value);
    }

    /// <summary>【可选 属性】边框虚线重复样式；不存在时返回 null。</summary>
    public STArray? DashPattern
    {
        get
        {
            var v = GetAttr("DashPattern");
            return string.IsNullOrWhiteSpace(v) ? null : STArray.Parse(v!);
        }
        set => SetAttr("DashPattern", value?.ToString());
    }

    /// <summary>
    /// 【可选】设置边框颜色；传入非 <see cref="BorderColor"/> 的
    /// <see cref="CT_Color"/> 时自动把元素名改写为 BorderColor（对齐 Java）。
    /// </summary>
    public Border SetBorderColor(CT_Color borderColor)
    {
        if (borderColor is null) return this;
        if (borderColor is not BorderColor)
            borderColor.Element.Name = OfdNs + "BorderColor";
        SetOFDEntity(borderColor);
        return this;
    }

    /// <summary>【可选】获取边框颜色；不存在时返回 null（表示黑色）。</summary>
    public BorderColor? GetBorderColor()
    {
        var e = GetChildElement("BorderColor");
        return e is null ? null : new BorderColor(e);
    }
}
