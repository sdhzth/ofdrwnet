using System.Xml.Linq;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;

namespace NetOfd.Core.Annotation.PageAnnot;

/// <summary>
/// 注释的静态呈现效果，使用页面块定义来描述。
/// <para>GB/T 33190-2016 第 15.2 节</para>
/// 对应 Java: org.ofdrw.core.annotation.pageannot.Appearance（继承 CT_PageBlock）
/// </summary>
public class Appearance : OFDElement, IGraphicContainer
{
    public Appearance() : base("Appearance") { }

    public Appearance(XElement element) : base(element) { }

    /// <summary>以边界创建注释外观。</summary>
    public Appearance(STBox boundary) : this()
    {
        Boundary = boundary;
    }

    /// <summary>【必选】注释绘制空间边界（附录 A.4）。</summary>
    public STBox? Boundary
    {
        get
        {
            var v = GetAttr("Boundary");
            return v is null ? null : STBox.Parse(v);
        }
        set => SetAttr("Boundary", value?.ToString());
    }

    /// <summary>添加文字对象。</summary>
    public Appearance AddText(CTText text) { _el.Add(text.Element); return this; }

    /// <summary>添加图像对象。</summary>
    public Appearance AddImage(CTImage image) { _el.Add(image.Element); return this; }

    /// <summary>添加路径对象。</summary>
    public Appearance AddPath(CTPath path) { _el.Add(path.Element); return this; }

    void IGraphicContainer.AddTextObject(CTText text) { AddText(text); }
    void IGraphicContainer.AddImageObject(CTImage image) { AddImage(image); }
    void IGraphicContainer.AddPathObject(CTPath path) { AddPath(path); }
}
