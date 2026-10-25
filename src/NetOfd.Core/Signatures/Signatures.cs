using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名列表根节点（Signs/Signatures.xml）。
/// <para>
/// 签名列表文件的入口点在 7.4 主入口（OFD.xml 的 DocBody/Signatures）中定义。
/// 签名列表文件中可以包含多个签名（例如联合发文等情况）。
/// 当允许下次继续添加签名时，该文件不会被包含到本次签名的保护文件列表（References）中。
/// </para>
/// <para>GB/T 33190-2016 第 18.1 节（图 85 表 66）</para>
/// 对应 Java: org.ofdrw.core.signatures.Signatures
/// </summary>
public class Signatures : OFDElement
{
    public Signatures() : base("Signatures") { }

    public Signatures(XElement element) : base(element) { }

    /// <summary>
    /// 【可选】安全标识的最大值。
    /// <para>
    /// 作用与文档入口文件 Document.xml 中的 MaxID 相同；
    /// 为了避免在签名时影响文档入口文件，采用了与 ST_ID 不一样的 ID 编码方式。
    /// 推荐使用 "sNNN" 的编码方式，NNN 从 1 开始。
    /// </para>
    /// <para>设为 null 表示移除该元素（对齐 Java <c>setMaxSignId(null)</c>）。</para>
    /// </summary>
    public string? MaxSignId
    {
        get => GetChildText("MaxSignId");
        set => SetChildText("MaxSignId", value);
    }

    /// <summary>【可选】增加一条数字签名或安全签章在列表中的注册信息（null 时忽略）。</summary>
    public Signatures AddSignature(Signature signature)
    {
        if (signature is null) return this;
        _el.Add(signature.Element);
        return this;
    }

    /// <summary>【可选】获取全部签名注册信息（无签名时返回空列表）。</summary>
    public List<Signature> GetSignatures()
    {
        return GetChildElements("Signature")
            .Select(e => new Signature(e))
            .ToList();
    }
}
