namespace NetOfd.Font;

/// <summary>
/// 内置字体名称枚举。
/// 对应 Java: org.ofdrw.font.FontName
/// </summary>
public enum FontName
{
    /// <summary>宋体（仿宋GB2312）</summary>
    SimSun,
    /// <summary>黑体</summary>
    SimHei,
    /// <summary>微软雅黑</summary>
    MSYahei,
    /// <summary>楷体</summary>
    KaiTi,
    /// <summary>仿宋</summary>
    FangSong,
    /// <summary>Times New Roman</summary>
    TimesNewRoman,
    /// <summary>Courier New（等宽字体）</summary>
    CourierNew,
    /// <summary>Arial</summary>
    Arial,
    /// <summary>自定义字体（使用 FontFile 路径）</summary>
    Custom
}

/// <summary>
/// 字体名称帮助类，提供字体映射、字符宽度数据等。
/// 对应 Java: org.ofdrw.font.FontName 枚举及相关静态数据
/// </summary>
public static class FontNames
{
    /// <summary>获取字体的规范化字体族名</summary>
    public static string GetFamilyName(FontName fontName) => fontName switch
    {
        FontName.SimSun => "宋体",
        FontName.SimHei => "黑体",
        FontName.MSYahei => "微软雅黑",
        FontName.KaiTi => "楷体",
        FontName.FangSong => "仿宋",
        FontName.TimesNewRoman => "Times New Roman",
        FontName.CourierNew => "Courier New",
        FontName.Arial => "Arial",
        _ => "宋体"
    };

    /// <summary>获取字体的 OFD FontName 属性值</summary>
    public static string GetFontNameValue(FontName fontName) => fontName switch
    {
        FontName.SimSun => "宋体",
        FontName.SimHei => "黑体",
        FontName.MSYahei => "微软雅黑",
        FontName.KaiTi => "楷体",
        FontName.FangSong => "仿宋",
        FontName.TimesNewRoman => "Times New Roman",
        FontName.CourierNew => "Courier New",
        FontName.Arial => "Arial",
        _ => "宋体"
    };

    /// <summary>
    /// 按字体名（OFD FontName 属性值或字体族名）反查内置字体枚举。
    /// 匹配为大小写不敏感，且同时接受中文名（"宋体"）与常见英文别名（"SimSun"/"Times New Roman" 等）。
    /// 未命中时返回 false。
    /// </summary>
    public static bool TryParse(string? fontName, out FontName font)
    {
        font = default;
        if (string.IsNullOrWhiteSpace(fontName)) return false;

        var n = fontName.Trim();
        var hit = n.ToLowerInvariant() switch
        {
            "宋体" or "simsun" or "song" or "songti" or "stsong" => (FontName?)FontName.SimSun,
            "黑体" or "simhei" or "heiti" => FontName.SimHei,
            "微软雅黑" or "msyahei" or "yahei" or "microsoft yahei" => FontName.MSYahei,
            "楷体" or "kaiti" or "stkaiti" => FontName.KaiTi,
            "仿宋" or "fangsong" or "stfangsong" => FontName.FangSong,
            "times new roman" or "timesnewroman" or "times" => FontName.TimesNewRoman,
            "courier new" or "couriernew" or "courier" => FontName.CourierNew,
            "arial" => FontName.Arial,
            _ => null
        };

        if (hit is null) return false;
        font = hit.Value;
        return true;
    }

    // ── 预计算的可打印 ASCII 字符宽度比例表 ─────────────────
    // 宽度比例 = 字符宽度 / 字体大小（em），与 Java 版保持一致
    // Noto 系列（宋体/黑体等中文字体）
    public static readonly float[] NotoAsciiWidths = new float[95];
    // Times New Roman
    public static readonly float[] TimesNewRomanAsciiWidths = new float[95];

