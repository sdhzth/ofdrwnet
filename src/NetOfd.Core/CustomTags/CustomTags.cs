using System.Xml.Linq;

namespace NetOfd.Core.CustomTags;

/// <summary>
/// 自定义标引列表（CustomTags.xml）。
/// <para>
/// 外部系统或用户可以添加自定义标记和信息，从而达到与其他系统、数据库
/// 进行交互的目的并扩展应用。一个文档可以带有多个自定义标引。
/// 入口点在文档根节点（Document）中定义。
/// </para>
/// 对应 Java: org.ofdrw.core.customTags.CustomTags（GB/T 33190-2016 第 16 章）
/// </summary>
public class CustomTags : OFDElement
{
    public CustomTags() : base("CustomTags") { }
    public CustomTags(XElement element) : base(element) { }

    /// <summary>增加自定义标引入口</summary>
    public CustomTags AddCustomTag(CustomTag customTag)
    {
        if (customTag is not null) AddOFDEntity(customTag);
        return this;
    }

    /// <summary>获取自定义标引入口列表</summary>
    public List<CustomTag> GetCustomTags()
        => GetChildElements("CustomTag").Select(e => new CustomTag(e)).ToList();
}
