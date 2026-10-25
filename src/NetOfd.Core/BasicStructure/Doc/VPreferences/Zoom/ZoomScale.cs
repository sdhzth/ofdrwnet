using System.Xml.Linq;

namespace NetOfd.Core.BasicStructure.Doc.VPreferences.Zoom;

/// <summary>
/// 缩放比例选择对象基类。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.zoom.ZoomScale
/// <para>
/// VPreferences 下的 Zoom / ZoomMode 二选一，二者共享本基类。
/// </para>
/// </summary>
public abstract class ZoomScale : OFDElement
{
    protected ZoomScale(XElement element) : base(element) { }
    protected ZoomScale(string name) : base(name) { }
}
