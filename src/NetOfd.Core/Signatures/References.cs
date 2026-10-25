using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名的范围（GB/T 33190-2016 §18.2.2 签名的范围 图 87 表 68）。
/// 一个受本次签名保护的包内文件对应一个 <see cref="Reference"/> 节点。
/// 对应 Java: org.ofdrw.core.signatures.range.References
/// </summary>
public class References : OFDElement
{
    public References() : base("References") { }

    public References(XElement element) : base(element) { }

    /// <summary>【可选 属性】摘要方法（原始字符串）。null 表示不写出该属性。</summary>
    public string? CheckMethod
    {
        get => GetAttr("CheckMethod");
        set => SetAttr("CheckMethod", value);
    }

    /// <summary>
    /// 【可选 属性】摘要方法的强类型视图；属性缺失或值为空时返回 null。
    /// <para>注意类内 <c>CheckMethod</c> 属性会遮蔽同名的
    /// <see cref="NetOfd.Core.Signatures.CheckMethod"/> 枚举，故此处必须写完全限定名
    /// （与增量 S 中 <c>Text.Weight</c> 同一处 C# 陷阱）。</para>
    /// </summary>
    public NetOfd.Core.Signatures.CheckMethod? CheckMethodKind
    {
        get
        {
            string? v = CheckMethod;
            return string.IsNullOrWhiteSpace(v)
                ? null
                : CheckMethodUtil.Parse(v);
        }
        set => CheckMethod = value?.ToString();
    }

    /// <summary>【必选】追加一个文件摘要节点（null 时忽略）。</summary>
    public References AddReference(Reference reference)
    {
        if (reference is null) return this;
        AddOFDEntity(reference);
        return this;
    }

    /// <summary>【必选】获取全部文件摘要节点。</summary>
    public List<Reference> GetReferences()
        => GetChildElements("Reference")
            .Select(e => new Reference(e))
            .ToList();

    /// <summary>检查是否已包含指定包内绝对路径的文件摘要。</summary>
    /// <param name="absLoc">文件绝对路径</param>
    public bool HasFile(string absLoc)
    {
        if (string.IsNullOrWhiteSpace(absLoc)) return false;
        return GetReferences()
            .Any(r => absLoc.Equals(r.FileRef?.Value, StringComparison.Ordinal));
    }
}
