using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core;

/// <summary>
/// OFD XML 元素基类，封装 <see cref="XElement"/> 并提供 OFD 命名空间感知的操作。
/// 对应 Java: org.ofdrw.core.DefaultElementProxy + org.ofdrw.core.OFDElement
/// </summary>
public abstract class OFDElement
{
    // ────────────────────────────────────────────────────────
    //  OFD 标准命名空间
    // ────────────────────────────────────────────────────────
    /// <summary>OFD 核心命名空间 (GB/T 33190-2016)</summary>
    public static readonly XNamespace OfdNs = "http://www.ofdspec.org/2016";

    /// <summary>签名命名空间</summary>
    public static readonly XNamespace SignNs = "http://www.ofdspec.org/2016/digital-signature";

    /// <summary>
    /// <c>defaults</c> 命名空间前缀（GB/T 33190-2016 §7.1 / GB/T 48666-2026 §6.2）。
    /// <para>
    /// 标准要求「容器内各 XML 文件的根节点中应声明 <c>defaults:ofd</c>」——
    /// 即根节点除 <c>xmlns:ofd</c> 外，还须额外声明 <c>xmlns:defaults="http://www.ofdspec.org/2016"</c>，
    /// 使同一命名空间同时具备 <c>ofd</c> 与 <c>defaults</c> 两个前缀标识，
    /// 以兼容按 <c>defaults:</c> 前缀解析的实现。
    /// </para>
    /// </summary>
    public static readonly XName DefaultsNs = XNamespace.Xmlns + "defaults";

    /// <summary>
    /// 为 OFD 命名空间的根节点补齐 <c>xmlns:defaults</c> 声明（幂等）。
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>本方法已废弃，请勿使用</b>——它会破坏文档。
    /// <para>
    /// 原因：LINQ-to-XML 无法表达「同一命名空间的两个前缀」。实测（.NET 10）：
    /// 对任何根元素追加 <c>new XAttribute(XNamespace.Xmlns + "defaults", uri)</c> 后再序列化，
    /// LINQ 会把<b>元素前缀</b>由 <c>ofd:</c> 改写为 <c>defaults:</c>，
    /// 属性名亦随之变成 <c>defaults:Version</c> 形式（S1/S2/S3 三种输入均如此），
    /// 严重违反 GB/T 33190-2016 §7.1「元素节点应使用命名空间标识（标识为 ofd）」。
    /// 即便先做字符串注入再 <c>XElement.Parse</c> 回解析，序列化时同样被改写为
    /// <c>defaults:</c>——故<b>任何经 LINQ-to-XML 序列化的路径都无法正确产出双前缀文档</b>。
    /// </para>
    /// <para>
    /// 正确做法：使用 <see cref="InjectDefaultsDeclaration(string,string)"/>，
    /// 在<b>已序列化的文本</b>上直接插入属性声明，绕开 LINQ 的前缀分配逻辑。
    /// </para>
    /// </remarks>
    [Obsolete("LINQ-to-XML 会把元素前缀改写为 defaults:，破坏 §7.1「标识符宜为 ofd」。" +
              "请改用 InjectDefaultsDeclaration(string,string) 在文本层注入。")]
    public static XElement EnsureDefaultsNamespace(XElement root)
    {
        if (root == null) return root!;
        // 仅对 OFD 命名空间的根节点生效
        if (root.Name.Namespace != OfdNs) return root;
        // 已声明则不重复添加（幂等）
        if (root.Attribute(DefaultsNs + "ofd") != null) return root;
        root.Add(new XAttribute(DefaultsNs + "ofd", OfdNs.NamespaceName));
        return root;
    }

    /// <summary>
    /// 为 OFD 命名空间的根节点补齐 <c>xmlns:defaults</c> 声明（幂等），返回序列化字节。
    /// </summary>
    /// <remarks>
    /// ⚠️ <b>本方法已废弃，请勿使用</b>——见 <see cref="EnsureDefaultsNamespace(XElement)"/> 的说明：
    /// 经 XmlWriter 序列化同样会把元素前缀改写为 <c>defaults:</c>。
    /// 请改用 <see cref="InjectDefaultsDeclarationBytes(byte[],string)"/>。
    /// </remarks>
    [Obsolete("序列化会改写元素前缀为 defaults:，破坏 §7.1。请改用 InjectDefaultsDeclarationBytes(byte[],string)。")]
    public static byte[] EnsureDefaultsNamespaceBytes(XDocument doc, bool indent = false)
    {
        if (doc?.Root != null) EnsureDefaultsNamespace(doc.Root);
        var settings = new System.Xml.XmlWriterSettings
        {
            Encoding = new System.Text.UTF8Encoding(false),
            Indent = indent,
            OmitXmlDeclaration = false,
        };
        using var ms = new MemoryStream();
        using (var w = System.Xml.XmlWriter.Create(ms, settings))
            doc!.Save(w);
        return ms.ToArray();
    }

