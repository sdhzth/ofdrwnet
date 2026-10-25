using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.Doc.VPreferences.Zoom;

/// <summary>
/// 文档的缩放率节点（<c>&lt;Zoom&gt;1.5&lt;/Zoom&gt;</c>）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.zoom.Zoom
/// </summary>
public class Zoom : ZoomScale
{
    public Zoom(XElement element) : base(element) { }

    public Zoom(double value) : base("Zoom") => SetValue(value);

    /// <summary>设置文档的缩放率</summary>
    public Zoom SetValue(double value)
    {
        SetText(STBase.Fmt(value));
        return this;
    }

    /// <summary>获取文档的缩放率；节点为空时抛异常（对齐 Java Double.parseDouble）。</summary>
    public double Value
    {
        get
        {
            var s = GetText();
            if (string.IsNullOrWhiteSpace(s))
                throw new InvalidOperationException("Zoom 节点缺少缩放率文本");
            return STBase.ToDouble(s.Trim());
        }
    }
}
