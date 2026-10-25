using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription;

/// <summary>
/// CT_Image - 图像对象。
/// 对应 Java: org.ofdrw.core.pageDescription.content.layer.ImageObject
/// </summary>
public class CTImage : CTGraphicUnit
{
    public CTImage() : base("ImageObject") { }
    public CTImage(XElement element) : base(element) { }

    /// <summary>图像资源 ID 引用</summary>
    public STRefID? ResourceID
    {
        get
        {
            var v = GetAttr("ResourceID");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("ResourceID", value?.Value.ToString());
    }

    /// <summary>替换图像（水印图像）</summary>
    public STRefID? Substitution
    {
        get
        {
            var v = GetAttr("Substitution");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("Substitution", value?.Value.ToString());
    }

    /// <summary>图像蒙版</summary>
    public STRefID? ImageMask
    {
        get
        {
            var v = GetAttr("ImageMask");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("ImageMask", value?.Value.ToString());
    }
}