    static FontNames()
    {
        // 使用通用的等宽近似值（实际会通过 SkiaSharp 动态测量）
        // ASCII 可打印字符从 space(32) 到 tilde(126)
        // 中文字体的 ASCII 字符通常 0.5 em 宽
        for (int i = 0; i < 95; i++)
            NotoAsciiWidths[i] = 0.5f;

        // Times New Roman 各字符宽度（手工标定的近似值）
        // 空格=0.25, 感叹号=0.278, ...（简化为统一值，实际用 SkiaSharp 测量）
        for (int i = 0; i < 95; i++)
            TimesNewRomanAsciiWidths[i] = 0.5f;

        // 常见字符的手工标定宽度（Times New Roman em 单位）
        var tnrWidths = new Dictionary<char, float>
        {
            [' '] = 0.25f, ['!'] = 0.278f, ['"'] = 0.355f, ['#'] = 0.556f,
            ['$'] = 0.556f, ['%'] = 0.889f, ['&'] = 0.667f, ['\''] = 0.191f,
            ['('] = 0.333f, [')'] = 0.333f, ['*'] = 0.389f, ['+'] = 0.584f,
            [','] = 0.278f, ['-'] = 0.333f, ['.'] = 0.278f, ['/'] = 0.278f,
            ['0'] = 0.556f, ['1'] = 0.556f, ['2'] = 0.556f, ['3'] = 0.556f,
            ['4'] = 0.556f, ['5'] = 0.556f, ['6'] = 0.556f, ['7'] = 0.556f,
            ['8'] = 0.556f, ['9'] = 0.556f, [':'] = 0.278f, [';'] = 0.278f,
            ['<'] = 0.584f, ['='] = 0.584f, ['>'] = 0.584f, ['?'] = 0.556f,
            ['@'] = 1.015f, ['A'] = 0.667f, ['B'] = 0.667f, ['C'] = 0.722f,
            ['D'] = 0.722f, ['E'] = 0.667f, ['F'] = 0.611f, ['G'] = 0.778f,
            ['H'] = 0.722f, ['I'] = 0.278f, ['J'] = 0.500f, ['K'] = 0.667f,
            ['L'] = 0.556f, ['M'] = 0.833f, ['N'] = 0.722f, ['O'] = 0.778f,
            ['P'] = 0.667f, ['Q'] = 0.778f, ['R'] = 0.722f, ['S'] = 0.667f,
            ['T'] = 0.611f, ['U'] = 0.722f, ['V'] = 0.667f, ['W'] = 0.944f,
            ['X'] = 0.667f, ['Y'] = 0.667f, ['Z'] = 0.611f, ['['] = 0.278f,
            ['\\'] = 0.278f, [']'] = 0.278f, ['^'] = 0.469f, ['_'] = 0.556f,
            ['`'] = 0.333f, ['a'] = 0.556f, ['b'] = 0.611f, ['c'] = 0.556f,
            ['d'] = 0.611f, ['e'] = 0.556f, ['f'] = 0.333f, ['g'] = 0.611f,
            ['h'] = 0.611f, ['i'] = 0.278f, ['j'] = 0.278f, ['k'] = 0.556f,
            ['l'] = 0.278f, ['m'] = 0.889f, ['n'] = 0.611f, ['o'] = 0.611f,
            ['p'] = 0.611f, ['q'] = 0.611f, ['r'] = 0.389f, ['s'] = 0.556f,
            ['t'] = 0.333f, ['u'] = 0.611f, ['v'] = 0.556f, ['w'] = 0.778f,
            ['x'] = 0.556f, ['y'] = 0.556f, ['z'] = 0.500f, ['{'] = 0.389f,
            ['|'] = 0.280f, ['}'] = 0.389f, ['~'] = 0.584f
        };
        foreach (var (ch, w) in tnrWidths)
        {
            int idx = ch - 32;
            if (idx >= 0 && idx < 95)
                TimesNewRomanAsciiWidths[idx] = w;
        }
    }

    /// <summary>获取字体的 ASCII 字符宽度比例表</summary>
    public static float[] GetAsciiWidths(FontName fontName) => fontName switch
    {
        FontName.TimesNewRoman => TimesNewRomanAsciiWidths,
        _ => NotoAsciiWidths
    };
}
