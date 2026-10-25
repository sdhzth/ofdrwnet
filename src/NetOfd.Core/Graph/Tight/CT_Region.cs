using System.Xml.Linq;

namespace NetOfd.Core.Graph.Tight;

/// <summary>
/// 区域（Region）：由一系列分路径（<see cref="CT_Area"/>）组成，每条路径闭合。
/// 用于以 XML 负载方式描述图形（相对于紧缩串 <c>AbbreviatedData</c>）。
/// 图 49 区域结构
/// 对应 Java: org.ofdrw.core.graph.tight.CT_Region
/// </summary>
public sealed class CT_Region : OFDElement
{
    public CT_Region() : base("Region") { }

    public CT_Region(XElement element) : base(element) { }

    /// <summary>【必选】为区域增加一条分路径；null 时忽略。</summary>
    public CT_Region AddArea(CT_Area area)
    {
        if (area is null) return this;
        AddOFDEntity(area);
        return this;
    }

    /// <summary>【必选】获取区域中所有分路径。</summary>
    public List<CT_Area> GetAreas() =>
        GetChildElements("Area").Select(e => new CT_Area(e)).ToList();
}
