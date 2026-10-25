using NetOfd.Core.PageDescription.Font;
using SkiaSharp;
using System.Collections.Concurrent;

namespace NetOfd.Converter;

/// <summary>
/// 字体缓存：将 OFD 的 CT_Font 解析为 SkiaSharp 的 <see cref="SKTypeface"/>。
/// 对应 Java: org.ofdrw.converter.FontLoader
/// <para>
/// 本移植版优先使用系统已安装的字体（如 Windows 上的“宋体/SimSun”、“楷体”等）。
/// </para>
/// <para>
/// <b>字体解析策略（2026-10-23 修复）</b>：
/// <list type="bullet">
/// <item>不能用 <see cref="SKTypeface.FromFamilyName"/> 做可用性判断——字体族不存在时它返回
/// <see cref="SKTypeface.Default"/>（非 null），会把「请求楷体却拿到默认字体（中文方块）」误判为命中，
/// 导致后续中文字体兜底候选永远走不到。改用 <see cref="SKFontManager.MatchFamily"/> 精确匹配（找不到返回 null）。</item>
/// <item>国内 OFD（GB/T 33190）大量使用 GB2312 字体内码名（<c>KaiTi_GB2312</c>/<c>SimHei</c>/<c>FangSong</c> 等），
/// 与 Windows 家族名（楷体/黑体/仿宋）不同名，需经别名表映射后再匹配。</item>
/// <item>最终兜底链：SimSun → 宋体 → Microsoft YaHei → 微软雅黑 → Default。</item>
/// </list>
/// </para>
/// </summary>
public static class OfdFontCache
{
    private static readonly ConcurrentDictionary<string, SKTypeface?> _cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// OFD 常见字体内码名/别名 → 系统字体族名候选映射。
    /// 国内 OFD 多用 GB2312 字体内码名（KaiTi_GB2312 等），Windows 家族名为中文（楷体），
    /// Linux/macOS 上常又是 KaiTi/SimSun 等英文名，逐一登记，按序 MatchFamily 精确匹配。
    /// 空数组 = 该名本身即系统字体名，无需展开。
    /// </summary>
    private static readonly Dictionary<string, string[]> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {
        // 楷体
        ["KaiTi_GB2312"] = new[] { "楷体", "KaiTi" },
        ["楷体_GB2312"] = new[] { "楷体", "KaiTi" },
        ["KaiTi"] = new[] { "楷体", "KaiTi" },
        ["楷体"] = Array.Empty<string>(),
        // 宋体
        ["SimSun"] = new[] { "宋体", "SimSun" },
        ["宋体"] = Array.Empty<string>(),
        ["NSimSun"] = new[] { "新宋体", "SimSun" },
        ["新宋体"] = new[] { "新宋体", "SimSun" },
        // 黑体
        ["SimHei"] = new[] { "黑体", "SimHei" },
        ["黑体"] = Array.Empty<string>(),
        // 仿宋
        ["FangSong"] = new[] { "仿宋", "FangSong" },
        ["仿宋"] = Array.Empty<string>(),
        ["FangSong_GB2312"] = new[] { "仿宋", "FangSong" },
        ["仿宋_GB2312"] = new[] { "仿宋", "FangSong" },
        // 微软雅黑
        ["Microsoft YaHei"] = new[] { "微软雅黑", "Microsoft YaHei" },
        ["微软雅黑"] = Array.Empty<string>(),
        // 隶书 / 幼圆 / 华文系
        ["LiSu"] = new[] { "隶书", "LiSu" },
        ["隶书"] = Array.Empty<string>(),
        ["YouYuan"] = new[] { "幼圆", "YouYuan" },
        ["幼圆"] = Array.Empty<string>(),
        ["STXihei"] = new[] { "华文细黑", "STXihei" },
        ["STHeiti"] = new[] { "华文黑体", "STHeiti" },
        ["STKaiti"] = new[] { "华文楷体", "STKaiti" },
        ["STSong"] = new[] { "华文宋体", "STSong" },
        ["STZhongsong"] = new[] { "华文中宋", "STZhongsong" },
        ["STFangsong"] = new[] { "华文仿宋", "STFangsong" },
    };

    /// <summary>根据 CT_Font 获取字形绘制所需的 Typeface（带缓存）。</summary>
    public static SKTypeface GetTypeface(CT_Font? ctFont)
        => GetTypeface(ctFont, false, false);

    /// <summary>
    /// 根据 CT_Font 获取字形绘制所需的 Typeface（带缓存）。
    /// <paramref name="textBold"/> / <paramref name="textItalic"/> 为 CT_Text 上的
    /// 文本级覆盖，优先级高于字体资源自身的 Bold/Italic 标记——很多 OFD 在 Text 上标粗而非在 Font 上。
    /// </summary>
    public static SKTypeface GetTypeface(CT_Font? ctFont, bool textBold, bool textItalic)
    {
        if (ctFont is null) return SKTypeface.Default;

        var style = MakeStyle(ctFont, textBold, textItalic);

        // 候选链：FamilyName/FontName 原名 → 别名展开 → 中文字体兜底。
        foreach (var name in BuildCandidates(ctFont))
        {
            var tf = Resolve(name, style);
            if (tf is not null)
                return tf;
        }
        return SKTypeface.Default;
    }

    /// <summary>构造候选字体名链（去重、保序）。</summary>
    private static IEnumerable<string> BuildCandidates(CT_Font ctFont)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in new[] { ctFont.FamilyName, ctFont.FontName })
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            foreach (var cand in Expand(raw.Trim()))
            {
                if (seen.Add(cand)) yield return cand;
            }
        }
        // 中文字体兜底链（中文 Windows 必有；Linux 需装有中文字体方可，全部落空才回 Default）
        foreach (var fallback in new[] { "SimSun", "宋体", "Microsoft YaHei", "微软雅黑" })
        {
            if (seen.Add(fallback)) yield return fallback;
        }
    }

    /// <summary>展开单个字体名：原名 + 别名表中的系统字体族名。</summary>
    private static IEnumerable<string> Expand(string name)
    {
        yield return name;
        if (Aliases.TryGetValue(name, out var targets))
        {
            foreach (var t in targets)
            {
                if (!string.IsNullOrWhiteSpace(t)) yield return t;
            }
        }
    }

    private static SKTypeface? Resolve(string name, SKFontStyle style)
    {
        string key = $"{name}|{(int)style.Weight}|{(int)style.Width}|{(int)style.Slant}";
        if (_cache.TryGetValue(key, out var cached))
            return cached;

        // 精确匹配：族不存在返回 null（区别于 FromFamilyName 找不到时返回 Default 的兜底行为）
        var tf = SKFontManager.Default.MatchFamily(name, style);
        _cache[key] = tf;
        return tf;
    }

    private static SKFontStyle MakeStyle(CT_Font ctFont)
        => MakeStyle(ctFont, false, false);

    private static SKFontStyle MakeStyle(CT_Font ctFont, bool textBold, bool textItalic)
    {
        // 文本级标记与字体资源标记“或”合并：任一为粗体/斜体即生效
        var weight = (textBold || ctFont.Bold) ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal;
        var slant = (textItalic || ctFont.Italic) ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        return new SKFontStyle(weight, SKFontStyleWidth.Normal, slant);
    }
}
