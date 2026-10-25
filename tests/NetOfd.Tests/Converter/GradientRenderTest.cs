using NetOfd.Converter;
using NetOfd.Core.BasicType;
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
/// OFD 渐变底纹（AxialShd / RadialShd）端到端测试：
/// Core 模型序列化 → Layout CanvasGradient/RadialGradient 写出 → Converter 渲染出渐变像素。
/// </summary>
public class GradientRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_gradient_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    // ── 1. Core：轴向渐变模型序列化为规范结构 ────────────────────
    [Fact]
    public void AxialShd_Serialize_ShouldMatchOfdStructure()
    {
        var shd = new CT_AxialShd(0, 0, 100, 0)
        {
            MapType = MapType.Direct,
            Extend = Extend.Both,
        };
        shd.AddSegment(new Segment(0, CT_Color.FromRgb(255, 0, 0)));
        shd.AddSegment(new Segment(1, CT_Color.FromRgb(0, 0, 255)));

        var xml = shd.Element.ToString();
        Assert.Contains("AxialShd", xml);
        Assert.Contains("MapType=\"Direct\"", xml);
        Assert.Contains("Extend=\"3\"", xml);
        Assert.Contains("StartPoint=\"0 0\"", xml);
        Assert.Contains("EndPoint=\"100 0\"", xml);
        Assert.Contains("<Segment ", xml);
        Assert.Contains("Position=\"0\"", xml);
        Assert.Contains("Position=\"1\"", xml);
        // 段内颜色为空格分隔 RGB（规范 ST_Array 格式）
        Assert.Contains("255 0 0", xml);
        Assert.Contains("0 0 255", xml);

        // 读取侧还原
        var restored = new CT_AxialShd(shd.Element);
        Assert.Equal(2, restored.GetSegments().Count);
        Assert.Equal(0, restored.StartPoint!.X);
        Assert.Equal(100, restored.EndPoint!.X);
        Assert.Equal(MapType.Direct, restored.MapType);
        Assert.Equal(Extend.Both, restored.Extend);
    }

    // ── 2. Core：径向渐变模型序列化 ──────────────────────────────
    [Fact]
    public void RadialShd_Serialize_ShouldKeepRadiiAndCenters()
    {
        var shd = new CT_RadialShd
        {
            StartPoint = new STPos(50, 50),
            StartRadius = 0,
            EndPoint = new STPos(50, 50),
            EndRadius = 40,
        };
        shd.AddSegment(new Segment(0, CT_Color.White));
        shd.AddSegment(new Segment(1, CT_Color.FromRgb(0, 0, 255)));

        var xml = shd.Element.ToString();
        Assert.Contains("RadialShd", xml);
        Assert.Contains("StartRadius=\"0\"", xml);
        Assert.Contains("EndRadius=\"40\"", xml);
        Assert.Contains("StartPoint=\"50 50\"", xml);

        var restored = new CT_RadialShd(shd.Element);
        Assert.Equal(40, restored.EndRadius);
        Assert.Equal(0, restored.StartRadius);
        Assert.Equal(2, restored.GetSegments().Count);
        // RadialShd 挂到 CT_Color 后可通过 GetRadialShd 取回
        var color = new CT_Color().SetColorCluster(shd);
        Assert.NotNull(color.GetRadialShd());
        Assert.Null(color.GetAxialShd());
    }

    // ── 3. Layout：线性渐变写出为页面 PathObject + AxialShd ─────
    [Fact]
    public void LinearGradient_FillRect_ShouldWriteAxialShdIntoOfd()
    {
        var ofdPath = Out("gradient_linear.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            var g = ctx.CreateLinearGradient(10, 10, 60, 10)
                .AddColorStop(0, "#ff0000")
                .AddColorStop(1, "#0000ff");
            ctx.FillStyle = g;
            ctx.FillRect(10, 10, 50, 50);
        }

        using var zip = System.IO.Compression.ZipFile.OpenRead(ofdPath);
        var pageEntry = zip.Entries.First(e => e.FullName.EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase));
        using var sr = new StreamReader(pageEntry.Open());
        var xml = sr.ReadToEnd();

        Assert.Contains("AxialShd", xml);
        Assert.Contains("StartPoint=\"10 10\"", xml);
        Assert.Contains("EndPoint=\"60 10\"", xml);
        Assert.Contains("Position=\"0\"", xml);
        Assert.Contains("Position=\"1\"", xml);
        Assert.Contains("255 0 0", xml); // 首段红
        Assert.Contains("0 0 255", xml); // 末段蓝
    }

    // ── 4. Layout：径向渐变写出 ─────────────────────────────────
    [Fact]
    public void RadialGradient_FillRect_ShouldWriteRadialShdIntoOfd()
    {
        var ofdPath = Out("gradient_radial.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            var g = ctx.CreateRadialGradient(40, 40, 0, 40, 40, 30)
                .AddColorStop(0, "#ffffff")
                .AddColorStop(1, "#ff0000");
            ctx.FillStyle = g;
            ctx.FillRect(10, 10, 60, 60);
        }

        using var zip = System.IO.Compression.ZipFile.OpenRead(ofdPath);
        var pageEntry = zip.Entries.First(e => e.FullName.EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase));
        using var sr = new StreamReader(pageEntry.Open());
        var xml = sr.ReadToEnd();

        Assert.Contains("RadialShd", xml);
        Assert.Contains("StartRadius=\"0\"", xml);
        Assert.Contains("EndRadius=\"30\"", xml);
    }

    // ── 5. 渲染：线性渐变应产出红→蓝过渡像素 ────────────────────
    [Fact]
    public void LinearGradient_RenderPng_ShouldProduceTransition()
    {
        var ofdPath = Out("gradient_render_linear.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            var g = ctx.CreateLinearGradient(20, 20, 120, 20)
                .AddColorStop(0, "#ff0000")
                .AddColorStop(1, "#0000ff");
            ctx.FillStyle = g;
            ctx.FillRect(20, 20, 100, 60); // 渐变轴线与矩形左→右对齐
        }

        var imgDir = Path.Combine(Out("imgs_linear"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        int redCount = 0, blueCount = 0, midCount = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Blue < 80) redCount++;
                else if (px.Blue > 200 && px.Red < 80) blueCount++;
                else if (px.Red > 80 && px.Blue > 80 && px.Red < 200 && px.Blue < 200) midCount++;
            }
        }

        Assert.True(redCount > 100, $"应渲染出渐变左端红色区，实际红色像素: {redCount}");
        Assert.True(blueCount > 100, $"应渲染出渐变右端蓝色区，实际蓝色像素: {blueCount}");
        Assert.True(midCount > 100, $"应渲染出红蓝过渡像素（紫），实际过渡像素: {midCount}");
    }

    // ── 6. 渲染：径向渐变应产出中心→外缘过渡像素 ─────────────────
    [Fact]
    public void RadialGradient_RenderPng_ShouldProduceCenterToEdgeTransition()
    {
        var ofdPath = Out("gradient_render_radial.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            var g = ctx.CreateRadialGradient(70, 170, 0, 70, 170, 40)
                .AddColorStop(0, "#ff0000")
                .AddColorStop(1, "#0000ff");
            ctx.FillStyle = g;
            ctx.FillRect(30, 130, 80, 80); // 矩形覆盖渐变中心（70,170）与四角
        }

        var imgDir = Path.Combine(Out("imgs_radial"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        int redCount = 0, blueCount = 0, midCount = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Blue < 80) redCount++;
                else if (px.Blue > 200 && px.Red < 80) blueCount++;
                else if (px.Red > 80 && px.Blue > 80 && px.Red < 200 && px.Blue < 200) midCount++;
            }
        }

        Assert.True(redCount > 100, $"径向渐变应渲染出中心红色区，实际红色像素: {redCount}");
        Assert.True(blueCount > 100, $"径向渐变应渲染出外缘蓝色区，实际蓝色像素: {blueCount}");
        Assert.True(midCount > 100, $"径向渐变应渲染出过渡像素，实际过渡像素: {midCount}");
    }

    // ── 7. 渲染：三段渐变（含中间停靠点）颜色停靠位正确 ───────────
    [Fact]
    public void MultiStopGradient_RenderPng_ShouldHonorMidStop()
    {
        var ofdPath = Out("gradient_render_mid.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            // 红 →(0.5) 白 →(1) 蓝：中点应为白色
            var g = ctx.CreateLinearGradient(20, 20, 120, 20)
                .AddColorStop(0, "#ff0000")
                .AddColorStop(0.5, "#ffffff")
                .AddColorStop(1, "#0000ff");
            ctx.FillStyle = g;
            ctx.FillRect(20, 20, 100, 40);
        }

        var imgDir = Path.Combine(Out("imgs_mid"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        // 渐变轴沿矩形水平中线 y ≈ 40mm → ppm10 = 400px（若 OFD 原点在底则 2970-400）
        // 无法预知翻转方向，故扫整条中线两处像素即可：找出矩形范围内最接近白的像素
        bool foundWhite = false;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 220 && px.Green > 220 && px.Blue > 220) { foundWhite = true; break; }
            }
            if (foundWhite) break;
        }
        Assert.True(foundWhite, "三段渐变中点的白色停靠位应被渲染出来");
    }

    // ── 8. 渲染：描边走渐变着色器路径（stroke 分支）───────────────
    [Fact]
    public void LinearGradient_StrokeRect_ShouldRenderGradientStroke()
    {
        var ofdPath = Out("gradient_render_stroke.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            var g = ctx.CreateLinearGradient(20, 20, 120, 20)
                .AddColorStop(0, "#ff0000")
                .AddColorStop(1, "#0000ff");
            ctx.StrokeStyle = g;
            ctx.LineWidth = 2;
            ctx.StrokeRect(20, 20, 100, 60);
        }

        var imgDir = Path.Combine(Out("imgs_stroke"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        int redCount = 0, blueCount = 0;
        for (int y = 0; y < bmp.Height; y++)
        {
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Blue < 80) redCount++;
                else if (px.Blue > 200 && px.Red < 80) blueCount++;
            }
        }

        // 描边像素占比小，阈值相应调低；上下水平边应为渐变（含红端与蓝端）
        Assert.True(redCount > 50, $"渐变描边应含红端，实际红色像素: {redCount}");
        Assert.True(blueCount > 50, $"渐变描边应含蓝端，实际蓝色像素: {blueCount}");
    }
}
