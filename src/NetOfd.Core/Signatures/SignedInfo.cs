using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名要保护的原文及本次签名相关的信息（GB/T 33190-2016 §18.2.1 图 86 表 67；GMT0099 B.2 图 B.2）。
/// 对应 Java: org.ofdrw.core.signatures.sig.SignedInfo
/// </summary>
/// <remarks>
/// 本类补齐了增量 V 有意留下的口子 —— 当时为避免在 Core 引入整条签名类型链，
/// <see cref="NetOfd.Core.Signatures.Signature"/> 的 <c>SignedInfo</c> 以 <see cref="XElement"/> 透传。
/// 现在整条链（Provider / Parameters / References / StampAnnot / Seal）已齐备，
/// 可通过 <see cref="Signature.GetSignedInfo"/> 取得强类型视图。
/// <para>
/// 类内 <c>Provider</c> / <c>Seal</c> / <c>Parameters</c> / <c>References</c> 属性会遮蔽同名的
/// 类型名，故 <c>new</c> 表达式一律写完全限定名（增量 S 的 <c>Text.Weight</c> 同款 C# 陷阱）。
/// </para>
/// </remarks>
public class SignedInfo : OFDElement
{
    public SignedInfo() : base("SignedInfo") { }

    public SignedInfo(XElement element) : base(element) { }

    // ────────────────────────────────────────────────────────
    //  Provider（必选）
    // ────────────────────────────────────────────────────────

    /// <summary>【必选】创建签名时所用的签章组件提供者信息；设为 null 表示移除。</summary>
    public NetOfd.Core.Signatures.Provider? Provider
    {
        get
        {
            XElement? e = GetChildElement("Provider");
            return e is null ? null : new NetOfd.Core.Signatures.Provider(e);
        }
        set
        {
            if (value is null)
            {
                GetChildElement("Provider")?.Remove();
                return;
            }
            SetOFDEntity(value);
        }
    }

    // ────────────────────────────────────────────────────────
    //  SignatureMethod / SignatureDateTime（可选）
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 【可选】签名方法（签名算法标识）。记录安全模块返回的签名算法代码，便于验证时使用。
    /// </summary>
    public string? SignatureMethod
    {
        get => GetChildText("SignatureMethod");
        set => SetTextChild("SignatureMethod", value);
    }

    /// <summary>
    /// 【可选】签名时间。
    /// <para>元素名为 <c>SignatureDateTime</c>（GB/T 33190 表 67 的写法，与 Java SignedInfo 一致）。
    /// 注意 <see cref="Signature"/> 的 <c>SignatureTime</c> 便捷访问器用的是 <c>SignatureTime</c>
    /// 元素名（SES 侧既有写法），两者不是同一个元素，勿混淆。</para>
    /// </summary>
    public string? SignatureDateTime
    {
        get => GetChildText("SignatureDateTime");
        set => SetTextChild("SignatureDateTime", value);
    }

    // ────────────────────────────────────────────────────────
    //  Parameters（可选 OFD 2.0）
    // ────────────────────────────────────────────────────────

    /// <summary>【可选 OFD 2.0】扩展签名属性集；设为 null 表示移除。</summary>
    public NetOfd.Core.Signatures.Parameters? Parameters
    {
        get
        {
            XElement? e = GetChildElement("Parameters");
            return e is null ? null : new NetOfd.Core.Signatures.Parameters(e);
        }
        set
        {
            if (value is null)
            {
                GetChildElement("Parameters")?.Remove();
                return;
            }
            SetOFDEntity(value);
        }
    }

    // ────────────────────────────────────────────────────────
    //  References（必选）
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 【必选】包内文件计算所得的摘要记录列表；设为 null 表示移除。
    /// 缺失时返回 null —— Java 版此处抛 <c>IllegalArgumentException</c>。
    /// </summary>
    public NetOfd.Core.Signatures.References? References
    {
        get
        {
            XElement? e = GetChildElement("References");
            return e is null ? null : new NetOfd.Core.Signatures.References(e);
        }
        set
        {
            if (value is null)
            {
                GetChildElement("References")?.Remove();
                return;
            }
            SetOFDEntity(value);
        }
    }

    // ────────────────────────────────────────────────────────
    //  StampAnnot（可选，可多次）
    // ────────────────────────────────────────────────────────

    /// <summary>【可选】追加本签名关联的外观（节点可出现多次）。</summary>
    public SignedInfo AddStampAnnot(NetOfd.Core.Signatures.StampAnnot stampAnnot)
    {
        if (stampAnnot is null) return this;
        AddOFDEntity(stampAnnot);
        return this;
    }

    /// <summary>【可选】获取本签名关联的全部外观。</summary>
    public List<NetOfd.Core.Signatures.StampAnnot> GetStampAnnots()
        => GetChildElements("StampAnnot")
            .Select(e => new NetOfd.Core.Signatures.StampAnnot(e))
            .ToList();

    // ────────────────────────────────────────────────────────
    //  Seal（可选）
    // ────────────────────────────────────────────────────────

    /// <summary>【可选】电子印章信息；设为 null 表示移除。</summary>
    public NetOfd.Core.Signatures.Seal? Seal
    {
        get
        {
            XElement? e = GetChildElement("Seal");
            return e is null ? null : new NetOfd.Core.Signatures.Seal(e);
        }
        set
        {
            if (value is null)
            {
                GetChildElement("Seal")?.Remove();
                return;
            }
            SetOFDEntity(value);
        }
    }

    // ── 内部 ────────────────────────────────────────────────
    private void SetTextChild(string name, string? value)
    {
        if (value is null)
        {
            GetChildElement(name)?.Remove();
            return;
        }
        SetChildText(name, value);
    }
}
