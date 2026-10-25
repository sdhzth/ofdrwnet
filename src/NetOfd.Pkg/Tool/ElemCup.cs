using System.Text;
using System.Xml.Linq;

namespace NetOfd.Pkg.Tool;

/// <summary>
/// 元素杯子 —— OFD XML 的统一序列化 / 反序列化工具。
/// <para>
/// 「注入（Inject）」= 反序列化：把文件或流读成 <see cref="XElement"/>；
/// 「倒出（Dump）」= 序列化：把 <see cref="XElement"/> 写成文件、字节序列或字符串。
/// </para>
/// <para>
/// 对应 Java: org.ofdrw.pkg.tool.ElemCup。
/// </para>
/// </summary>
/// <remarks>
/// <b>与 Java 版的一处有意差异</b>：Java 依赖 <c>SAXReaderFactory</c> 显式关闭
/// DOCTYPE / 外部实体来防 XXE；.NET 的 <see cref="XElement.Load(Stream)"/> 默认即
/// <c>DtdProcessing.Prohibit</c> 且 <c>XmlResolver</c> 为空，天然免疫，
/// 因此本实现不再需要（也不提供）对应的读取器工厂。
/// </remarks>
public static class ElemCup
{
    /// <summary>OFD 标准 XML 声明（UTF-8、无 BOM 写出）</summary>
    public const string XmlDeclaration = "<?xml version=\"1.0\" encoding=\"UTF-8\"?>";

    /// <summary>
    /// 是否开启调试输出：true 时 XML 带缩进换行（便于人工阅读），
    /// false 时为紧凑单行（默认，与 Java <c>ENABLE_DEBUG_PRINT = false</c> 一致）。
    /// </summary>
    public static bool EnableDebugPrint { get; set; }

    // ────────────────────────────────────────────────────────
    //  注入（反序列化）
    // ────────────────────────────────────────────────────────

    /// <summary>从文件加载根元素（文件不存在返回 null）。</summary>
    public static XElement? Inject(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        return XElement.Load(path);
    }

    /// <summary>从流加载根元素。</summary>
    public static XElement? Inject(Stream? stream)
    {
        if (stream is null) return null;
        return XElement.Load(stream);
    }

    // ────────────────────────────────────────────────────────
    //  倒出（序列化）
    // ────────────────────────────────────────────────────────

    /// <summary>序列化元素为 XML 字符串（含 XML 声明）；元素为 null 时返回 null。</summary>
    public static string? ToXml(XElement? element)
    {
        if (element is null) return null;
        var options = EnableDebugPrint ? SaveOptions.None : SaveOptions.DisableFormatting;
        return XmlDeclaration + element.ToString(options);
    }

    /// <summary>
    /// 序列化元素为 UTF-8 字节序列（含 XML 声明）。
    /// 对应 Java: <c>ElemCup.dump(Element)</c>
    /// </summary>
    public static byte[]? Dump(XElement? element)
    {
        var xml = ToXml(element);
        return xml is null ? null : new UTF8Encoding(false).GetBytes(xml);
    }

    /// <summary>
    /// 序列化元素到文件（自动创建父目录；含 XML 声明）。
    /// 对应 Java: <c>ElemCup.dump(Element, Path)</c>
    /// </summary>
    public static void Dump(XElement? element, string to)
    {
        if (element is null) return;
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("文档元素序列化目标路径（to）为空", nameof(to));

        WriteTo(element, to, upgradeNs: false);
    }

    /// <summary>
    /// 序列化元素到文件，并先把元素（含后代）的命名空间升级为 OFD 标准命名空间。
    /// <para>
    /// 用于回写由第三方工具生成、缺少命名空间（或命名空间错误）的 OFD 片段。
    /// 对应 Java: <c>ElemCup.dumpUpNS(Element, Path)</c>
    /// </para>
    /// </summary>
    public static void DumpUpNs(XElement? element, string to)
    {
        if (element is null) return;
        if (string.IsNullOrWhiteSpace(to))
            throw new ArgumentException("文档元素序列化目标路径（to）为空", nameof(to));

        WriteTo(element, to, upgradeNs: true);
    }

    private static void WriteTo(XElement element, string to, bool upgradeNs)
    {
        string full = Path.GetFullPath(to);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // 不直接改调用方传入的元素：先克隆，避免就地升级命名空间污染内存中的文档树
        var target = new XElement(element);

        if (upgradeNs)
            new OFDNameSpaceModifier().Upgrade(target);

        File.WriteAllText(full, ToXml(target), new UTF8Encoding(false));
    }
}
