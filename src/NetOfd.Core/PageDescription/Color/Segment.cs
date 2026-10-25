using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 渐变颜色段（GB/T 33190 §8.3.4.2 表 29），至少出现两个。
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.Segment
/// </summary>
public class Segment : OFDElement
{
    public Segment() : base("Segment") { }
    public Segment(XElement element) : base(element) { }

    /// <summary>构造带颜色的段。</summary>
    public Segment(CT_Color color) : this() => Color = color;

    /// <summary>构造带位置与颜色的段。</summary>
    public Segment(double position, CT_Color color) : this()
    {
        Position = position;
        Color = color;
    }

    /// <summary>
    /// 【可选 属性】渐变位置参数，取值范围 [0, 1.0]，用于确定 StartPoint 与 EndPoint 之间的位置。
    /// <para>
    /// 首个 Segment 默认为 0，最后一个默认为 1.0；缺失时在空缺区间内平均分配
    /// （2 段按 0/1.0，3 段按 0/0.5/1.0，5 段按 0/0.25/0.5/0.75/1.0）。
    /// </para>
    /// </summary>
    public double? Position
    {
        get
        {
            var v = GetAttr("Position");
            return v is null ? null : STBase.ToDouble(v);
        }
        set => SetAttr("Position", value is null ? null : STBase.Fmt(value.Value));
    }

    /// <summary>【必选】该段的颜色（应是基本颜色）。</summary>
    public CT_Color? Color
    {
        get
        {
            var e = GetChildElement("Color");
            return e is null ? null : new CT_Color(e);
        }
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value), "段颜色（Color）不能为空");
            SetOFDEntity(value);
        }
    }
}
