using System.Xml.Linq;
using NetOfd.Core.PageDescription.Color;

namespace NetOfd.Core.Image;

/// <summary>
/// 边框颜色（元素名 BorderColor，语义同 8.3.2 基本颜色，默认黑色）。
/// 对应 Java: org.ofdrw.core.image.BorderColor
/// </summary>
public class BorderColor : CT_Color
{
    public BorderColor() : base("BorderColor") { }
    public BorderColor(XElement element) : base(element) { }
}
