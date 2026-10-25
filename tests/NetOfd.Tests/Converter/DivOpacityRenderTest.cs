using NetOfd.Converter;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// Div.Opacity（对齐 Java Div.setOpacity）端到端测试：
/// Layout 写出 PathObject Alpha → Converter 渲染按 Alpha 与下层内容混合。
/// </summary>
public class DivOpacityRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_opacity_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>
    /// 渲染：不透明红底 + 半透明蓝 div（opacity 0.5）叠加，
    /// 中心应呈 (≈127, 0, ≈127) 混合色而非纯蓝 (0,0,255)。
    /// </summary>
    [Fact]
    public void DivOpacity_RenderPng_ShouldBlendOverUnderlyingContent()
    {
        var ofdPath = Out("opacity_blend.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            // 底层：不透明红铺满
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(20, 20)
                .SetWidth(170)
                .SetHeight(257)
                .SetBackgroundColor(CT_Color.Red));
            // 顶层：半透明蓝（0.5）
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(60, 60)
                .SetWidth(90)
                .SetHeight(90)
                .SetBackgroundColor(CT_Color.Blue)
                .SetOpacity(0.5));
            doc.AddVPage(vPage);
        }

        var imgDir = Path.Combine(Out("imgs_blend"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        // 蓝 div 覆盖区（60..150mm → ppm10 = 600..1500px；若 Y 翻转则互补区间），
        // 扫描全图找红蓝 1:1 混合像素；若 Alpha 未生效则只会出现纯蓝或纯红。
        bool foundBlend = false;
        for (int y = 0; y < bmp.Height && !foundBlend; y += 2)
        {
            for (int x = 0; x < bmp.Width && !foundBlend; x += 2)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red > 100 && px.Red < 160
                    && px.Green < 40
                    && px.Blue > 100 && px.Blue < 160)
                    foundBlend = true;
            }
        }
        Assert.True(foundBlend, "半透明蓝叠红底应渲染出 (≈127,0,≈127) 混合色");
    }

    /// <summary>
    /// opacity = 1（钳制后）：不应写 Alpha 属性，渲染结果应为纯蓝不透明（与无 opacity 一致）。
    /// </summary>
    [Fact]
    public void DivOpacity_One_ShouldRenderOpaquePureColor()
    {
        var ofdPath = Out("opacity_one.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(20, 20)
                .SetWidth(170)
                .SetHeight(257)
                .SetBackgroundColor(CT_Color.Red));
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(60, 60)
                .SetWidth(90)
                .SetHeight(90)
                .SetBackgroundColor(CT_Color.Blue)
                .SetOpacity(1));
            doc.AddVPage(vPage);
        }

        var imgDir = Path.Combine(Out("imgs_one"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        // 中心区应为纯蓝：红底被完全覆盖
        bool foundPureBlue = false;
        for (int y = 0; y < bmp.Height && !foundPureBlue; y += 2)
        {
            for (int x = 0; x < bmp.Width && !foundPureBlue; x += 2)
            {
                var px = bmp.GetPixel(x, y);
                if (px.Red < 40 && px.Green < 40 && px.Blue > 215)
                    foundPureBlue = true;
            }
        }
        Assert.True(foundPureBlue, "opacity=1 的蓝 div 应完全覆盖红底呈纯蓝");
    }
}
