using System.Xml.Linq;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 轴向渐变（GB/T 33190 §8.3.4.2 图 29、30 表 29）。
/// <para>
/// 颜色沿由 StartPoint → EndPoint 决定的轴线方向渐变，
/// 与该轴线垂直的直线上的点颜色相同。
/// </para>
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.CT_AxialShd
/// </summary>
public class CT_AxialShd : CT_Shd
{
    public CT_AxialShd() : base("AxialShd") { }
    public CT_AxialShd(XElement element) : base(element) { }

    /// <summary>便捷构造：指定轴线起止点。</summary>
    public CT_AxialShd(double x0, double y0, double x1, double y1) : this()
    {
        StartPoint = new BasicType.STPos(x0, y0);
        EndPoint = new BasicType.STPos(x1, y1);
    }
}
