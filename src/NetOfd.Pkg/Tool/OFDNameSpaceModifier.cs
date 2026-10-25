using System.Xml.Linq;
using NetOfd.Core;

namespace NetOfd.Pkg.Tool;

/// <summary>
/// 命名空间升级器：把元素及其所有后代的命名空间统一升级为 OFD 命名空间。
/// <para>
/// 与 <c>NetOfd.Reader.Tools.NameSpaceCleaner</c> 正好互补：
/// Cleaner 是「降级」——把第三方非标准命名空间清成无命名空间，便于宽松读取；
/// Modifier 是「升级」——把无命名空间（或错误命名空间）的元素补回标准 OFD
/// 命名空间，便于写回合规文件。
/// </para>
/// <para>
/// 典型场景：部分第三方工具生成的 OFD 不带命名空间，
/// 靠 <see cref="NetOfd.Core.OFDElement.NSStrictMode"/> 宽松模式可以读进来，
/// 但直接写回就会输出没有 <c>xmlns</c> 的 XML（不合规）。写回前先过一遍本工具即可。
/// </para>
/// 对应 Java: org.ofdrw.pkg.tool.OFDNameSpaceModifier（Java 基于 dom4j VisitorSupport，
/// 本实现基于 LINQ to XML，语义等价）。
/// </summary>
public sealed class OFDNameSpaceModifier
{
    /// <summary>期望变更到的目标命名空间（默认 OFD 标准命名空间）</summary>
    public XNamespace ExpectNs { get; }

    /// <param name="namespace">目标命名空间；为 null 时使用 OFD 标准命名空间</param>
    public OFDNameSpaceModifier(XNamespace? @namespace = null)
    {
        ExpectNs = @namespace ?? OFDElement.OfdNs;
    }

    /// <summary>升级整个文档（含根元素及其所有后代）。</summary>
    public void Upgrade(XDocument? document)
    {
        if (document?.Root is null) return;
        Upgrade(document.Root);
    }

    /// <summary>
    /// 就地升级指定元素及其所有后代元素的命名空间。
    /// 已处于目标命名空间的元素保持不变（幂等）。
    /// </summary>
    public void Upgrade(XElement? element)
    {
        if (element is null) return;

        // 1. 先改元素名（含后代），让命名空间声明自然落到根元素上
        foreach (var e in element.DescendantsAndSelf().ToList())
        {
            if (e.Name.Namespace != ExpectNs)
                e.Name = ExpectNs + e.Name.LocalName;
        }

        // 2. 清掉多余的命名空间声明（第三方残留的 xmlns / xmlns:xxx），
        //    仅保留目标命名空间的那一条
        foreach (var e in element.DescendantsAndSelf().ToList())
        {
            var declar = e.Attributes()
                .Where(a => a.IsNamespaceDeclaration)
                .ToList();

            foreach (var d in declar)
            {
                bool isTarget = d.Value == ExpectNs.NamespaceName;
                // 根元素上的目标声明保留（其余位置的重复声明由 LINQ to XML 自动去重）
                if (!isTarget || !ReferenceEquals(e, element))
                    d.Remove();
            }
        }

        // 3. 属性名上的命名空间一并去掉（OFD 的属性均为无命名空间属性）
        foreach (var e in element.DescendantsAndSelf().ToList())
        {
            foreach (var a in e.Attributes().ToList())
            {
                if (a.Name.Namespace == XNamespace.None) continue;
                var value = a.Value;
                a.Remove();
                e.SetAttributeValue(a.Name.LocalName, value);
            }
        }
    }
}
