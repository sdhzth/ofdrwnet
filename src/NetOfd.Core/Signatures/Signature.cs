using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名描述文件的根节点（Signs/Sign_N/Signature.xml）。
/// <para>
/// OFD 的数字签名通过对描述文件的保护间接实现对 OFD 原文的保护。签名结构中的
/// 签名信息（SignedInfo）是这一过程中的关键点，其中记录了当次数字签名保护的
/// 所有文件的二进制摘要信息，同时将安全算法提供者、签名算法、签名时间和所应用的
/// 安全印章等信息也包含在此节点内。签名描述文件同时包含了签名值将要存放的包内位置。
/// </para>
/// <para>GB/T 33190-2016 第 18.2.1 节（图 86 表 67）</para>
/// 对应 Java: org.ofdrw.core.signatures.sig.Signature
/// </summary>
/// <remarks>
/// <b>Java 有两个同名的 <c>Signature</c>，netofd 合并为一个</b>（与
/// <see cref="PageDescription.CTText"/> 合并 CT_Text + TextObject 的做法一致）：
/// <list type="bullet">
/// <item><description><c>org.ofdrw.core.signatures.Signature</c> —— Signatures.xml 中的
/// <b>注册项</b>，属性为 <c>ID</c> / <c>Type</c> / <c>BaseLoc</c> / <c>Relative</c>；</description></item>
/// <item><description><c>org.ofdrw.core.signatures.sig.Signature</c> —— Signature.xml 的
/// <b>根节点</b>，元素为 <c>SignedInfo</c> / <c>SignedValue</c>。</description></item>
/// </list>
/// 两者 XML 元素名都是 <c>Signature</c>，本类是它们的<b>属性并集</b>；
/// 读取时缺失的一侧返回 null，调用方按上下文取用即可。
/// <para>
/// <see cref="SignedInfo"/> 子树除 <see cref="XElement"/> 透传外，
/// 还可通过 <see cref="GetSignedInfo"/> 取到强类型视图（§18.2 整条类型链已于增量 Y 补齐）。
/// </para>
/// </remarks>
public class Signature : OFDElement
{
    /// <summary>SignedInfo 子元素名</summary>
    public const string SignedInfoElementName = "SignedInfo";

    public Signature() : base("Signature") { }

    public Signature(XElement element) : base(element) { }

    // ────────────────────────────────────────────────────────
    //  SignedInfo（必选）
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 【必选】签名要保护的原文及本次签名的相关信息（SignedInfo 子树）。
    /// 设为 null 表示移除该子树。
    /// </summary>
    public XElement? SignedInfo
    {
        get => GetChildElement(SignedInfoElementName);
        set
        {
            GetChildElement(SignedInfoElementName)?.Remove();
            if (value is not null) _el.Add(value);
        }
    }

    // ────────────────────────────────────────────────────────
    //  SignedInfo 便捷访问器（避免调用方裸写 XML）
    // ────────────────────────────────────────────────────────

    /// <summary>【SignedInfo/Provider】安全算法提供者的名称（不存在返回 null）。</summary>
    public string? ProviderName
    {
        get => SignedInfoChildText("Provider", "ProviderName");
        set => SetSignedInfoChildText("Provider", "ProviderName", value);
    }

    /// <summary>【SignedInfo/Provider】安全算法提供者的版本（不存在返回 null）。</summary>
    public string? ProviderVersion
    {
        get => SignedInfoChildText("Provider", "Version");
        set => SetSignedInfoChildText("Provider", "Version", value);
    }

    /// <summary>【SignedInfo/Method】签名算法标识（不存在返回 null）。</summary>
    public string? Method
    {
        get => SignedInfoChildText("Method", null);
        set => SetSignedInfoChildText("Method", null, value);
    }

    /// <summary>【SignedInfo/SignatureTime】签名时间（不存在返回 null）。</summary>
    public string? SignatureTime
    {
        get => SignedInfoChildText("SignatureTime", null);
        set => SetSignedInfoChildText("SignatureTime", null, value);
    }

