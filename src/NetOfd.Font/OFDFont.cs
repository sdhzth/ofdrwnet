using SkiaSharp;

namespace NetOfd.Font;

/// <summary>
/// OFD 字体描述，使用 SkiaSharp 进行跨平台字体度量。
/// 对应 Java: org.ofdrw.font.Font
/// </summary>
public class OFDFont : IDisposable
{
    private bool _disposed;
    private SKTypeface? _typeface;

    // ── 基本属性 ─────────────────────────────────────────────
    /// <summary>字体名称（用于 OFD FontName 属性）</summary>
    public string Name { get; }

    /// <summary>字体族名（用于 OFD FamilyName 属性）</summary>
    public string FamilyName { get; }

    /// <summary>字体文件路径（可选，用于嵌入字体）</summary>
    public string? FontFilePath { get; }

    /// <summary>是否可嵌入</summary>
    public bool Embeddable { get; set; } = true;

    /// <summary>是否为内置字体（不需要嵌入文件）</summary>
    public bool IsBuiltIn { get; }

    /// <summary>可打印 ASCII 字符宽度比例表（字符宽/字体大小）</summary>
    private float[]? _asciiWidthMap;

    // ── 构造 ─────────────────────────────────────────────────

    /// <summary>使用内置字体名称创建</summary>
    public OFDFont(FontName builtIn)
    {
        Name = FontNames.GetFontNameValue(builtIn);
        FamilyName = FontNames.GetFamilyName(builtIn);
        IsBuiltIn = true;
        _asciiWidthMap = FontNames.GetAsciiWidths(builtIn);
    }

    /// <summary>使用字体文件路径创建</summary>
    public OFDFont(string fontName, string familyName, string fontFilePath)
    {
        Name = fontName;
        FamilyName = familyName;
        FontFilePath = fontFilePath;
        IsBuiltIn = false;
    }

    /// <summary>
    /// 使用字体名 / 字体族名创建（无字体文件，度量走系统字体回退）。
    /// 对应 Java: <c>new Font(ctFont.getFontName(), ctFont.getFamilyName())</c>。
    /// <para>若 <paramref name="fontName"/> 命中内置字体枚举（如 "Times New Roman"），
    /// 则自动套用该字体的可打印 ASCII 宽度表以保证度量一致（对齐 Java 对 Times New Roman 的特判）。</para>
    /// </summary>
    public OFDFont(string fontName, string? familyName)
    {
        Name = fontName;
        FamilyName = string.IsNullOrWhiteSpace(familyName) ? fontName : familyName;
        FontFilePath = null;
        IsBuiltIn = FontNames.TryParse(fontName, out var builtIn);
        if (IsBuiltIn)
        {
            _asciiWidthMap = FontNames.GetAsciiWidths(builtIn);
            FamilyName = FontNames.GetFamilyName(builtIn);
        }
    }

    // ── 字体度量 ─────────────────────────────────────────────

    /// <summary>
    /// 获取字符相对于字体大小的宽度比例（0~1之间）。
    /// 对于中文字符通常返回 1.0（全宽），ASCII 字符依字体而定。
    /// </summary>
    public float GetCharWidthScale(char c)
    {
        // ASCII 可打印字符：使用预计算表
        if (c >= 32 && c <= 126 && _asciiWidthMap is not null)
        {
            return _asciiWidthMap[c - 32];
        }

        // 中文和其他 Unicode 字符：使用 SkiaSharp 测量或返回默认值
        if (c > 127)
        {
            // 中文字符通常为全宽（1.0 em）
            if (c >= 0x4E00 && c <= 0x9FFF) return 1.0f;
            if (c >= 0x3000 && c <= 0x303F) return 1.0f;
            if (c >= 0xFF00 && c <= 0xFFEF) return 1.0f;
        }

        return MeasureCharWidth(c);
    }

    /// <summary>
    /// 使用 SkiaSharp 测量字符宽度（会触发字体加载，有性能开销）
    /// </summary>
    private float MeasureCharWidth(char c)
    {
        EnsureTypeface();
        if (_typeface is null) return 0.5f;

        using var paint = new SKPaint
        {
            Typeface = _typeface,
            TextSize = 1000f, // 使用大字号以提高精度
            IsAntialias = false
        };

        float width = paint.MeasureText(c.ToString());
        return width / 1000f;
    }

    private void EnsureTypeface()
    {
        if (_typeface is not null) return;

        if (!string.IsNullOrEmpty(FontFilePath) && File.Exists(FontFilePath))
        {
            _typeface = SKTypeface.FromFile(FontFilePath);
        }
        else
        {
            // 尝试系统字体
            _typeface = SKTypeface.FromFamilyName(FamilyName);
        }
    }

    /// <summary>
    /// 计算字符串在给定字体大小（磅值）下的宽度（mm）
    /// </summary>
    public double MeasureTextWidth(string text, double fontSizePt)
    {
        double totalWidth = 0;
        foreach (char c in text)
        {
            totalWidth += GetCharWidthScale(c) * fontSizePt;
        }
        // 磅转毫米：1pt = 25.4/72 mm ≈ 0.3528 mm
        return totalWidth * 25.4 / 72.0;
    }

    // ── Dispose ──────────────────────────────────────────────
    public void Dispose()
    {
        if (!_disposed)
        {
            _typeface?.Dispose();
            _typeface = null;
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
