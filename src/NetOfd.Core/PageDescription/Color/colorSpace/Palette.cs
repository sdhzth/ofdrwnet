using System.Xml.Linq;

namespace NetOfd.Core.PageDescription.Color.colorSpace;

/// <summary>
/// 调色板（GB/T 33190-2016 §8.3）。颜色索引从 0 开始。
/// 对应 Java: org.ofdrw.core.pageDescription.color.colorSpace.Palette
/// </summary>
public class Palette : OFDElement
{
    public Palette() : base("Palette") { }
    public Palette(XElement element) : base(element) { }

    /// <summary>追加一个预定义颜色。</summary>
    public Palette AddCV(CV cv)
    {
        _el.Add(cv.Element);
        return this;
    }

    /// <summary>按索引（0 起）获取预定义颜色，越界返回 null。</summary>
    public CV? GetCVByIndex(int index)
    {
        if (index < 0) return null;
        var cvs = GetChildElements("CV").ToList();
        return index >= cvs.Count ? null : new CV(cvs[index]);
    }

    /// <summary>按索引（0 起）获取预定义颜色的分量文本。</summary>
    public string? GetColorTextByIndex(int index) => GetCVByIndex(index)?.ColorText;

    /// <summary>获取全部预定义颜色。</summary>
    public List<CV> GetCVs() => GetChildElements("CV").Select(e => new CV(e)).ToList();
}
