using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名扩展属性集（OFD 2.0 / GMT0099 表 B.2）。
/// 对应 Java: org.ofdrw.core.signatures.sig.Parameters
/// </summary>
public class Parameters : OFDElement
{
    public Parameters() : base("Parameters") { }

    public Parameters(XElement element) : base(element) { }

    /// <summary>
    /// 【必选 OFD 2.0】添加签名扩展属性。
    /// 同名属性已存在时<b>先移除旧的再写入</b>（后写覆盖，与 Java 版一致）。
    /// </summary>
    public Parameters AddParameter(Parameter parameter)
    {
        if (parameter is null) return this;

        string? name = parameter.Name;
        if (name is not null)
            GetParameter(name)?.Element.Remove();

        AddOFDEntity(parameter);
        return this;
    }

    /// <summary>【必选 OFD 2.0】按名称与值添加扩展属性。</summary>
    public Parameters AddParameter(string name, string value)
        => AddParameter(new Parameter(name, value));

    /// <summary>【必选 OFD 2.0】按名称、类型与值添加扩展属性。</summary>
    public Parameters AddParameter(string name, string? type, string value)
        => AddParameter(new Parameter(name, type, value));

    /// <summary>
    /// 按名称删除扩展属性。
    /// </summary>
    /// <returns>被删除的属性；不存在时返回 null</returns>
    public Parameter? RemoveParameter(string name)
    {
        if (name is null) return null;

        Parameter? old = GetParameter(name);
        old?.Element.Remove();
        return old;
    }

    /// <summary>按名称查找扩展属性，不存在返回 null。</summary>
    public Parameter? GetParameter(string name)
        => GetParameters().FirstOrDefault(p => name.Equals(p.Name, StringComparison.Ordinal));

    /// <summary>【必选 OFD 2.0】获取全部扩展属性。</summary>
    public List<Parameter> GetParameters()
        => GetChildElements("Parameter")
            .Select(e => new Parameter(e))
            .ToList();
}