    /// <summary>
    /// 在 XML 文本层面为根开始标签补齐 <c>xmlns:defaults</c> 声明（幂等）。
    /// <para>
    /// GB/T 33190-2016 §7.1 与 GB/T 48666-2026 §6.2：「容器内各 XML 文件的根节点中
    /// 应声明 defaults:ofd」。本方法在已序列化的文本上直接插入属性声明，
    /// 对默认命名空间形式（<c>xmlns="..."</c>）与前缀形式（<c>xmlns:ofd="..."</c>）均适用，
    /// 且不会改变元素的命名空间归属。
    /// </para>
    /// <para>
    /// <b>为何不能用 LINQ-to-XML 追加命名空间声明</b>：LINQ 无法表达「同一命名空间的两个前缀」，
    /// 实测（.NET 10）对根元素追加 <c>new XAttribute(XNamespace.Xmlns + "defaults", uri)</c>
    /// 后再序列化，LINQ 会把元素前缀由 <c>ofd:</c> 改写为 <c>defaults:</c>
    /// （属性名亦变成 <c>defaults:Version</c> 形式），违反 §7.1「标识符宜为 ofd」。
    /// 即便先做字符串注入再 <c>XElement.Parse</c> 回解析，序列化时同样被改写。
    /// 故本方法直接在<b>已序列化的文本</b>上操作，是唯一可行的注入路径。
    /// </para>
    /// <para>
    /// ⚠️ 调用方须确保在计算文件杂凑/签名<b>之前</b>调用，否则签名值将与实际字节不符。
    /// </para>
    /// </summary>
    /// <param name="xml">XML 文本（UTF-8 解码后的内容）。</param>
    /// <param name="ns">OFD 命名空间 URI。</param>
    /// <returns>处理后的文本；非 OFD 命名空间根节点或已声明时原样返回。</returns>
    public static string InjectDefaultsDeclaration(string xml, string ns)
    {
        if (string.IsNullOrEmpty(xml) || string.IsNullOrEmpty(ns)) return xml;
        if (xml.Contains("xmlns:defaults")) return xml;    // 幂等

        // 定位根节点的开始标签（跳过 XML 声明、注释、处理指令）
        int idx = 0, lt = -1;
        while (true)
        {
            lt = xml.IndexOf('<', idx);
            if (lt < 0) return xml;
            if (lt + 1 < xml.Length && xml[lt + 1] == '?')   // 处理指令 / XML 声明
            {
                int gt = xml.IndexOf("?>", lt);
                if (gt < 0) return xml;
                idx = gt + 2;
                continue;
            }
            if (lt + 1 < xml.Length && xml[lt + 1] == '!')   // 注释 / DOCTYPE
            {
                // 注释以 --> 结束，DOCTYPE 可能含内部子集（[... ]），需一并跳过
                int gt = xml.IndexOf('>', lt);
                if (gt < 0) return xml;
                idx = gt + 1;
                continue;
            }
            break;
        }

        // 根元素名结束位置（空白 / '>' / '/'）
        int nameEnd = lt + 1;
        while (nameEnd < xml.Length
               && !char.IsWhiteSpace(xml[nameEnd])
               && xml[nameEnd] != '>' && xml[nameEnd] != '/')
            nameEnd++;

        // 根开始标签的结束位置
        int tagEnd = xml.IndexOf('>', nameEnd);
        if (tagEnd < 0) return xml;

        // 仅当根开始标签自身声明了 OFD 命名空间时才处理
        string rootTag = xml.Substring(lt, tagEnd - lt + 1);
        if (!rootTag.Contains($"\"{ns}\"")) return xml;

        int insertPos = tagEnd;
        if (insertPos > lt && xml[insertPos - 1] == '/') insertPos--;   // 自闭合标签插在 '/' 之前

        return xml.Substring(0, insertPos) + $" xmlns:defaults=\"{ns}\"" + xml.Substring(insertPos);
    }

