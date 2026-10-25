using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 高洛德渐变（GB/T 33190 §8.3.4.4 图 38-41 表 31）。
/// <para>
/// 基本思想：指定三个带有可选颜色的顶点，在其构成的三角形区域内采用高洛德算法
/// （颜色按面积坐标线性插值）绘制渐变；可定义多个三角区域，区域变化由各控制点的
/// 方向标志（<see cref="EdgeFlag"/>）指定（对齐 ISO 32000-1 type-4 free-form mesh）。
/// </para>
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.CT_GouraudShd
/// </summary>
public class CT_GouraudShd : OFDElement, ColorClusterType
{
    public CT_GouraudShd() : base("GouraudShd") { }
    public CT_GouraudShd(XElement element) : base(element) { }

    /// <summary>供派生类（网格高洛德）以不同元素名构造。</summary>
    protected CT_GouraudShd(string localName) : base(localName) { }

    /// <summary>
    /// 【可选 属性】在渐变控制点所确定范围之外的部分是否填充（Extend），0 为不填充、1 为填充；默认 0。
    /// <para>注意：此处为布尔（0/1），与轴向/径向渐变 <see cref="Extend"/> 枚举（0-3）语义不同。</para>
    /// </summary>
    public bool? Extend
    {
        get
        {
            var v = GetAttr("Extend");
            if (v is null) return null;
            return v.Trim() == "1";
        }
        set => SetAttr("Extend", value is null ? null : (value.Value ? "1" : "0"));
    }

    /// <summary>设置是否填充渐变控制点范围之外的区域（null 移除属性）。</summary>
    public CT_GouraudShd SetExtend(bool? extend) { Extend = extend; return this; }

    /// <summary>【必选】增加渐变控制点（至少出现三个；null 忽略）。</summary>
    public CT_GouraudShd AddPoint(Point point)
    {
        if (point is null) return this;
        AddOFDEntity(point);
        return this;
    }

    /// <summary>【必选】批量增加渐变控制点（至少出现三个）。</summary>
    public CT_GouraudShd AddPoints(params Point[] points)
    {
        foreach (var p in points) AddPoint(p);
        return this;
    }

    /// <summary>【必选】获取渐变控制点列表（至少出现三个）。</summary>
    public List<Point> GetPoints() =>
        GetChildElements("Point").Select(e => new Point(e)).ToList();

    /// <summary>便捷构造：以坐标+颜色三元组直接创建控制点（Java CT_GouraudShd 无此工厂，NetOfd 扩展）。</summary>
    public static Point P(double x, double y, CT_Color color, EdgeFlag? flag = null)
        => new Point().SetX(x).SetY(y).SetColor(color).SetEdgeFlag(flag);

    /// <summary>
    /// 【可选】渐变范围外的填充颜色（BackColor，应是基本颜色）。
    /// <para>挂接时把颜色元素改名为 <c>BackColor</c>（对齐 Java setOFDName 语义）。</para>
    /// </summary>
    public CT_Color? BackColor
    {
        get
        {
            var e = GetChildElement("BackColor");
            return e is null ? null : new CT_Color(e);
        }
        set
        {
            if (value is null)
            {
                GetChildElement("BackColor")?.Remove();
                return;
            }
            // Java: backColor.setOFDName("BackColor"); this.set(backColor);
            value.Element.Name = OFDElement.OfdNs + "BackColor";
            _el.Elements().Where(e => e.Name.LocalName == "BackColor").Remove();
            _el.Add(value.Element);
        }
    }

    /// <summary>设置渐变范围外的填充颜色（null 移除）。</summary>
    public CT_GouraudShd SetBackColor(CT_Color? backColor) { BackColor = backColor; return this; }
}
