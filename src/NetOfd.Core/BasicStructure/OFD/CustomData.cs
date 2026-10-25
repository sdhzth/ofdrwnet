using System.Xml.Linq;

namespace NetOfd.Core.BasicStructure.OFD;

/// <summary>
/// 用户自定义元数据，可以指定一个名称及其对应的值。
/// 对应 Java: org.ofdrw.core.basicStructure.ofd.docInfo.CustomData
/// </summary>
public class CustomData : OFDElement
{
    public CustomData(XElement element) : base(element) { }

    /// <summary>自定元数据</summary>
    /// <param name="name">元数据名称（Name 属性）</param>
    /// <param name="value">元数据值（元素文本）</param>
    public CustomData(string name, string value) : base("CustomData")
    {
        DataName = name;
        Value = value;
    }

    /// <summary>【必选 属性】元数据名称（Name）</summary>
    public string? DataName
    {
        get => GetAttr("Name");
        set => SetAttr("Name", value);
    }

    /// <summary>
    /// 元数据值（元素文本）。
    /// <para>
    /// 注意：不能直接复用基类的 <c>SetText</c>——它会 <c>RemoveAll()</c>，
    /// 把必选的 Name 属性一并删除；此处只替换文本节点、保留属性。
    /// </para>
    /// </summary>
    public string? Value
    {
        get => GetText();
        set
        {
            _el.Nodes().Remove();
            if (value is not null) _el.Add(value);
        }
    }
}
