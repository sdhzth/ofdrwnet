using System.Xml.Linq;
using NetOfd.Core.PageDescription.Color.colorSpace;

namespace NetOfd.Core.Resources;

/// <summary>
/// 颜色空间资源集合（7.9 图 20 表 18）。
/// 对应 Java: org.ofdrw.core.basicStructure.res.resources.ColorSpaces
/// </summary>
public sealed class ColorSpaces : OFDElement, IOFDResource
{
    public ColorSpaces() : base("ColorSpaces") { }

    public ColorSpaces(XElement element) : base(element) { }

    /// <summary>
    /// 【必选】添加颜色空间描述；null 时忽略，缺少 ID 时抛 <see cref="ArgumentException"/>。
    /// </summary>
    public ColorSpaces AddColorSpace(CT_ColorSpace colorSpace)
    {
        if (colorSpace is null) return this;
        if (colorSpace.ID is null) throw new ArgumentException("颜色空间ID不能为空");
        AddOFDEntity(colorSpace);
        return this;
    }

    /// <summary>【必选】获取颜色空间描述列表。</summary>
    public List<CT_ColorSpace> GetColorSpaces() =>
        GetChildElements("ColorSpace").Select(e => new CT_ColorSpace(e)).ToList();
}
