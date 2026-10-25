using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Core.BasicStructure.OFD;

/// <summary>
/// 用户自定义元数据集合，其子节点为 CustomData。
/// 对应 Java: org.ofdrw.core.basicStructure.ofd.docInfo.CustomDatas
/// </summary>
public class CustomDatas : OFDElement
{
    public CustomDatas() : base("CustomDatas") { }
    public CustomDatas(XElement element) : base(element) { }

    /// <summary>增加用户自定义元数据</summary>
    public CustomDatas AddCustomData(string name, string value)
        => AddCustomData(new CustomData(name, value));

    /// <summary>增加用户自定义元数据</summary>
    public CustomDatas AddCustomData(CustomData customData)
    {
        if (customData is not null) AddOFDEntity(customData);
        return this;
    }

    /// <summary>获取自定义元数据集合</summary>
    public List<CustomData> GetCustomDatas() =>
        GetChildElements("CustomData").Select(e => new CustomData(e)).ToList();

    /// <summary>按名称获取用户自定义元数据值，不存在时返回 null。</summary>
    public string? GetCustomDataValue(string name)
    {
        foreach (var item in GetCustomDatas())
            if (name == item.DataName)
                return item.Value;
        return null;
    }
}
