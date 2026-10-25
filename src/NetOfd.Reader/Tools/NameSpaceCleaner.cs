using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Reader.Tools;

/// <summary>
/// XML 命名空间清理工具：把元素/属性上的命名空间声明与前缀全部去除，
/// 得到"无命名空间"的等价文档。
/// <para>
/// 对应 Java: org.ofdrw.reader.tools.NameSpaceCleaner（Java 基于 dom4j VisitorSupport，
/// 本实现基于 LINQ to XML，语义等价）。
/// </para>
/// <para>
/// 用途：部分第三方工具生成的 OFD 使用了非标准/错误的命名空间，
/// 直接按 <c>ofd:</c> 前缀查找会全部落空；先清理命名空间再用宽松模式解析可提升兼容性
/// （参见 <see cref="NetOfd.Core.OFDElement.NSStrictMode"/>）。
/// </para>
/// </summary>
public static class NameSpaceCleaner
{
    /// <summary>清理整个文档（含根元素）的命名空间。</summary>
    public static void Clean(XDocument document)
    {
        if (document?.Root is null) return;
        Clean(document.Root);
    }

    /// <summary>就地清理指定元素及其所有后代元素、属性的命名空间。</summary>
    public static void Clean(XElement element)
    {
        if (element is null) return;

        foreach (var e in element.DescendantsAndSelf().ToList())
        {
            // 1. 去掉元素自身的命名空间（保留本地名）
            if (e.Name.Namespace != XNamespace.None)
                e.Name = e.Name.LocalName;

            // 2. 移除 xmlns / xmlns:xxx 命名空间声明
            var decls = e.Attributes().Where(a => a.IsNamespaceDeclaration).ToList();
            foreach (var d in decls) d.Remove();

            // 3. 去掉属性上的命名空间（保留本地名）
            foreach (var a in e.Attributes().ToList())
            {
                if (a.Name.Namespace != XNamespace.None)
                {
                    var value = a.Value;
                    a.Remove();
                    e.SetAttributeValue(a.Name.LocalName, value);
                }
            }
        }
    }
}
