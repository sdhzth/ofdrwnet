using System;
using System.Collections.Generic;
using System.IO;

namespace NetOfd.Graphics2D.AWT;

/// <summary>
/// Alpha 合成模式（替代 Java AWT AlphaComposite）
/// </summary>
public class AlphaComposite : IPaint
{
    public int Rule { get; }
    public float AlphaValue { get; }

    public const int CLEAR = 1;
    public const int SRC = 2;
    public const int SRC_OVER = 3;
    public const int DST_OVER = 4;
    public const int SRC_IN = 5;
    public const int DST_IN = 6;
    public const int SRC_OUT = 7;
    public const int DST_OUT = 8;
    public const int SRC_ATOP = 9;
    public const int DST_ATOP = 10;
    public const int XOR = 11;

    public static AlphaComposite SrcOver => new(SRC_OVER, 1.0f);
    public static AlphaComposite Clear => new(CLEAR, 0.0f);

    private AlphaComposite(int rule, float alpha)
    { Rule = rule; AlphaValue = alpha; }

    public static AlphaComposite GetInstance(int rule) => new(rule, 1.0f);
    public static AlphaComposite GetInstance(int rule, float alpha) => new(rule, alpha);

    public float GetAlpha() => AlphaValue;
    public int GetRule() => Rule;

    public AlphaComposite Derive(float alpha) => new(Rule, alpha);
}

/// <summary>
/// 渲染提示（替代 Java AWT RenderingHints）
/// </summary>
public class RenderingHints
{
    private readonly Dictionary<object, object> _hints = new();

    public static readonly object KEY_ANTIALIASING = "ANTIALIASING";
    public static readonly object KEY_TEXT_ANTIALIASING = "TEXT_ANTIALIASING";
    public static readonly object KEY_FRACTIONALMETRICS = "FRACTIONAL_METRICS";

    public static readonly object VALUE_ANTIALIAS_ON = "ON";
    public static readonly object VALUE_ANTIALIAS_OFF = "OFF";
    public static readonly object VALUE_ANTIALIAS_DEFAULT = "DEFAULT";
    public static readonly object VALUE_TEXT_ANTIALIAS_ON = "TEXT_ON";
    public static readonly object VALUE_TEXT_ANTIALIAS_OFF = "TEXT_OFF";
    public static readonly object VALUE_TEXT_ANTIALIAS_DEFAULT = "TEXT_DEFAULT";
    public static readonly object VALUE_FRACTIONAL_METRICS_ON = "FRAC_ON";
    public static readonly object VALUE_FRACTIONAL_METRICS_OFF = "FRAC_OFF";

    public RenderingHints(object? initialKey)
    {
        // Empty hints by default
    }

    public void Put(object key, object value) => _hints[key] = value;
    public object? Get(object key) => _hints.TryGetValue(key, out var v) ? v : null;
    public void Clear() => _hints.Clear();
    public void Add(RenderingHints h) { foreach (var kv in h._hints) _hints[kv.Key] = kv.Value; }
    public RenderingHints Clone() { var r = new RenderingHints(null); r.Add(this); return r; }
    public int Count => _hints.Count;
}

/// <summary>
/// 透明度常量
/// </summary>
public static class Transparency
{
    public const int OPAQUE = 1;
    public const int BITMASK = 2;
    public const int TRANSLUCENT = 3;
}

/// <summary>
/// 字体规格（替代 Java AWT FontMetrics）
/// </summary>
public class FontMetrics
{
    public OFDFont Font { get; }

    public FontMetrics(OFDFont font) => Font = font;

    public int GetHeight() => (int)(Font.Size * 1.2);
    public int GetAscent() => (int)(Font.Size * 0.8);
    public int GetDescent() => (int)(Font.Size * 0.2);
    public int GetLeading() => (int)(Font.Size * 0.1);

    public int StringWidth(string str) => (int)(str.Length * Font.Size * 0.6);

    public override string ToString() => $"FontMetrics[height={GetHeight()}, ascent={GetAscent()}]";
}