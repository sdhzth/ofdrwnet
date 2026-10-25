using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 径向渐变（GB/T 33190 §8.3.4.2 表 29）。
/// <para>
/// 由起始椭圆（StartPoint + StartRadius）渐变到结束椭圆（EndPoint + EndRadius），
/// 椭圆的形状由离心率 Eccentricity 与倾斜角 Angle 决定。
/// </para>
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.CT_RadialShd
/// </summary>
public class CT_RadialShd : CT_Shd
{
    public CT_RadialShd() : base("RadialShd") { }
    public CT_RadialShd(XElement element) : base(element) { }

    /// <summary>
    /// 【可选 属性】离心率，用于确定椭圆的形状，取值范围 [0,1]。
    /// 0 表示圆，值越接近 1 椭圆越扁，默认值为 0。
    /// </summary>
    public double Eccentricity
    {
        get => GetDoubleAttr("Eccentricity", 0.0);
        set => SetDoubleAttr("Eccentricity", value);
    }

    /// <summary>【可选 属性】椭圆倾斜的角度，单位为度，默认值为 0。</summary>
    public double Angle
    {
        get => GetDoubleAttr("Angle", 0.0);
        set => SetDoubleAttr("Angle", value);
    }

    /// <summary>【必选 属性】起始椭圆的半径，默认值为 0。</summary>
    public double StartRadius
    {
        get => GetDoubleAttr("StartRadius", 0.0);
        set => SetDoubleAttr("StartRadius", value);
    }

    /// <summary>【必选 属性】结束椭圆的半径，默认值为 0。</summary>
    public double EndRadius
    {
        get => GetDoubleAttr("EndRadius", 0.0);
        set => SetDoubleAttr("EndRadius", value);
    }
}
