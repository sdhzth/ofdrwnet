using System.Xml.Linq;
using NetOfd.Core.PageDescription.Font;

namespace NetOfd.Core.Resources;

/// <summary>
/// 字体资源集合（7.9 图 20 表 18）：包含文档所有字形的描述。
/// 对应 Java: org.ofdrw.core.basicStructure.res.resources.Fonts
/// </summary>
public sealed class Fonts : OFDElement, IOFDResource
{
    public Fonts() : base("Fonts") { }

    public Fonts(XElement element) : base(element) { }

    /// <summary>
    /// 【必选】添加字形描述；null 时忽略，缺少 ID 时抛 <see cref="ArgumentException"/>。
    /// </summary>
    public Fonts AddFont(CT_Font font)
    {
        if (font is null) return this;
        if (font.GetObjID() is null) throw new ArgumentException("字形描述ID不能为空");
        AddOFDEntity(font);
        return this;
    }

    /// <summary>【必选】获取字形描述序列。</summary>
    public List<CT_Font> GetFonts() =>
        GetChildElements("Font").Select(e => new CT_Font(e)).ToList();
}
