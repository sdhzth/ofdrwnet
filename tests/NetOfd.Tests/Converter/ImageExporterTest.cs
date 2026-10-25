using System;
using System.IO;
using System.Linq;
using NetOfd.Converter;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// OFD → 图像 导出测试（对应 Java ofdrw-converter ImageExporter 功能）。
/// 验证：生成 OFD → 渲染为 PNG → 文件存在且尺寸正确。
/// </summary>
public class ImageExporterTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_converter_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    // ── 1. 通过布局引擎生成含文字的 OFD，并渲染为 PNG ─────────────
    [Fact]
    public void LayoutDoc_ExportToPng_ShouldProduceImage()
    {
        var ofdPath = Out("layout_export.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.Add(new Paragraph().AddText("你好，OFD 图像导出测试！").SetFontSize(12d));
            doc.Add(new Paragraph().AddText("English text rendering check.").SetFontSize(8d));
            doc.Add(new Paragraph().AddText("第二段：这是 netofd .NET 移植版渲染验证").SetFontSize(6d));
        }

        var imgDir = Path.Combine(Out("imgs_layout"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 15);
        var paths = exporter.Export();

        Assert.Single(paths);
        Assert.True(File.Exists(paths[0]), "应生成 PNG 文件");

        // 210mm * 15ppm = 3150; 297mm * 15ppm = 4455
        var info = SKBitmapLoader.Load(paths[0]);
        Assert.Equal(3150, info.Width);
        Assert.Equal(4455, info.Height);
    }

    // ── 2. 通过 DrawContext 生成含路径/文字的 OFD，并渲染 ─────────
    [Fact]
    public void CanvasDoc_ExportToPng_ShouldRenderShapes()
    {
        var ofdPath = Out("canvas_export.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));

            // 填充矩形
            ctx.FillStyle = "#ff0000";
            ctx.FillRect(10, 10, 100, 50);
            // 描边矩形
            ctx.StrokeStyle = "#0000ff";
            ctx.LineWidth = 1;
            ctx.StrokeRect(10, 80, 100, 50);
            // 文字（DrawContext 路径不带 Boundary，使用绝对坐标）
            ctx.FillStyle = "#000000";
            ctx.Font = "12mm SimSun";
            ctx.FillText("测试矩形与文字", 10, 160);
        }

        var imgDir = Path.Combine(Out("imgs_canvas"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();

        Assert.Single(paths);
        Assert.True(File.Exists(paths[0]));

        // 210*10=2100; 297*10=2970
        var info = SKBitmapLoader.Load(paths[0]);
        Assert.Equal(2100, info.Width);
        Assert.Equal(2970, info.Height);

        // 验证红色填充矩形确实渲染到画布上（按像素统计，避免依赖具体坐标翻转）
        using var bmp = SkiaSharp.SKBitmap.Decode(paths[0]);
        int redCount = 0;
        for (int y = 0; y < bmp.Height; y++)
            for (int x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 200 && px.Green < 60 && px.Blue < 60) redCount++;
            }
        Assert.True(redCount > 100, $"应渲染出红色填充矩形，实际红色像素数: {redCount}");
    }

    // ── 3. 自定义 DPI / PPM ────────────────────────────────────
    [Fact]
    public void Export_WithCustomPPM_ShouldMatchDimensions()
    {
        var ofdPath = Out("ppm_export.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.Add(new Paragraph().AddText("分辨率测试").SetFontSize(10d));
        }

        var imgDir = Path.Combine(Out("imgs_ppm"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 8);
        exporter.SetDPI(200); // 200 DPI ≈ 7.874 ppm
        var paths = exporter.Export();

        Assert.Single(paths);
        var info = SKBitmapLoader.Load(paths[0]);
        // 210 * 7.874 ≈ 1653.5 -> 1654; 297 * 7.874 ≈ 2338.6 -> 2339
        Assert.Equal(1654, info.Width);
        Assert.Equal(2339, info.Height);
    }

    // ── 4. 多页文档逐页导出 ────────────────────────────────────
    [Fact]
    public void MultiPageDoc_ExportAllPages_ShouldProduceOnePerPage()
    {
        var ofdPath = Out("multipage_export.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            // 第 1 页
            doc.AddPage();
            var ctx0 = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx0.FillText("第 1 页内容", 10, 50);
            // 第 2 页
            doc.AddPage();
            var ctx1 = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx1.FillText("第 2 页内容", 10, 50);
        }

        var imgDir = Path.Combine(Out("imgs_multi"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 15);
        var paths = exporter.Export();

        Assert.Equal(2, paths.Count);
        Assert.All(paths, p => Assert.True(File.Exists(p)));
        Assert.Equal(3150, SKBitmapLoader.Load(paths[0]).Width);
        Assert.Equal(4455, SKBitmapLoader.Load(paths[1]).Height);
    }

    // ── 并行导出：多页文档应产出与页数一致且有效的 PNG ─────────────
    [Fact]
    public void ParallelExport_MultiPage_ShouldProduceAllPages()
    {
        var ofdPath = Out("parallel_export.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            doc.AddPage();
            doc.AddPage();
            doc.Add(new Paragraph().AddText("并行导出测试").SetFontSize(10d));
        }

        var imgDir = Path.Combine(Out("imgs_parallel"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 8) { ParallelExport = true };
        var paths = exporter.Export();

        Assert.Equal(3, paths.Count);
        Assert.All(paths, p => Assert.True(File.Exists(p), "应生成 PNG 文件"));
        Assert.All(paths, p => Assert.True(SKBitmapLoader.Load(p).Width > 0, "PNG 应可解码"));
    }
}

/// <summary>
/// 轻量帮助类：读取 PNG 尺寸（避免在本测试直接依赖 SKBitmap 的 Dispose 细节）。
/// </summary>
internal static class SKBitmapLoader
{
    public static (int Width, int Height) Load(string path)
    {
        using var stream = File.OpenRead(path);
        using var codec = SkiaSharp.SKCodec.Create(stream);
        var info = codec.Info;
        return (info.Width, info.Height);
    }
}
