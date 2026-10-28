using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NetOfd.Sign;

/// <summary>
/// OFD 签名相关 XML 构件构建（参考 ofdrw-core 的 signatures 模型）。
/// <para>命名空间：http://www.ofdspec.org/2016</para>
/// </summary>
public static class SignatureModels
{
    public static readonly XNamespace Ns = "http://www.ofdspec.org/2016";

    /// <summary>单个受保护文件的摘要记录。</summary>
    public sealed class ReferenceItem
    {
        public string FileRef { get; init; } = "";
        public string CheckValue { get; init; } = ""; // base64(SM3)
    }

    /// <summary>
    /// 签章外观注释（OFD 标准 18.2.3 的 <b>StampAnnot</b>）。
    /// <para>
    /// 数字签名的外观以“OFD 中的注解（注释）”形式表达，而非把印章烤进页 Content。
    /// 参考 ofdrw-core 的 <c>StampAnnot</c>：作为 <c>SignedInfo</c> 的子元素存在。
    /// </para>
    /// </summary>
    /// <remarks>
    /// 属性均为字符串，格式对齐 OFD 规范：
    /// <list type="bullet">
    ///   <item><description>Id：签章注释标识，推荐 “sNNN”（NNN 从 1 开始）。</description></item>
    ///   <item><description>PageRef：外观注释所在页面的标识符（对应 Page 的 ID）。</description></item>
    ///   <item><description>Boundary：外观边框位置，ST_Box 文本 “x y w h”。</description></item>
    ///   <item><description>Clip：可选，外观裁剪框，ST_Box 文本 “x y w h”。</description></item>
    /// </list>
    /// </remarks>
    public sealed class StampAnnot
    {
        public string Id { get; init; } = "";
        public string PageRef { get; init; } = "";
        public string Boundary { get; init; } = "";
        public string? Clip { get; init; }
    }

    /// <summary>将 <see cref="StampAnnot"/> 序列化为 <c>StampAnnot</c> XElement。</summary>
    public static XElement BuildStampAnnotElement(StampAnnot a)
    {
        var content = new List<object>
        {
            new XAttribute("ID", a.Id),
            new XAttribute("PageRef", a.PageRef),
            new XAttribute("Boundary", a.Boundary)
        };
        if (a.Clip is { } clip)
            content.Add(new XAttribute("Clip", clip));
        return new XElement(Ns + "StampAnnot", content);
    }

