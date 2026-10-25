using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 底纹单元（CellContent）：用底纹填充目标区域时使用的单元对象。
/// <para>
/// 使用独立坐标空间：左上角 (0,0) 为原点、X 向右、Y 向下，单位为毫米。
/// 对应 Java: org.ofdrw.core.pageDescription.color.pattern.CellContent
/// （Java 中继承 CT_PageBlock；NetOfd Core 无页块容器模型，故直接作 OFDElement
/// 元素容器，容纳 ImageObject / PathObject / TextObject 等图元子元素，序列化一致）。</para>
/// </summary>
public class CellContent : OFDElement
{
    public CellContent() : base("CellContent") { }
    public CellContent(XElement element) : base(element) { }

    /// <summary>【可选 属性】引用资源文件中缩略图图像的标识符。</summary>
    public STRefID? Thumbnail
    {
        get
        {
            var v = GetAttr("Thumbnail");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("Thumbnail", value?.Value.ToString());
    }

    /// <summary>底纹单元内的图形/图像对象（子元素图元，宽松命名空间匹配读回）。</summary>
    public IReadOnlyList<CTGraphicUnit> GetPageBlocks() =>
        Element.Elements()
            .Where(e => e.Name.LocalName is "ImageObject" or "PathObject" or "TextObject")
            .Select(e => e.Name.LocalName switch
            {
                "ImageObject" => (CTGraphicUnit)new CTImage(e),
                "PathObject" => new CTPath(e),
                _ => new CTText(e)
            })
            .ToList();

    /// <summary>【可选】追加一个页块图元到底纹单元。</summary>
    public CellContent AddPageBlock(CTGraphicUnit pageBlock)
    {
        Element.Add(pageBlock.Element);
        return this;
    }

    /// <summary>获取底纹单元内首个图像对象（ImageObject），不存在时返回 null。</summary>
    public CTImage? GetImage()
    {
        var e = Element.Elements().FirstOrDefault(x => x.Name.LocalName == "ImageObject");
        return e is null ? null : new CTImage(e);
    }
}
