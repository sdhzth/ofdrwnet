namespace NetOfd.Core.PageDescription.Font;

/// <summary>
/// 字形适用的字符分类（GB/T 33190-2016 §11.1 表 44，附录 A.5 CT_Font）。
/// <para>用于匹配替代字形，默认值为 <see cref="Unicode"/>。</para>
/// 对应 Java: org.ofdrw.core.text.font.Charset
/// </summary>
public enum Charset
{
    /// <summary>符号字符集。</summary>
    Symbol,

    /// <summary>简体中文（GB2312/GBK 系列）。</summary>
    Prc,

    /// <summary>繁体中文（Big5）。</summary>
    Big5,

    /// <summary>日文（Shift-JIS）。XML 中的写法为 <c>shift-jis</c>。</summary>
    ShiftJis,

    /// <summary>韩文 Wansung（KS C 5601）。</summary>
    Wansung,

    /// <summary>韩文 Johab。</summary>
    Johab,

    /// <summary>Unicode（默认值）。</summary>
    Unicode
}

/// <summary>
/// <see cref="Charset"/> 的解析/格式化辅助（与 XML 属性字符串互转）。
/// </summary>
public static class CharsetUtil
{
    /// <summary>解析 XML 属性值；空值视为 <see cref="Charset.Unicode"/>。未知取值抛出 <see cref="FormatException"/>。</summary>
    public static Charset Parse(string? value)
    {
        var s = (value ?? string.Empty).Trim();
        if (s.Length == 0) return Charset.Unicode;

        if (s.Equals("symbol", StringComparison.OrdinalIgnoreCase)) return Charset.Symbol;
        if (s.Equals("prc", StringComparison.OrdinalIgnoreCase)) return Charset.Prc;
        if (s.Equals("big5", StringComparison.OrdinalIgnoreCase)) return Charset.Big5;
        if (s.Equals("shift-jis", StringComparison.OrdinalIgnoreCase)) return Charset.ShiftJis;
        if (s.Equals("wansung", StringComparison.OrdinalIgnoreCase)) return Charset.Wansung;
        if (s.Equals("johab", StringComparison.OrdinalIgnoreCase)) return Charset.Johab;
        if (s.Equals("unicode", StringComparison.OrdinalIgnoreCase)) return Charset.Unicode;

        throw new FormatException("未知的字形适用的字符分类：" + value);
    }

    /// <summary>转为 XML 属性值。注意 <see cref="Charset.ShiftJis"/> 写作 <c>shift-jis</c>（下划线转连字符）。</summary>
    public static string ToXml(Charset charset) => charset switch
    {
        Charset.Symbol => "symbol",
        Charset.Prc => "prc",
        Charset.Big5 => "big5",
        Charset.ShiftJis => "shift-jis",
        Charset.Wansung => "wansung",
        Charset.Johab => "johab",
        _ => "unicode"
    };
}