    private string? SignedInfoChildText(string childName, string? grandChildName)
    {
        var child = SignedInfo?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == childName);
        if (child is null) return null;
        return grandChildName is null
            ? child.Value
            : child.Elements().FirstOrDefault(e => e.Name.LocalName == grandChildName)?.Value;
    }

    private void SetSignedInfoChildText(string childName, string? grandChildName, string? text)
    {
        var info = SignedInfo;
        if (info is null)
        {
            // 不自动创建 SignedInfo：它是必选节点，缺少应由调用方显式补齐，
            // 静默创建会掩盖"签名描述文件不完整"的问题。
            if (text is null) return;
            info = new XElement(OfdNs + SignedInfoElementName);
            _el.Add(info);
        }

        var child = info.Elements().FirstOrDefault(e => e.Name.LocalName == childName);
        if (text is null)
        {
            if (grandChildName is null) child?.Remove();
            else
                child?.Elements().FirstOrDefault(e => e.Name.LocalName == grandChildName)?.Remove();
            return;
        }

        if (child is null)
        {
            child = new XElement(OfdNs + childName);
            info.Add(child);
        }

        if (grandChildName is null)
        {
            child.Value = text;
        }
        else
        {
            var gc = child.Elements().FirstOrDefault(e => e.Name.LocalName == grandChildName);
            if (gc is null)
            {
                gc = new XElement(OfdNs + grandChildName);
                child.Add(gc);
            }
            gc.Value = text;
        }
    }

    // ────────────────────────────────────────────────────────
    //  SignedValue（必选）
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 【必选】指向安全签名提供者所返还的、针对签名描述文件计算所得的签名值文件（ST_Loc）。
    /// </summary>
    public STLoc? SignedValue
    {
        get
        {
            var el = GetChildElement("SignedValue");
            var v = el?.Value.Trim();
            return string.IsNullOrEmpty(v) ? null : new STLoc(v);
        }
        set => SetChildText("SignedValue", value?.Value);
    }

    // ────────────────────────────────────────────────────────
    //  注册项属性（Signatures.xml 中的 Signature 条目）
    // ────────────────────────────────────────────────────────

    /// <summary>【必选 属性】签名的唯一标识（推荐使用 “sNNN” 编码，NNN 从 1 开始）。</summary>
    public string? ID
    {
        get => GetAttr("ID");
        set => SetAttr("ID", value);
    }

    /// <summary>
    /// 【可选 属性】签名节点的类型。
    /// 属性缺失时返回 <see cref="SigType.Seal"/>（对齐 Java <c>SigType.getInstance("")</c> 的缺省语义）。
    /// </summary>
    public SigType Type
    {
        get => SigTypeUtil.Parse(GetAttr("Type"));
        set => SetAttr("Type", SigTypeUtil.ToXml(value));
    }

    /// <summary>【可选 属性 OFD 2.0】关联的前一次签名标识（联合署名链）。</summary>
    public string? Relative
    {
        get => GetAttr("Relative");
        set => SetAttr("Relative", value);
    }

    /// <summary>【必选 属性】指向具体签名描述文件（Signs/Sign_N/Signature.xml）的路径。</summary>
    public STLoc? BaseLoc
    {
        get
        {
            var v = GetAttr("BaseLoc");
            return v is null ? null : new STLoc(v);
        }
        set => SetAttr("BaseLoc", value?.Value);
    }

    // ────────────────────────────────────────────────────────
    //  SignedInfo 强类型桥接
    // ────────────────────────────────────────────────────────

    /// <summary>获取 SignedInfo 子树的强类型视图；子树不存在时返回 null。</summary>
    public NetOfd.Core.Signatures.SignedInfo? GetSignedInfo()
    {
        var el = SignedInfo;
        return el is null ? null : new NetOfd.Core.Signatures.SignedInfo(el);
    }

    /// <summary>以强类型 <see cref="NetOfd.Core.Signatures.SignedInfo"/> 覆盖子树（null 表示移除）。</summary>
    public void SetSignedInfo(NetOfd.Core.Signatures.SignedInfo? info)
        => SignedInfo = info?.Element;
}
