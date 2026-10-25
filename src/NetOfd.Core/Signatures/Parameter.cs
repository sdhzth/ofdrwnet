using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名扩展属性（OFD 2.0 / GMT0099 表 B.2）。
/// 记录一个属性的“键”（Name 属性）与“值”（记录在节点内容中）。
/// 对应 Java: org.ofdrw.core.signatures.sig.Parameter
/// </summary>
public class Parameter : OFDElement
{
    public Parameter() : base("Parameter") { }

    public Parameter(XElement element) : base(element) { }

    /// <summary>以名称与值创建。</summary>
    public Parameter(string name, string value) : this()
    {
        Name = name;
        Value = value;
    }

    /// <summary>以名称、类型与值创建。</summary>
    public Parameter(string name, string? type, string value) : this()
    {
        Name = name;
        Type = type;
        Value = value;
    }

    /// <summary>【必选 属性】扩展属性的名称。</summary>
    public string? Name
    {
        get => GetAttr("Name");
        set => SetAttr("Name", value);
    }

    /// <summary>【可选 属性】扩展属性的值类型。</summary>
    public string? Type
    {
        get => GetAttr("Type");
        set => SetAttr("Type", value);
    }

    /// <summary>
    /// 【可选】扩展属性的值，即元素自身的文本内容。
    /// <para>注意不要走 <c>OFDElement.SetText()</c> —— 该方法会连属性一起删除；
    /// 这里直接写 <see cref="OFDElement.Element"/> 的 <c>Value</c>，只替换文本节点。</para>
    /// </summary>
    public string? Value
    {
        get => Element.Value;
        set => Element.Value = value ?? "";
    }
}
