using NetOfd.Core.BasicType;
using NetOfd.Reader.Model;
using System.Xml.Linq;

namespace NetOfd.Reader;

/// <summary>
/// 签名描述文件（Doc_0/Signs/Sign_N/Signature.xml）结构化解析器。
/// <para>
/// 对应 Java ofdrw-core <c>org.ofdrw.core.signatures.sig.Signature</c>（完整 SignedInfo 模型）。
/// 区别于 <see cref="OFDReader.GetDefaultSignatures"/>（仅返回 Signatures.xml 摘要条目）。
/// </para>
/// <para>
/// 本解析器为 OFDReader 用户便利层（非 Java OFDReader 公开 API）——拿到签名摘要
/// <see cref="SignatureInfo"/> 后，用本解析器拿到 SignedInfo 全量字段供校验/可视化/审计使用。
/// </para>
/// <para>容忍语义：缺字段返回 null/空集合，与 GetDefaultSignatures 风格一致。</para>
/// </summary>
public static class SignatureDescReader
{
    /// <summary>获取默认文档全部签名描述（按签名列表顺序）。</summary>
    public static List<SignatureDescEntity> GetDescriptions(OFDReader reader)
    {
        var res = new List<SignatureDescEntity>();
        if (!reader.HasSignature()) return res;
        var sigList = reader.GetDefaultSignatures();
        if (sigList is null || sigList.Count == 0) return res;
        foreach (var sig in sigList)
        {
            var desc = GetDescription(reader, sig);
            if (desc is not null) res.Add(desc);
        }
        return res;
    }

    /// <summary>按签名列表索引获取签名描述。</summary>
    public static SignatureDescEntity? GetDescription(OFDReader reader, int signatureIndex)
    {
        var sigList = reader.GetDefaultSignatures();
        if (sigList is null || signatureIndex < 0 || signatureIndex >= sigList.Count)
            return null;
        return GetDescription(reader, sigList[signatureIndex]);
    }

    /// <summary>按签名摘要条目获取签名描述。</summary>
    public static SignatureDescEntity? GetDescription(OFDReader reader, SignatureInfo signature)
    {
        if (string.IsNullOrEmpty(signature.BaseLoc)) return null;
        var rl = reader.GetResourceLocator();
        var baseLoc = new STLoc(signature.BaseLoc);
        XElement? root = rl.Get(baseLoc);
        if (root is null) return null;

        var signedValueLoc = ChildText(root, "SignedValue");
        XElement? signedInfoEl = Child(root, "SignedInfo");

        return new SignatureDescEntity
        {
            Id = root.Attribute("ID")?.Value,
            SignedValueLoc = signedValueLoc,
            SignedInfo = ParseSignedInfo(signedInfoEl)
        };
    }

    // ── SignedInfo ──

    private static SignedInfoSummary? ParseSignedInfo(XElement? el)
    {
        if (el is null) return null;
        var providerEl = Child(el, "Provider");
        var sealEl = Child(el, "Seal");
        var referencesEl = Child(el, "References");
        var stampAnnots = el.Elements()
            .Where(e => e.Name.LocalName == "StampAnnot")
            .Select(ParseStampAnnot)
            .ToList();
        var references = referencesEl is null
            ? new List<ReferenceSummary>()
            : referencesEl.Elements()
                .Where(e => e.Name.LocalName == "Reference")
                .Select(ParseReference)
                .ToList();
        return new SignedInfoSummary
        {
            Provider = ParseProvider(providerEl),
            SignatureMethod = ChildText(el, "SignatureMethod"),
            SignatureDateTime = ChildText(el, "SignatureDateTime"),
            SealLoc = ChildText(sealEl, "BaseLoc"),
            StampAnnots = stampAnnots,
            References = references,
            ReferencesCheckMethod = referencesEl?.Attribute("CheckMethod")?.Value
        };
    }

    private static ProviderSummary? ParseProvider(XElement? el)
    {
        if (el is null) return null;
        return new ProviderSummary
        {
            ProviderName = el.Attribute("ProviderName")?.Value,
            Company = el.Attribute("Company")?.Value,
            Version = el.Attribute("Version")?.Value
        };
    }

    private static StampAnnotSummary ParseStampAnnot(XElement el) => new()
    {
        Id = el.Attribute("ID")?.Value,
        PageRef = el.Attribute("PageRef")?.Value,
        Boundary = TryParseBox(el.Attribute("Boundary")?.Value, out var box, out var text) ? box : null,
        BoundaryText = text,
        Clip = el.Attribute("Clip")?.Value
    };

    private static ReferenceSummary ParseReference(XElement el)
    {
        string b64 = ChildText(el, "CheckValue") ?? string.Empty;
        byte[] raw = Array.Empty<byte>();
        if (b64.Length > 0)
        {
            try { raw = Convert.FromBase64String(b64); }
            catch { /* 非法 Base64 视为空字节，对齐容忍语义 */ }
        }
        return new ReferenceSummary
        {
            FileRef = el.Attribute("FileRef")?.Value ?? string.Empty,
            CheckValueBase64 = b64,
            CheckValue = raw
        };
    }

    // ── XML 辅助 ──

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string? ChildText(XElement? parent, string localName)
    {
        if (parent is null) return null;
        return Child(parent, localName)?.Value.Trim();
    }

    private static bool TryParseBox(string? raw, out STBox? box, out string? text)
    {
        box = null;
        text = raw;
        if (string.IsNullOrWhiteSpace(raw)) return false;
        var parts = raw.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 4) return false;
        if (!double.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var x) ||
            !double.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var y) ||
            !double.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ||
            !double.TryParse(parts[3], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h))
        {
            return false;
        }
        box = new STBox(x, y, w, h);
        text = null;
        return true;
    }
}