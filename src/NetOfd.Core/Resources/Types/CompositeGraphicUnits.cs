using System.Xml.Linq;
using NetOfd.Core.CompositeObj;

namespace NetOfd.Core.Resources;

/// <summary>
/// 矢量图像资源集合（7.9 图 20 表 18）：复合对象引用的 VectorG 资源。
/// 对应 Java: org.ofdrw.core.basicStructure.res.resources.CompositeGraphicUnits
/// </summary>
public sealed class CompositeGraphicUnits : OFDElement, IOFDResource
{
    public CompositeGraphicUnits() : base("CompositeGraphicUnits") { }

    public CompositeGraphicUnits(XElement element) : base(element) { }

    /// <summary>
    /// 【必选】添加矢量图像资源描述；null 时忽略，缺少 ID 时抛 <see cref="ArgumentException"/>。
    /// 添加时元素名会被重写为 <c>CompositeGraphicUnit</c>（与 Java 一致）。
    /// </summary>
    public CompositeGraphicUnits AddCompositeGraphicUnit(CT_VectorG unit)
    {
        if (unit is null) return this;
        if (unit.GetObjID() is null) throw new ArgumentException("矢量图像资源描述ID不能为空");
        unit.Element.Name = OfdNs + "CompositeGraphicUnit";
        AddOFDEntity(unit);
        return this;
    }

    /// <summary>【必选】获取矢量图像资源描述序列。</summary>
    public List<CT_VectorG> GetCompositeGraphicUnits() =>
        GetChildElements("CompositeGraphicUnit").Select(e => new CT_VectorG(e)).ToList();
}
