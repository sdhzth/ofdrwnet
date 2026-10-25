using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Archive;

/// <summary>
/// OFD XML 导航辅助：按 local-name 忽略命名空间（OFD 默认带命名空间，规则只需关心元素/属性名）。
/// </summary>
public static class OfdXml
{
    /// <summary>取所有 local-name 匹配的子元素。</summary>
    public static IEnumerable<XElement> LocalElems(this XElement e, string name)
        => e.Elements().Where(x => x.Name.LocalName == name);

    /// <summary>取第一个 local-name 匹配的子元素。</summary>
    public static XElement? LocalElem(this XElement e, string name)
        => e.Elements().FirstOrDefault(x => x.Name.LocalName == name);

    /// <summary>取所有 local-name 匹配的子孙元素（含深层）。</summary>
    public static IEnumerable<XElement> LocalDescendants(this XElement e, string name)
        => e.Descendants().Where(x => x.Name.LocalName == name);

    /// <summary>取属性（OFD 属性均无命名空间）。</summary>
    public static string? Attr(this XElement e, string name)
        => e.Attribute(name)?.Value;

    /// <summary>取属性并 trim；不存在返回 null。</summary>
    public static string? AttrTrim(this XElement e, string name)
    {
        var v = e.Attribute(name)?.Value;
        return v == null ? null : v.Trim();
    }

    /// <summary>
    /// 取 ST_Loc 型取值：优先属性形式，其次同名子元素文本。
    /// <para>OFD 实践中 MediaFile/FontFile/FileLoc 等既有属性写法也有子元素写法，需同时兼容。</para>
    /// </summary>
    public static string? LocValue(this XElement e, string name)
    {
        var a = e.AttrTrim(name);
        if (!string.IsNullOrEmpty(a)) return a;
        var c = e.LocalElem(name)?.Value.Trim();
        return string.IsNullOrEmpty(c) ? null : c;
    }

    /// <summary>写 ST_Loc 型取值：保持原承载形式（已有子元素则改子元素，否则写属性）。</summary>
    public static void SetLocValue(this XElement e, string name, string value)
    {
        var child = e.LocalElem(name);
        if (child != null) { child.Value = value; return; }
        e.SetAttributeValue(name, value);
    }

    /// <summary>
    /// 取「最外层」同名子孙元素：local-name 匹配且在 root 之内不存在同名祖先。
    /// <para>用于 PageBlock 这类可自嵌套的结构：无论它挂在 Page 下还是 Content/Layer 下，都能定位到嵌套链的根。</para>
    /// </summary>
    public static IEnumerable<XElement> TopLevelLocal(this XElement root, string name)
        => root.Descendants().Where(x => x.Name.LocalName == name
            && !x.Ancestors().TakeWhile(a => a != root).Any(a => a.Name.LocalName == name));
}
