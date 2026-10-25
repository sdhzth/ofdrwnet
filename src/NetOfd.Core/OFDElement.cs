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
