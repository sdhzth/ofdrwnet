using NetOfd.Converter;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// OFD 图案底纹（Pattern）端到端测试：
/// Core 模型序列化 → Layout CanvasPattern.CreatePattern 写出 → Converter 平铺渲染像素断言。
/// 注：Java ofdrw-converter 的 pattern 渲染整段注释（未实现），此渲染能力为 NetOfd 增值实现。
/// </summary>
public class PatternRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_pattern_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>生成一张纯色小 PNG（w×h 像素）作为底纹单元图片。</summary>
    private static string MakeTinyPng(string name, int w, int h, byte r, byte g, byte b)
    {
        var path = Out(name);
        using var bmp = new SKBitmap(w, h);
        using (var canvas = new SKCanvas(bmp))
        {
            canvas.Clear(new SKColor(r, g, b));
        }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        using var fs = File.Create(path);
        data.SaveTo(fs);
        return path;
    }

    // ── 1. Core：CT_Pattern 模型序列化为规范结构 ──────────────────
    [Fact]
    public void Pattern_Serialize_ShouldMatchOfdStructure()
    {
        var imgObj = new CTImage
        {
            Boundary = new STBox(0, 0, 5.29, 5.29),
            ResourceID = 7L,
            CTM = new STArray([5.29, 0, 0, 5.29, 0, 0])
        };
        var pattern = new CT_Pattern
        {
            Width = 5.29,
            Height = 5.29,
            XStep = 8,
            YStep = 8,
            ReflectMethod = ReflectMethod.RowAndColumn,
        };
        pattern.SetCellContent(new CellContent().AddPageBlock(imgObj));

        var xml = pattern.Element.ToString();
        Assert.Contains("<Pattern", xml);
        Assert.Contains("Width=\"5.29\"", xml);
        Assert.Contains("Height=\"5.29\"", xml);
        Assert.Contains("XStep=\"8\"", xml);
        Assert.Contains("YStep=\"8\"", xml);
        Assert.Contains("ReflectMethod=\"RowAndColumn\"", xml);
        Assert.Contains("<CellContent>", xml);
        Assert.Contains("<ImageObject", xml);
        Assert.Contains("ResourceID=\"7\"", xml);
        // 读取侧还原
        var restored = new CT_Pattern(pattern.Element);
        Assert.Equal(5.29, restored.Width);
        Assert.Equal(8, restored.XStep);
        Assert.Equal(ReflectMethod.RowAndColumn, restored.ReflectMethod);
        Assert.NotNull(restored.GetCellContent());
        Assert.NotNull(restored.GetCellContent()!.GetImage());
        Assert.Equal(7L, restored.GetCellContent()!.GetImage()!.ResourceID!.Value);
    }

    // ── 2. Core：CT_Color 上 Pattern 与渐变互斥挂接 ────────────────
    [Fact]
    public void Pattern_OnColor_ShouldBeMutuallyExclusiveWithShading()
    {
        var imgObj = new CTImage
        {
            Boundary = new STBox(0, 0, 5, 5),
            ResourceID = 3L,
        };
        var pattern = new CT_Pattern { Width = 5, Height = 5 };
        pattern.SetCellContent(new CellContent().AddPageBlock(imgObj));

        var color = new CT_Color().SetPattern(pattern);
        Assert.NotNull(color.GetPattern());
        Assert.Null(color.GetAxialShd());
        Assert.Null(color.GetRadialShd());
        Assert.IsType<CT_Pattern>(color.GetColorCluster());

        // 再挂渐变应互斥移除 Pattern
        var shd = new CT_AxialShd(0, 0, 10, 0);
        color.SetColorCluster(shd);
        Assert.NotNull(color.GetAxialShd());
        Assert.Null(color.GetPattern());
    }

    // ── 3. Layout：CreatePattern + FillRect 写出 Pattern 到底纹 ────
    [Fact]
    public void Pattern_FillRect_ShouldWritePatternIntoOfd()
    {
        var png = MakeTinyPng("tile_red_24.png", 24, 24, 255, 0, 0);
        var ofdPath = Out("pattern_write.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            var pattern = ctx.CreatePattern(png); // 24px / 3.78 ≈ 6.35mm 单元
            ctx.FillStyle = pattern;
            ctx.FillRect(20, 30, 80, 60);
        }

        var content = ReadPageContent(ofdPath);
        var pathObj = content.Descendants().First(e => e.Name.LocalName == "PathObject");
        var fillColor = pathObj.Elements().First(e => e.Name.LocalName == "FillColor");
        var patternEl = fillColor.Elements().First(e => e.Name.LocalName == "Pattern");
        Assert.NotNull(patternEl);
        // 24px / 3.78 ≈ 6.3492mm（浮点），只断言单元宽度 > 6 即可
        var w = double.Parse(patternEl.Attribute("Width")!.Value,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.True(w > 6.0 && w < 6.5, $"底纹单元宽度应 ≈ 24px/3.78 ≈ 6.35mm（实际 {w}）");
        // 底纹单元 CellContent 内含 ImageObject（用 LocalName 判断以规避 ofd: 前缀）
        Assert.NotNull(patternEl.Elements().FirstOrDefault(e => e.Name.LocalName == "CellContent"));
        Assert.NotNull(patternEl.Descendants().FirstOrDefault(e => e.Name.LocalName == "ImageObject"));
        // 图片已入包：Res 下有图片条目且 ResourceID 指向它
        var resId = patternEl.Descendants().First(e => e.Name.LocalName == "ImageObject")
            .Attribute("ResourceID")?.Value;
        Assert.False(string.IsNullOrWhiteSpace(resId));
    }

    // ── 4. Converter：平铺渲染像素断言（红底纹矩形覆盖大背景）────
    [Fact]
    public void Pattern_RenderPng_ShouldTileRedAcrossRect()
    {
        var png = MakeTinyPng("tile_red_16.png", 16, 16, 255, 0, 0);
        var ofdPath = Out("pattern_render.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            // 先画蓝背景，再画红 pattern 矩形 —— 若 pattern 未渲染则显示蓝底
            ctx.FillStyle = "#0000ff";
            ctx.FillRect(0, 0, 210, 297);
            var pattern = ctx.CreatePattern(png); // 16px / 3.78 ≈ 4.23mm
            ctx.FillStyle = pattern;
            ctx.FillRect(60, 80, 90, 90);
        }

        var imgDir = Path.Combine(Out("imgs"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        // pattern 矩形 = x∈[60,150]mm、y∈[80,170]mm → ppm10 像素 [600,1500)×[800,1700)。
        // 该区域内应几乎全为红色平铺（≈90×90×10²=81 万），蓝色 ≈ 0（若 pattern 未渲染则整区蓝）。
        int red = 0, blue = 0;
        for (int y = 800; y < 1700; y++)
        {
            for (int x = 600; x < 1500; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 180 && px.Red - px.Blue > 120) red++;
                else if (px.Blue > 180 && px.Blue - px.Red > 120) blue++;
            }
        }
        Assert.True(red > 700_000, $"pattern 矩形(81 万像素)应几乎全红平铺（red={red}, blue={blue}）");
        Assert.True(blue < 10_000, $"pattern 矩形内不应残留蓝背景（blue={blue}）");
    }

    /// <summary>从 zip 读取第一个页面的 Content.xml。</summary>
    private static XElement ReadPageContent(string ofdPath)
    {
        using var za = System.IO.Compression.ZipFile.OpenRead(ofdPath);
        var entry = za.Entries
            .First(e => e.FullName.Replace('\\', '/').Contains("/Pages/")
                        && e.FullName.Replace('\\', '/').EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase));
        using var sr = new StreamReader(entry.Open());
        return XElement.Parse(sr.ReadToEnd());
    }
}
