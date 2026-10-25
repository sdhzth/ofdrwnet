namespace NetOfd.Font;

/// <summary>
/// 系统/环境字体管理器，负责扫描和查找系统中已安装的字体。
/// 对应 Java: org.ofdrw.font.EnvFont
/// </summary>
public static class EnvFont
{
    private static Dictionary<string, string>? _fontMap;
    private static readonly object _lock = new();

    /// <summary>获取系统字体目录列表</summary>
    public static IEnumerable<string> GetFontDirs()
    {
        if (OperatingSystem.IsWindows())
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts));
            string? localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            string userFonts = Path.Combine(localAppData, "Microsoft", "Windows", "Fonts");
            if (Directory.Exists(userFonts)) yield return userFonts;
        }
        else if (OperatingSystem.IsMacOS())
        {
            yield return "/System/Library/Fonts";
            yield return "/Library/Fonts";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Library/Fonts");
        }
        else // Linux
        {
            yield return "/usr/share/fonts";
            yield return "/usr/local/share/fonts";
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".fonts");
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local/share/fonts");
        }
    }

    /// <summary>
    /// 扫描系统字体，返回 字体名称（小写） → 字体文件路径 的映射
    /// </summary>
    public static Dictionary<string, string> GetFontMap()
    {
        if (_fontMap is not null) return _fontMap;

        lock (_lock)
        {
            if (_fontMap is not null) return _fontMap;

            var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var dir in GetFontDirs())
            {
                if (!Directory.Exists(dir)) continue;
                foreach (var file in Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories))
                {
                    string ext = Path.GetExtension(file).ToLowerInvariant();
                    if (ext is ".ttf" or ".otf" or ".ttc" or ".woff" or ".woff2")
                    {
                        string name = Path.GetFileNameWithoutExtension(file);
                        map.TryAdd(name.ToLowerInvariant(), file);
                    }
                }
            }
            _fontMap = map;
        }
        return _fontMap;
    }

    /// <summary>按名称查找系统字体路径</summary>
    public static string? FindFontFile(string fontName)
    {
        var map = GetFontMap();
        if (map.TryGetValue(fontName.ToLowerInvariant(), out var path))
            return path;

        // 模糊匹配
        string lowerName = fontName.ToLowerInvariant();
        foreach (var (key, value) in map)
        {
            if (key.Contains(lowerName) || lowerName.Contains(key))
                return value;
        }
        return null;
    }

    /// <summary>检查是否安装了指定字体</summary>
    public static bool IsFontInstalled(string fontName)
        => FindFontFile(fontName) is not null;

    /// <summary>重置字体缓存（用于测试）</summary>
    public static void ClearCache()
    {
        lock (_lock) { _fontMap = null; }
    }
}
