using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// CT_Color - 颜色定义，支持 RGB、CMYK、灰度等颜色空间。
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.CT_Color
/// </summary>
public class CT_Color : OFDElement
{
    public CT_Color() : base("Color") { }
    public CT_Color(XElement element) : base(element) { }

    /// <summary>
    /// 以指定元素名构造颜色（供 BorderColor 等具名颜色子类使用）。
    /// </summary>
    protected CT_Color(string localName) : base(localName) { }

    /// <summary>颜色值（各通道空格分隔，如 "255 0 0" 表示红色）</summary>
    public string? Value
    {
        get => GetAttr("Value");
        set => SetAttr("Value", NormalizeChannels(value));
    }

    /// <summary>
    /// 归一化颜色通道分隔符：GB/T 33190 §7.4 规定 <c>Color/@Value</c> 各通道以<b>空格</b>分隔。
    /// <para>调用方若传入逗号/分号分隔（如 <c>"255,0,0"</c>），写盘时统一转为空格分隔，
    /// 避免 ofdrw / ofdgo（pdfcpu 管线）报 “expected 3 color components” 而整页渲染失败。</para>
    /// </summary>
    private static string? NormalizeChannels(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        if (value.IndexOf(',') < 0 && value.IndexOf(';') < 0) return value;
        return string.Join(" ", value.Split(new[] { ' ', ',', ';', '\t', '\r', '\n' },
            StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>颜色空间 ID 引用</summary>
    public STRefID? ColorSpace
    {
        get
        {
            var v = GetAttr("ColorSpace");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("ColorSpace", value?.Value.ToString());
    }

    /// <summary>【可选】调色板索引（引用 ColorSpace 内 Palette 的第 N 个预定义颜色，0 起）。</summary>
    public int? Index
    {
        get
        {
            var v = GetAttr("Index");
            return v is null ? null : STBase.ToInt(v);
        }
        set => SetAttr("Index", value?.ToString());
    }

    /// <summary>透明度 (0-255, 255=不透明)</summary>
    public int? Alpha
    {
        get
        {
            var v = GetAttr("Alpha");
            return v is null ? null : (int)STBase.ToDouble(v);
        }
        set => SetAttr("Alpha", value?.ToString());
    }

    // ── 颜色簇（渐变 / 图案底纹）──────────────────────────────

    /// <summary>所有可出现在 Color 下的底纹元素名（互斥，设置新的会移除旧的）。</summary>
    private static readonly string[] ColorClusterNames =
        { "Pattern", "AxialShd", "RadialShd", "GouraudShd", "LaGouraudShd" };

    /// <summary>
    /// 以颜色簇（渐变底纹）替代单一颜色值：移除已有底纹子元素后挂接新的。
    /// 对应 Java: CT_Color.setColor(ColorClusterType)
    /// </summary>
    public CT_Color SetColorCluster(ColorClusterType cluster)
    {
        if (cluster is null) throw new System.ArgumentNullException(nameof(cluster));
        if (cluster is not OFDElement el)
            throw new System.ArgumentException("颜色簇必须同时是 OFDElement", nameof(cluster));

        Element.Elements().Where(e => ColorClusterNames.Contains(e.Name.LocalName)).Remove();
        Element.Add(el.Element);
        return this;
    }

    /// <summary>获取轴向渐变底纹，不存在时返回 null。</summary>
    public CT_AxialShd? GetAxialShd()
    {
        var e = GetChildElement("AxialShd");
        return e is null ? null : new CT_AxialShd(e);
    }

    /// <summary>获取径向渐变底纹，不存在时返回 null。</summary>
    public CT_RadialShd? GetRadialShd()
    {
        var e = GetChildElement("RadialShd");
        return e is null ? null : new CT_RadialShd(e);
    }

    /// <summary>获取图案底纹，不存在时返回 null。</summary>
    public CT_Pattern? GetPattern()
    {
        var e = GetChildElement("Pattern");
        return e is null ? null : new CT_Pattern(e);
    }

    /// <summary>以图案底纹挂接（互斥移除其它颜色簇后挂接 <c>&lt;Pattern&gt;</c> 子元素）。</summary>
    public CT_Color SetPattern(CT_Pattern pattern) => (CT_Color)SetColorCluster(pattern);

    /// <summary>获取自由高洛德渐变底纹，不存在时返回 null。</summary>
    public CT_GouraudShd? GetGouraudShd()
    {
        var e = GetChildElement("GouraudShd");
        return e is null ? null : new CT_GouraudShd(e);
    }

    /// <summary>以自由高洛德渐变挂接（互斥移除其它颜色簇）。</summary>
    public CT_Color SetGouraudShd(CT_GouraudShd shd) => (CT_Color)SetColorCluster(shd);

    /// <summary>获取网格高洛德渐变底纹，不存在时返回 null。</summary>
    public CT_LaGouraudShd? GetLaGouraudShd()
    {
        var e = GetChildElement("LaGouraudShd");
        return e is null ? null : new CT_LaGouraudShd(e);
    }

    /// <summary>以网格高洛德渐变挂接（互斥移除其它颜色簇）。</summary>
    public CT_Color SetLaGouraudShd(CT_LaGouraudShd shd) => (CT_Color)SetColorCluster(shd);

    /// <summary>
    /// 获取本颜色上挂接的颜色簇（轴向 / 径向渐变、自由 / 网格高洛德、图案底纹），无底纹时返回 null。
    /// </summary>
    public ColorClusterType? GetColorCluster()
        => (ColorClusterType?)GetAxialShd()
           ?? (ColorClusterType?)GetRadialShd()
           ?? (ColorClusterType?)GetGouraudShd()
           ?? (ColorClusterType?)GetLaGouraudShd()
           ?? (ColorClusterType?)GetPattern();

    // ── 常用颜色工厂方法 ──────────────────────────────────────

    /// <summary>RGB 颜色（0-255）</summary>
    public static CT_Color FromRgb(int r, int g, int b)
        => new() { Value = $"{r} {g} {b}" };

    /// <summary>灰度颜色（0-255，0=黑，255=白）</summary>
    public static CT_Color FromGray(int gray)
        => new() { Value = gray.ToString() };

    public static CT_Color Black => FromRgb(0, 0, 0);
    public static CT_Color White => FromRgb(255, 255, 255);
    public static CT_Color Red => FromRgb(255, 0, 0);
    public static CT_Color Blue => FromRgb(0, 0, 255);
    public static CT_Color Green => FromRgb(0, 128, 0);
}
