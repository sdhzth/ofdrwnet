using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 扩展参数节点中的单个参数（名称/值对）。
/// 对应 Java: org.ofdrw.core.crypto.encryt.Parameter
/// </summary>
public class Parameter : OFDElement
{
    public Parameter() : base("Parameter") { }
    public Parameter(XElement element) : base(element) { }
    public Parameter(string name, string value) : base("Parameter")
    {
        SetAttr("Name", name);
        SetAttr("Value", value);
    }

    public string? GetAttrName() => GetAttr("Name");
    public string? GetValue() => GetAttr("Value");
}

/// <summary>
/// 扩展参数节点（GM/T 0099-2020 附录 C 图 C.1）。
/// 对应 Java: org.ofdrw.core.crypto.encryt.ExtendParams
/// </summary>
public class ExtendParams : OFDElement
{
    public ExtendParams() : base("ExtendParams") { }
    public ExtendParams(XElement element) : base(element) { }

    public ExtendParams AddParameter(Parameter parameter)
    {
        if (parameter is not null) AddOFDEntity(parameter);
        return this;
    }

    public ExtendParams AddParameter(string name, string value) =>
        AddParameter(new Parameter(name, value));

    public System.Collections.Generic.List<Parameter> GetParameters() =>
        GetChildElements("Parameter").Select(e => new Parameter(e)).ToList();

    public string? GetValue(string name) =>
        GetParameters().FirstOrDefault(p => p.GetAttrName() == name)?.GetValue();
}
