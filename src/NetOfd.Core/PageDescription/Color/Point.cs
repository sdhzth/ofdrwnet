using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 渐变控制点（GB/T 33190 §8.3.4.4 表 31，附录 A.13）。
/// <para>
/// 携带坐标（X/Y，必选）、可选的三角单元切换方向标志（<see cref="EdgeFlag"/>）
/// 与对应的颜色（<c>Color</c>，必选、应使用基本颜色）。
/// </para>
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.Point
/// </summary>
public class Point : OFDElement
{
    public Point() : base("Point") { }
    public Point(XElement element) : base(element) { }

    /// <summary>【必选 属性】控制点水平位置（Java 语义：缺失视为文档错误，此处宽松返回 null）。</summary>
    public double? X
    {
        get
        {
            var v = GetAttr("X");
            return v is null ? null : STBase.ToDouble(v);
        }
        set => SetAttr("X", value is null ? null : STBase.Fmt(value.Value));
    }

    /// <summary>【必选 属性】控制点垂直位置（Java 语义：缺失视为文档错误，此处宽松返回 null）。</summary>
    public double? Y
    {
        get
        {
            var v = GetAttr("Y");
            return v is null ? null : STBase.ToDouble(v);
        }
        set => SetAttr("Y", value is null ? null : STBase.Fmt(value.Value));
    }

    /// <summary>【可选 属性】三角单元切换的方向标志，缺省为 <see cref="EdgeFlag.Flag0"/>（Java getEdgeFlag 空串→_0）。</summary>
    public EdgeFlag? EdgeFlag
    {
        get
        {
            var v = GetAttr("EdgeFlag");
            return v is null ? null : EdgeFlagUtil.Parse(v);
        }
        set => SetAttr("EdgeFlag", value is null ? null : EdgeFlagUtil.ToXml(value.Value));
    }

    /// <summary>【必选】控制点对应的颜色（应是基本颜色）。</summary>
    public CT_Color? Color
    {
        get
        {
            var e = GetChildElement("Color");
            return e is null ? null : new CT_Color(e);
        }
        set
        {
            if (value is null) throw new ArgumentNullException(nameof(value), "控制点对应的颜色（Color）不能为空");
            SetOFDEntity(value);
        }
    }

    // ── Fluent 便捷构造 ─────────────────────────────────────

    /// <summary>设置控制点水平位置。</summary>
    public Point SetX(double x) { X = x; return this; }

    /// <summary>设置控制点垂直位置。</summary>
    public Point SetY(double y) { Y = y; return this; }

    /// <summary>设置三角单元切换方向标志（null 移除属性）。</summary>
    public Point SetEdgeFlag(EdgeFlag? flag) { EdgeFlag = flag; return this; }

    /// <summary>设置控制点颜色（必选）。</summary>
    public Point SetColor(CT_Color color) { Color = color; return this; }
}
