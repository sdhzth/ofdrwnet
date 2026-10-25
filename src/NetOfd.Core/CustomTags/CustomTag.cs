using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.CustomTags;

/// <summary>
/// 自定义标引入口（CustomTag）。
/// <para>
/// 标引索引文件中通过 ID 引用被标引对象，发生“非接触式（分离式）”关联，
/// 标引内容可任意扩展，但建议给出扩展内容的规范约束文件（Schema）或命名空间。
/// </para>
/// 对应 Java: org.ofdrw.core.customTags.CustomTag（GB/T 33190-2016 第 16 章）
/// </summary>
public class CustomTag : OFDElement
{
    public CustomTag() : base("CustomTag") { }
    public CustomTag(XElement element) : base(element) { }

    /// <summary>自定义标引内容节点使用的类型标识（必填）</summary>
    public string TypeID
    {
        get => GetAttr("TypeID") ?? throw new InvalidOperationException("自定义标引内容节点使用的类型标识（TypeID）为空");
        set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("类型标识（TypeID）不能为空", nameof(value));
            SetAttr("TypeID", value);
        }
    }

    /// <summary>命名空间（可选，附录 A.9 CustomTags.xsd）</summary>
    public string? NameSpace
    {
        get => GetAttr("NameSpace");
        set => SetAttr("NameSpace", string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>指向自定义标引内容节点适用的 Schema 文件（可选）</summary>
    public STLoc? SchemaLoc
    {
        get
        {
            var v = GetChildText("SchemaLoc");
            return v is null ? null : new STLoc(v);
        }
        set
        {
            if (value is null) GetChildElement("SchemaLoc")?.Remove();
            else SetChildText("SchemaLoc", value.Value);
        }
    }

    /// <summary>指向自定义标引文件（必填）。这类文件中通过“非接触方式”引用版式内容流中的图元和相关信息。</summary>
    public STLoc FileLoc
    {
        get => GetChildText("FileLoc") is { } v ? new STLoc(v) : throw new InvalidOperationException("指向自定义标引文件（FileLoc）为空");
        set
        {
            if (value is null) throw new ArgumentException("指向自定义标引文件（FileLoc）为空", nameof(value));
            SetChildText("FileLoc", value.Value);
        }
    }
}