    /// <summary>
    /// <see cref="InjectDefaultsDeclaration(string,string)"/> 的字节流版本。
    /// <para>
    /// 对非 UTF-8 内容或空内容原样返回（不抛异常），便于在批量处理路径上安全调用。
    /// </para>
    /// </summary>
    public static byte[] InjectDefaultsDeclarationBytes(byte[] content, string ns)
    {
        if (content == null || content.Length == 0) return content!;
        string text;
        try { text = new System.Text.UTF8Encoding(false).GetString(content); }
        catch { return content; }
        var patched = InjectDefaultsDeclaration(text, ns);
        if (ReferenceEquals(patched, text) || patched == text) return content;
        return new System.Text.UTF8Encoding(false).GetBytes(patched);
    }

    /// <summary>
    /// 对包内全部 OFD 命名空间的 XML 条目批量补齐 <c>xmlns:defaults</c> 声明（幂等）。
    /// <para>
    /// 本方法是<b>库端统一收口点</b>：凡经此方法处理的包，包内所有 OFD 命名空间的 XML
    /// 根节点均带 <c>xmlns:defaults</c> 声明，满足 GB/T 33190-2016 §7.1 与
    /// GB/T 48666-2026 §6.2。
    /// </para>
    /// <para>
    /// ⚠️ <b>必须在计算文件杂凑/签名之前调用</b>——杂凑与签名均针对文件字节，
    /// 事后追加声明会使签名值与实际字节不符。
    /// </para>
    /// </summary>
    /// <param name="map">包内条目映射（条目名 → 字节）。</param>
    /// <param name="ns">OFD 命名空间 URI。</param>
    /// <returns>被实际修改的条目数（供调用方判断是否需要重算杂凑）。</returns>
    public static int InjectDefaultsDeclarationToMap(Dictionary<string, byte[]> map, string ns)
    {
        if (map == null || map.Count == 0) return 0;
        int changed = 0;
        foreach (var key in map.Keys.ToList())
        {
            if (!key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var patched = InjectDefaultsDeclarationBytes(map[key], ns);
            if (patched.Length == map[key].Length && patched.AsSpan().SequenceEqual(map[key])) continue;
            map[key] = patched;
            changed++;
        }
        return changed;
    }

    /// <summary>
    /// 将 XML 字节规范为「元素前缀为 <c>ofd:</c> + 根节点声明 <c>xmlns:defaults</c>」的标准形式。
    /// <para>
    /// 这是库端写包的<b>统一收口</b>：GB/T 33190-2016 §7.1 要求「标识符宜为 ofd」
    /// 且「应在包内各 XML 文档的根节点申明 defaults:ofd」，二者必须同时满足。
    /// </para>
    /// <para>
    /// <b>为何必须在文本层完成</b>：LINQ-to-XML 无法表达「同一命名空间的两个前缀」，
    /// 实测（.NET 10）经 XmlWriter 序列化时会把元素前缀由 <c>ofd:</c> 改写为
    /// <c>defaults:</c>（属性名亦变成 <c>defaults:Version</c>），违反「标识符宜为 ofd」。
    /// 因此本方法直接在已序列化的文本上插入声明，绕开 LINQ 的前缀分配逻辑。
    /// </para>
    /// <para>
    /// ⚠️ 调用方须确保在计算文件杂凑/签名<b>之前</b>调用，否则签名值将与实际字节不符。
    /// </para>
    /// </summary>
    /// <param name="content">XML 字节（UTF-8）。</param>
    /// <param name="ns">OFD 命名空间 URI。</param>
    /// <returns>规范化后的字节；非 OFD 命名空间根节点时原样返回。</returns>
    public static byte[] NormalizeOfdXmlBytes(byte[] content, string ns)
    {
        return InjectDefaultsDeclarationBytes(content, ns);
    }

    /// <summary>
    /// 将 XML 文本中的 <c>defaults:</c> 元素前缀还原为 <c>ofd:</c>（文本层操作）。
    /// <para>
    /// 背景：LINQ-to-XML 在文档同时声明 <c>xmlns:ofd</c> 与 <c>xmlns:defaults</c>
    /// （同一命名空间的两个前缀）时，序列化会把元素前缀分配给 <c>defaults</c>——
    /// 实测（.NET 10）<c>&lt;ofd:OFD&gt;</c> 会变成 <c>&lt;defaults:OFD&gt;</c>，
    /// 属性名亦变成 <c>defaults:Version</c> 形式，违反 GB/T 33190-2016 §7.1
    /// 「标识符宜为 ofd，元素属性不使用命名空间标识」。
    /// </para>
    /// <para>
    /// 本方法在<b>已序列化的文本</b>上做字符串替换，把元素/属性的 <c>defaults:</c> 前缀
    /// 改回 <c>ofd:</c>，但<b>保留</b>根节点上的 <c>xmlns:defaults</c> 命名空间声明
    /// （标准要求的「声明 defaults:ofd」得以满足）。
    /// </para>
    /// <para>
    /// ⚠️ 仅适用于根节点<b>同时</b>声明了 <c>xmlns:ofd</c> 与 <c>xmlns:defaults</c> 的文档；
    /// 若根节点只声明了 <c>xmlns:defaults</c>（无 <c>xmlns:ofd</c>），则元素前缀改回
    /// <c>ofd:</c> 会产生「未声明前缀」错误（实测 <c>'ofd' is an undeclared prefix</c>），
    /// 此时原样返回。
    /// </para>
    /// </summary>
    /// <param name="xml">XML 文本。</param>
    /// <returns>前缀已还原的文本。</returns>
    public static string RestoreOfdPrefix(string xml)
    {
        if (string.IsNullOrEmpty(xml)) return xml;
        if (!xml.Contains("xmlns:defaults")) return xml;         // 未声明 defaults，无需还原
        if (!xml.Contains("<defaults:")) return xml;             // 无 defaults 前缀元素，无需还原
        if (!xml.Contains("xmlns:ofd")) return xml;               // 无 ofd 前缀声明，改写会造成未声明前缀

        // 仅替换标签名中的 defaults: 前缀（<defaults:X 与 </defaults:X），
        // 不触碰属性值与命名空间声明本身。
        return System.Text.RegularExpressions.Regex.Replace(
            xml,
            @"<(/?)defaults:",
            m => "<" + m.Groups[1].Value + "ofd:",
            System.Text.RegularExpressions.RegexOptions.Compiled);
    }

    /// <summary>
    /// <see cref="RestoreOfdPrefix(string)"/> 的字节流版本。
    /// </summary>
    public static byte[] RestoreOfdPrefixBytes(byte[] content)
    {
        if (content == null || content.Length == 0) return content!;
        string text;
        try { text = new System.Text.UTF8Encoding(false).GetString(content); }
        catch { return content; }
        var patched = RestoreOfdPrefix(text);
        if (patched == text) return content;
        return new System.Text.UTF8Encoding(false).GetBytes(patched);
    }

    /// <summary>是否严格校验命名空间（默认 false，兼容老旧文件）</summary>
    public static bool NSStrictMode { get; set; } = false;

    // ────────────────────────────────────────────────────────
    //  内部 XElement 代理
    // ────────────────────────────────────────────────────────
    internal XElement _el;

    /// <summary>获取底层 XElement</summary>
    public XElement Element => _el;

    // ────────────────────────────────────────────────────────
    //  构造
    // ────────────────────────────────────────────────────────
    protected OFDElement(XElement element)
    {
        _el = element ?? throw new ArgumentNullException(nameof(element));
    }

    /// <summary>以 OFD 命名空间创建一个带标签名的 XElement</summary>
    protected OFDElement(string localName)
        : this(new XElement(OfdNs + localName))
    {
    }

    // ────────────────────────────────────────────────────────
    //  ID 管理
    // ────────────────────────────────────────────────────────
    /// <summary>设置对象 ID（ofd:ID 属性）</summary>
    public OFDElement SetObjID(STID id)
    {
        _el.SetAttributeValue("ID", id.Value);
        return this;
    }

    /// <summary>获取对象 ID</summary>
    public STID? GetObjID()
    {
        string? v = _el.Attribute("ID")?.Value;
        return v is null ? null : STID.Parse(v);
    }

    // ────────────────────────────────────────────────────────
    //  子元素操作
    // ────────────────────────────────────────────────────────
    /// <summary>
    /// 添加 OFD 子元素（如已存在同名元素则先移除旧的）
    /// </summary>
    protected void SetOFDEntity<T>(T entity) where T : OFDElement
    {
        XName name = entity._el.Name;
        _el.Elements(name).Remove();
        _el.Add(entity._el);
    }

    /// <summary>
    /// 添加 OFD 子元素（允许多个同名）
    /// </summary>
    protected void AddOFDEntity<T>(T entity) where T : OFDElement
    {
        _el.Add(entity._el);
    }

    /// <summary>
    /// 获取指定类型的第一个子元素（按 OFD 命名空间或宽松模式匹配）
    /// </summary>
    protected XElement? GetChildElement(string localName)
    {
        if (NSStrictMode)
            return _el.Element(OfdNs + localName);

        // 宽松模式：先尝试 OFD 命名空间，再尝试无命名空间
        return _el.Element(OfdNs + localName)
               ?? _el.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
    }

    /// <summary>
    /// 获取指定类型的所有子元素
    /// </summary>
    protected IEnumerable<XElement> GetChildElements(string localName)
    {
        if (NSStrictMode)
            return _el.Elements(OfdNs + localName);

        return _el.Elements().Where(e =>
            e.Name == OfdNs + localName || e.Name.LocalName == localName);
    }

    // ────────────────────────────────────────────────────────
    //  属性读写帮助
    // ────────────────────────────────────────────────────────
    protected string? GetAttr(string name) => _el.Attribute(name)?.Value;

    protected void SetAttr(string name, string? value)
    {
        if (value is null)
            _el.Attribute(name)?.Remove();
        else
            _el.SetAttributeValue(name, value);
    }

    protected double GetDoubleAttr(string name, double defaultValue = 0.0)
    {
        string? v = GetAttr(name);
        return v is null ? defaultValue : STBase.ToDouble(v);
    }

    protected void SetDoubleAttr(string name, double value) =>
        SetAttr(name, STBase.Fmt(value));

    protected long GetLongAttr(string name, long defaultValue = 0)
    {
        string? v = GetAttr(name);
        return v is null ? defaultValue : STBase.ToLong(v);
    }

    protected void SetLongAttr(string name, long value) =>
        SetAttr(name, value.ToString());

    protected bool GetBoolAttr(string name, bool defaultValue = false)
    {
        string? v = GetAttr(name);
        if (v is null) return defaultValue;
        return v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
    }

    protected void SetBoolAttr(string name, bool value) =>
        SetAttr(name, value ? "true" : "false");

    // ────────────────────────────────────────────────────────
    //  子元素文本读写
    // ────────────────────────────────────────────────────────
    protected string? GetChildText(string localName) =>
        GetChildElement(localName)?.Value;

    /// <summary>获取元素自身的纯文本内容（不含子元素）</summary>
    protected string? GetText() => _el.Value;

    /// <summary>设置元素自身的纯文本内容（替换全部内容）</summary>
    protected void SetText(string? text)
    {
        _el.RemoveAll();
        if (text is not null) _el.Add(text);
    }

    protected void SetChildText(string localName, string? text)
    {
        if (text is null)
        {
            GetChildElement(localName)?.Remove();
            return;
        }
        var child = GetChildElement(localName);
        if (child is null)
        {
            child = new XElement(OfdNs + localName);
            _el.Add(child);
        }
        child.Value = text;
    }

    // ────────────────────────────────────────────────────────
    //  Misc
    // ────────────────────────────────────────────────────────
    /// <summary>移除所有子节点</summary>
    public OFDElement RemoveAll()
    {
        _el.RemoveNodes();
        return this;
    }

    /// <summary>输出 XML 字符串（格式化）</summary>
    public override string ToString() => _el.ToString();

    /// <summary>
    /// 输出带 ofd: 前缀的 XML 字符串（用于 ofd.js 兼容性）。
    /// 将所有元素标签从 {http://www.ofdspec.org/2016}Name 转换为 ofd:Name，
    /// 并在根元素添加 xmlns:ofd 声明。
    /// </summary>
    /// <remarks>
    /// ofd.js 使用 ofd-xml-parser 解析，期望 XML 标签带 ofd: 前缀。
    /// 例如 &lt;Document&gt; 应写作 &lt;ofd:Document&gt;。
    /// </remarks>
    public string ToXmlStringWithPrefix()
    {
        // 克隆，避免修改原始对象
        XElement clone = CloneWithOfdPrefix(_el);
        // 根元素添加 xmlns:ofd 声明
        clone.Add(new XAttribute("xmlns:ofd", OfdNs.NamespaceName));
        // 同时保留已有的其他 xmlns 声明（避免丢失 xlink 等）
        return clone.ToString(SaveOptions.DisableFormatting);
    }

    private static XElement CloneWithOfdPrefix(XElement source)
    {
        // 创建新的 XElement，标签名加上 "ofd:" 前缀
        var newEl = new XElement("ofd:" + source.Name.LocalName, source.Value);
        // 复制属性（属性值如果是命名空间引用，保留原样）
        foreach (var attr in source.Attributes())
        {
            // 跳过 xmlns 声明（因为我们要统一在根元素处理）
            if (attr.Name.NamespaceName == XNamespace.Xmlns.NamespaceName)
                continue;
            newEl.SetAttributeValue(attr.Name, attr.Value);
        }
        // 递归处理子元素
        foreach (var child in source.Elements())
        {
            var newChild = CloneWithOfdPrefix(child);
            newEl.Add(newChild);
        }
        return newEl;
    }

    /// <summary>从已有 XElement 包装为目标类型（工厂委托）</summary>
    public static T Wrap<T>(XElement element, Func<XElement, T> factory) where T : OFDElement
        => factory(element);
}
