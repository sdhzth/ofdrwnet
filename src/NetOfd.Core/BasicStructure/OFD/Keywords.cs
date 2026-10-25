using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Core.BasicStructure.OFD;

/// <summary>
/// 关键词集合，每一个关键词用一个 "Keyword" 子节点来表达（表 4 文档元数据属性）。
/// 对应 Java: org.ofdrw.core.basicStructure.ofd.docInfo.Keywords
/// <para>
/// Java 缺陷修正：Java 的 <c>getKeywords()</c> 读取的子元素名是 "Keywords"，
/// 而 <c>addKeyword()</c> 写入的是 "Keyword"，导致永远读不到已写入的关键词。
/// 本实现按写入名 "Keyword" 读取，保证可往返。
/// </para>
/// </summary>
public class Keywords : OFDElement
{
    public Keywords() : base("Keywords") { }
    public Keywords(XElement element) : base(element) { }

    /// <summary>【必选】增加关键字（子节点名 "Keyword"）</summary>
    public Keywords AddKeyword(string keyword)
    {
        var child = new XElement(OfdNs + "Keyword", keyword);
        _el.Add(child);
        return this;
    }

    /// <summary>获取关键字列表</summary>
    public List<string> GetKeywords() =>
        GetChildElements("Keyword")
            .Select(e => e.Value.Trim())
            .ToList();
}