    /// <summary>将 XElement 序列化为 UTF-8（无 BOM）且带 XML 声明的字节。</summary>
    public static byte[] ToUtf8Bytes(XElement root)
    {
        var doc = new XDocument(root);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings))
            doc.Save(w);
        // 还原元素前缀为 ofd:（GB/T 33190-2016 §7.1「标识符宜为 ofd」）。
        // 输入 XML 若已带 xmlns:defaults 声明，LINQ-to-XML 序列化会把元素前缀
        // 分配给 defaults:（实测 <ofd:OFD> → <defaults:OFD>），故在此文本层还原。
        return NetOfd.Core.OFDElement.RestoreOfdPrefixBytes(ms.ToArray());
    }

    /// <summary>构建签名列表文件（Signatures.xml）字节。</summary>
    public static byte[] BuildSignaturesListBytes(string signId, string sigType, string baseLoc, string maxSignId)
        => BuildSignaturesListBytes(
            new List<(string Id, string Type, string BaseLoc, string? Relative)>
            {
                (signId, sigType, baseLoc, null)
            }, maxSignId);

    /// <summary>构建签名列表文件（Signatures.xml）字节（含已有记录，用于追加签名）。</summary>
    /// <param name="records">签名记录（含相对 ID）。</param>
    /// <param name="maxSignId">当前最大签名 ID。</param>
    public static byte[] BuildSignaturesListBytes(
        IReadOnlyList<(string Id, string Type, string BaseLoc, string? Relative)> records, string maxSignId)
    {
        var root = new XElement(Ns + "Signatures");
        foreach (var (id, type, baseLoc, relative) in records)
        {
            var content = new List<object>
            {
                new XAttribute("ID", id),
                new XAttribute("Type", type),
                new XAttribute("BaseLoc", baseLoc)
            };
            if (!string.IsNullOrEmpty(relative))
                content.Add(new XAttribute("Relative", relative));
            root.Add(new XElement(Ns + "Signature", content));
        }
        root.Add(new XElement(Ns + "MaxSignId", maxSignId));
        return ToUtf8Bytes(root);
    }

    /// <summary>构建签名描述文件（Sign_N/Signature.xml）字节。</summary>
    public static byte[] BuildSignatureDescriptionBytes(
        string providerName, string company, string version,
        string signatureMethod, string signatureDateTime,
        IReadOnlyList<ReferenceItem> references, string signedValueLoc,
        IReadOnlyList<StampAnnot>? stampAnnots = null,
        string? sealLoc = null)
    {
        var refsEl = new XElement(Ns + "References", new XAttribute("CheckMethod", "SM3"));
        foreach (var r in references)
            refsEl.Add(new XElement(Ns + "Reference",
                new XAttribute("FileRef", r.FileRef),
                new XElement(Ns + "CheckValue", r.CheckValue)));

        var signedInfo = new XElement(Ns + "SignedInfo",
            new XElement(Ns + "Provider",
                new XAttribute("ProviderName", providerName),
                new XAttribute("Company", company),
                new XAttribute("Version", version)),
            new XElement(Ns + "SignatureMethod", signatureMethod),
            new XElement(Ns + "SignatureDateTime", signatureDateTime),
            refsEl);

        // 电子印章引用（签章模式下签名描述文件应引用 Seal.esl）
        if (!string.IsNullOrEmpty(sealLoc))
            signedInfo.Add(new XElement(Ns + "Seal", new XAttribute("BaseLoc", sealLoc)));

        // 签章外观以“OFD 注解（注释）”形式写入 SignedInfo（参考 ofdrw-core StampAnnot）。
        // 不写入页 Content，从而保持签名内容“干净”、可独立校验。
        if (stampAnnots is { Count: > 0 } list)
            foreach (var a in list)
                signedInfo.Add(BuildStampAnnotElement(a));

        var root = new XElement(Ns + "Signature", signedInfo,
            new XElement(Ns + "SignedValue", signedValueLoc));
        return ToUtf8Bytes(root);
    }

    /// <summary>在 OFD.xml 的 DocBody 下写入/更新 Signatures 入口（Loc 属性）。</summary>
    public static byte[] UpdateOfdXmlBytes(byte[] ofdXml, string signaturesLoc)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(ofdXml));
        var ofd = doc.Root;
        var docBody = ofd?.Element(Ns + "DocBody");
        if (docBody == null)
        {
            docBody = new XElement(Ns + "DocBody");
            ofd?.Add(docBody);
        }
        docBody.Elements(Ns + "Signatures").Remove();
        docBody.Add(new XElement(Ns + "Signatures", new XAttribute("Loc", signaturesLoc)));
        return ToUtf8Bytes(doc.Root!);
    }

    /// <summary>从 OFD.xml 字节中移除 DocBody 下的 Signatures 入口。</summary>
    public static byte[] RemoveSignaturesOfdXmlBytes(byte[] ofdXml)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(ofdXml));
        doc.Root?.Element(Ns + "DocBody")?.Elements(Ns + "Signatures").Remove();
        return ToUtf8Bytes(doc.Root!);
    }

    /// <summary>从签名描述文件字节中解析签名值文件位置（SignedValue 元素文本）。</summary>
    public static string? ParseSignedValueLoc(byte[] descriptionBytes)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(descriptionBytes));
        return doc.Root?.Element(Ns + "SignedValue")?.Value?.Trim();
    }

    /// <summary>从签名描述文件字节中解析 SignedInfo/Seal 的 BaseLoc（电子印章文件位置，可选）。</summary>
    public static string? ParseSealBaseLoc(byte[] descriptionBytes)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(descriptionBytes));
        return doc.Root?.Element(Ns + "SignedInfo")?.Element(Ns + "Seal")?.Attribute("BaseLoc")?.Value?.Trim();
    }

    /// <summary>从签名描述文件字节中解析 References（FileRef → CheckValue(base64)）。</summary>
    public static List<ReferenceItem> ParseReferences(byte[] descriptionBytes)
    {
        var result = new List<ReferenceItem>();
        var doc = XDocument.Parse(Encoding.UTF8.GetString(descriptionBytes));
        var refsEl = doc.Root?.Element(Ns + "SignedInfo")?.Element(Ns + "References");
        if (refsEl == null) return result;
        foreach (var r in refsEl.Elements(Ns + "Reference"))
        {
            var fileRef = r.Attribute("FileRef")?.Value ?? "";
            var checkValue = r.Element(Ns + "CheckValue")?.Value ?? "";
            result.Add(new ReferenceItem { FileRef = fileRef, CheckValue = checkValue });
        }
        return result;
    }

    /// <summary>解析签名列表文件中的 MaxSignId 元素。</summary>
    public static string? ParseMaxSignId(byte[] listBytes)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(listBytes));
        return doc.Root?.Element(Ns + "MaxSignId")?.Value?.Trim();
    }

    /// <summary>从 OFD.xml 字节中解析 DocBody/Signatures 的 Loc（签名列表文件位置）。</summary>
    public static string? ParseSignaturesLoc(byte[] ofdXml)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(ofdXml));
        return doc.Root?.Element(Ns + "DocBody")?.Element(Ns + "Signatures")?.Attribute("Loc")?.Value?.Trim();
    }

    /// <summary>解析签名列表文件字节，返回 Signature 记录（ID, Type, BaseLoc, Relative）。</summary>
    public static List<(string Id, string Type, string BaseLoc, string? Relative)> ParseSignatureRecords(byte[] listBytes)
    {
        var result = new List<(string, string, string, string?)>();
        var doc = XDocument.Parse(Encoding.UTF8.GetString(listBytes));
        foreach (var s in doc.Root?.Elements(Ns + "Signature") ?? Enumerable.Empty<XElement>())
        {
            var id = s.Attribute("ID")?.Value ?? "";
            var type = s.Attribute("Type")?.Value ?? "Sign";
            var baseLoc = s.Attribute("BaseLoc")?.Value ?? "";
            var relative = s.Attribute("Relative")?.Value;
            result.Add((id, type, baseLoc, relative));
        }
        return result;
    }
}
