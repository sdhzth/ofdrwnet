using System.Xml.Linq;
using NetOfd.Core.CompositeObj;
using NetOfd.Core.PageDescription.Color.colorSpace;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.PageDescription.Font;

namespace NetOfd.Core.Resources;

/// <summary>
/// 资源文件抽象类型：绘制参数、颜色空间、字形、图像、音视频、矢量图像等资源集合的共性标记。
/// 对应 Java: org.ofdrw.core.basicStructure.res.OFDResource
/// </summary>
public interface IOFDResource
{
    /// <summary>底层 XML 元素</summary>
    XElement Element { get; }
}

/// <summary>
/// 资源类型工厂：按元素名分发到具体的资源集合子类。
/// 对应 Java: org.ofdrw.core.basicStructure.res.OFDResource#getInstance
/// </summary>
public static class OFDResource
{
    /// <summary>
    /// 解析元素并获取对应资源类型实例；无法识别时按名称关键字回退，
    /// 仍无法识别则抛 <see cref="ArgumentException"/>。
    /// </summary>
    public static IOFDResource? GetInstance(XElement element)
    {
        if (element is null) return null;

        var local = element.Name.LocalName;
        switch (local)
        {
            case "ColorSpaces":
                return new ColorSpaces(element);
            case "DrawParams":
                return new DrawParams(element);
            case "Fonts":
                return new Fonts(element);
            case "MultiMedias":
                return new MultiMedias(element);
            case "CompositeGraphicUnits":
                return new CompositeGraphicUnits(element);
            default:
                var lower = local.ToLowerInvariant();
                if (lower.Contains("draw")) return new DrawParams(element);
                if (lower.Contains("font")) return new Fonts(element);
                if (lower.Contains("color")) return new ColorSpaces(element);
                throw new ArgumentException($"不是 Res 的子类，未知元素类型：{local}");
        }
    }
}
