using System;
using System.Globalization;

namespace NetOfd.Graphics2D.AWT;

/// <summary>
/// 跨平台 OFD 颜色实现（替代 Java AWT Color）
/// </summary>
public class OFDColor
{
    public static readonly OFDColor White = new(255, 255, 255);
    public static readonly OFDColor LightGray = new(192, 192, 192);
    public static readonly OFDColor Gray = new(128, 128, 128);
    public static readonly OFDColor DarkGray = new(64, 64, 64);
    public static readonly OFDColor Black = new(0, 0, 0);
    public static readonly OFDColor Red = new(255, 0, 0);
    public static readonly OFDColor Green = new(0, 255, 0);
    public static readonly OFDColor Blue = new(0, 0, 255);
    public static readonly OFDColor Yellow = new(255, 255, 0);
    public static readonly OFDColor Cyan = new(0, 255, 255);
    public static readonly OFDColor Magenta = new(255, 0, 255);
    public static readonly OFDColor Pink = new(255, 175, 175);

    public int R { get; }
    public int G { get; }
    public int B { get; }
    public int A { get; }

    public OFDColor(int r, int g, int b, int a = 255)
    {
        R = Math.Clamp(r, 0, 255);
        G = Math.Clamp(g, 0, 255);
        B = Math.Clamp(b, 0, 255);
        A = Math.Clamp(a, 0, 255);
    }

    public int GetRed() => R;
    public int GetGreen() => G;
    public int GetBlue() => B;
    public int GetAlpha() => A;

    public int GetRGB() => (A << 24) | (R << 16) | (G << 8) | B;

    public float GetRedF() => R / 255f;
    public float GetGreenF() => G / 255f;
    public float GetBlueF() => B / 255f;
    public float GetAlphaF() => A / 255f;

    public OFDColor Brighter()
    {
        int r = Math.Min(255, (int)(R * 1.3));
        int g = Math.Min(255, (int)(G * 1.3));
        int b = Math.Min(255, (int)(B * 1.3));
        return new OFDColor(r, g, b, A);
    }

    public OFDColor Darker()
    {
        int r = Math.Max(0, (int)(R * 0.7));
        int g = Math.Max(0, (int)(G * 0.7));
        int b = Math.Max(0, (int)(B * 0.7));
        return new OFDColor(r, g, b, A);
    }

    public string ToHexString() => A == 255
        ? $"#{R:X2}{G:X2}{B:X2}"
        : $"#{R:X2}{G:X2}{B:X2}{A:X2}";

    /// <summary>
    /// 转为 OFD <c>Color/@Value</c> 字符串。
    /// <para>GB/T 33190 §7.4 规定通道值以<b>空格</b>分隔（如 <c>"0 0 0"</c>）；
    /// 旧实现用逗号（<c>"0,0,0"</c>），会让 ofdrw / ofdgo（pdfcpu 管线）报
    /// “expected 3 color components”而整页渲染失败。</para>
    /// </summary>
    public string ToRgbString() => A == 255
        ? $"{R} {G} {B}"
        : $"{R} {G} {B} {A}";

    public override string ToString() => $"#{R:X2}{G:X2}{B:X2}";

    public override bool Equals(object? obj)
    {
        if (obj is OFDColor c) return c.R == R && c.G == G && c.B == B && c.A == A;
        return false;
    }

    public override int GetHashCode() => GetRGB();

    public static OFDColor GetColor(string hex)
    {
        hex = hex.TrimStart('#');
        int r, g, b, a = 255;
        if (hex.Length == 6)
        {
            r = int.Parse(hex[..2], NumberStyles.HexNumber);
            g = int.Parse(hex[2..4], NumberStyles.HexNumber);
            b = int.Parse(hex[4..6], NumberStyles.HexNumber);
        }
        else if (hex.Length == 8)
        {
            r = int.Parse(hex[..2], NumberStyles.HexNumber);
            g = int.Parse(hex[2..4], NumberStyles.HexNumber);
            b = int.Parse(hex[4..6], NumberStyles.HexNumber);
            a = int.Parse(hex[6..8], NumberStyles.HexNumber);
        }
        else return Black;
        return new OFDColor(r, g, b, a);
    }

    public static OFDColor GetColor(int rgb) => new(
        (rgb >> 16) & 0xFF,
        (rgb >> 8) & 0xFF,
        rgb & 0xFF,
        (rgb >> 24) & 0xFF);

    public static OFDColor Decode(string nm) => GetColor(nm);

    public static OFDColor HSBtoRGB(float hue, float saturation, float brightness)
    {
        int r, g, b;
        if (saturation == 0)
            r = g = b = (int)(brightness * 255);
        else
        {
            int i = (int)(hue * 6);
            float f = hue * 6 - i;
            float p = brightness * (1 - saturation);
            float q = brightness * (1 - saturation * f);
            float t = brightness * (1 - saturation * (1 - f));
            switch (i % 6)
            {
                case 0: r = (int)(brightness * 255); g = (int)(t * 255); b = (int)(p * 255); break;
                case 1: r = (int)(q * 255); g = (int)(brightness * 255); b = (int)(p * 255); break;
                case 2: r = (int)(p * 255); g = (int)(brightness * 255); b = (int)(t * 255); break;
                case 3: r = (int)(p * 255); g = (int)(q * 255); b = (int)(brightness * 255); break;
                case 4: r = (int)(t * 255); g = (int)(p * 255); b = (int)(brightness * 255); break;
                default: r = (int)(brightness * 255); g = (int)(p * 255); b = (int)(q * 255); break;
            }
        }
        return new OFDColor(r, g, b);
    }

    public static float[] RGBtoHSB(int r, int g, int b, float[] hsbvals)
    {
        hsbvals ??= new float[3];
        float hue, saturation, brightness;
        int cmax = Math.Max(r, Math.Max(g, b));
        int cmin = Math.Min(r, Math.Min(g, b));
        brightness = cmax / 255f;
        if (cmax != 0)
            saturation = (cmax - cmin) / (float)cmax;
        else
            saturation = 0;
        if (saturation == 0)
            hue = 0;
        else
        {
            float redc = (cmax - r) / (float)(cmax - cmin);
            float greenc = (cmax - g) / (float)(cmax - cmin);
            float bluec = (cmax - b) / (float)(cmax - cmin);
            if (r == cmax)
                hue = bluec - greenc;
            else if (g == cmax)
                hue = 2f + redc - bluec;
            else
                hue = 4f + greenc - redc;
            hue /= 6f;
            if (hue < 0)
                hue += 1f;
        }
        hsbvals[0] = hue;
        hsbvals[1] = saturation;
        hsbvals[2] = brightness;
        return hsbvals;
    }
}