using System;

namespace NetOfd.Graphics2D.AWT;

/// <summary>
/// OFD 字体实现（替代 Java AWT Font）
/// </summary>
public class OFDFont
{
    public const int PLAIN = 0;
    public const int BOLD = 1;
    public const int ITALIC = 2;

    public string Name { get; }
    public int Style { get; }
    public double Size { get; }  // 单位：毫米

    public OFDFont(string name, int style, double size)
    {
        Name = name ?? "sanserif";
        Style = style;
        Size = Math.Abs(size);
    }

    public OFDFont(string name, int style, int size) : this(name, style, (double)size) { }

    public bool IsPlain => Style == PLAIN;
    public bool IsBold => (Style & BOLD) != 0;
    public bool IsItalic => (Style & ITALIC) != 0;

    public string GetName() => Name;
    public string GetFamily() => Name;
    public int GetStyle() => Style;
    public double GetSize() => Size;
    public double GetSize2D() => Size;

    public bool IsBoldStyle() => IsBold;
    public bool IsItalicStyle() => IsItalic;
    public bool IsPlainStyle() => IsPlain;

    public OFDFont DeriveFont(int style) => new(Name, style, Size);
    public OFDFont DeriveFont(double size) => new(Name, Style, size);
    public OFDFont DeriveFont(int style, double size) => new(Name, style, size);

    public OFDFont GetBold() => new(Name, Style | BOLD, Size);
    public OFDFont GetItalic() => new(Name, Style | ITALIC, Size);
    public OFDFont GetPlain() => new(Name, PLAIN, Size);

    public string GetFontName()
    {
        var styleStr = (IsBold ? "Bold " : "") + (IsItalic ? "Italic " : "");
        return $"{styleStr}{Size:F1}mm {Name}";
    }

    public override string ToString() => $"Font[name={Name}, style={StyleString()}, size={Size:F1}]";

    private string StyleString() => Style switch
    {
        PLAIN => "plain",
        BOLD => "bold",
        ITALIC => "italic",
        BOLD | ITALIC => "bolditalic",
        _ => "plain"
    };

    public override bool Equals(object? obj)
    {
        if (obj is OFDFont f) return f.Name == Name && f.Style == Style && Math.Abs(f.Size - Size) < 0.001;
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(Name, Style, Size);

    public static OFDFont Decode(string str)
    {
        // 解析 CSS font 格式: "bold 14px Times New Roman"
        var parts = str.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return new OFDFont("Dialog", PLAIN, 12);

        int style = PLAIN;
        double size = 12;
        string name = "Dialog";

        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower is "bold") style |= BOLD;
            else if (lower is "italic" or "oblique") style |= ITALIC;
            else if (lower.EndsWith("pt"))
            {
                if (double.TryParse(part[..^2], out var pt)) size = pt * 0.352778; // pt -> mm
            }
            else if (lower.EndsWith("px"))
            {
                if (double.TryParse(part[..^2], out var px)) size = px / 3.78; // px -> mm
            }
            else if (lower.EndsWith("mm"))
            {
                if (double.TryParse(part[..^2], out var mm)) size = mm;
            }
            else if (double.TryParse(part, out var num))
            {
                size = num;
            }
            else
            {
                name = part;
            }
        }
        return new OFDFont(name, style, size);
    }

    public static OFDFont SansSerif => new("sanserif", PLAIN, 12);
    public static OFDFont Serif => new("serif", PLAIN, 12);
    public static OFDFont Monospaced => new("monospaced", PLAIN, 12);
}