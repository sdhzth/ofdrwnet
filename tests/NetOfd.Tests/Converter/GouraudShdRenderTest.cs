using NetOfd.Converter;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// 高洛德渐变（GouraudShd/LaGouraudShd）渲染端到端测试：
/// Layout FillStyle 挂 CT_Color(GouraudShd) → FillRect 写出 → Converter 以
/// SKVertices 顶点色插值渲染。按已知双线性插值解析结果在毫米→像素坐标采样断言。
/// 注：Java ofdrw 的 layout/converter 均无高洛德消费点（仅 Core 模型），此为 NetOfd 增值渲染，
/// 语义按 GB/T 33190 §8.3.4.4/8.3.4.5 + ISO 32000-1 type-4/type-5。
/// </summary>
public class GouraudShdRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_gouraud_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>构造自由高洛德填充（红-绿-蓝 三角覆盖矩形左上大半）。</summary>
    private static CT_Color FreeFormFill()
    {
        var shd = new CT_GouraudShd().AddPoints(
            CT_GouraudShd.P(20, 20, CT_Color.FromRgb(255, 0, 0)),
            CT_GouraudShd.P(120, 20, CT_Color.FromRgb(0, 255, 0)),
            CT_GouraudShd.P(20, 60, CT_Color.FromRgb(0, 0, 255)));
        return new CT_Color().SetGouraudShd(shd);
    }

    /// <summary>构造网格高洛德填充（2×2：红-绿 / 蓝-白 覆盖整个矩形）。</summary>
    private static CT_Color LatticeFill()
    {
        var lat = new CT_LaGouraudShd().SetVerticesPerRow(2);
        lat.AddPoints(
            CT_GouraudShd.P(20, 20, CT_Color.FromRgb(255, 0, 0)),
            CT_GouraudShd.P(120, 20, CT_Color.FromRgb(0, 255, 0)),
            CT_GouraudShd.P(20, 60, CT_Color.FromRgb(0, 0, 255)),
            CT_GouraudShd.P(120, 60, CT_Color.White));
        return new CT_Color().SetLaGouraudShd(lat);
    }

    private static SKBitmap RenderSingle(CT_Color fill, string tag)
    {
        var ofdPath = Out($"gouraud_{tag}.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx.FillStyle = fill;
            ctx.FillRect(20, 20, 100, 40);   // 毫米坐标，与顶点 X/Y 同空间
        }

        var imgDir = Path.Combine(Out($"imgs_{tag}"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);
        var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);
        return bmp;
    }

    // ── 1. 自由高洛德：红/绿/蓝三角顶点色插值 ──────────────────
    [Fact]
    public void FreeForm_Render_ShouldInterpolateVertexColors()
    {
        using var bmp = RenderSingle(FreeFormFill(), "free");

        // 顶点色按面积坐标插值（ppm=10：mm→px 直接×10，原点在左上已由既有渲染测试证实）
        // P(30,25)mm → 重心 (α=.775 红, β=.1 绿, γ=.125 蓝) ≈ (198,25,32)：红色主导
        var p1 = bmp.GetPixel(300, 250);
        Assert.True(p1.Red > 150 && p1.Red - p1.Green > 100,
            $"近红顶点区应红主导，实际 RGBA=({p1.Red},{p1.Green},{p1.Blue})");

        // P(65,38)mm → 重心 (α=.1, β=.45 绿, γ=.45 蓝) ≈ (25,115,115)：青绿
        var p2 = bmp.GetPixel(650, 380);
        Assert.True(p2.Green > 80 && p2.Blue > 80 && p2.Red < 100,
            $"红-蓝边中点应青绿，实际 RGBA=({p2.Red},{p2.Green},{p2.Blue})");
    }

    // ── 2. 自由高洛德：三角形未覆盖区域不填充 ──────────────────
    [Fact]
    public void FreeForm_UncoveredRegion_ShouldStayBackground()
    {
        using var bmp = RenderSingle(FreeFormFill(), "free_bg");

        // 三角斜边 y=68-0.4x；P(110,55)mm 在三角形外、矩形内 → 背景白
        var p = bmp.GetPixel(1100, 550);
        Assert.True(p.Red > 225 && p.Green > 225 && p.Blue > 225,
            $"未覆盖区域应为背景白，实际 RGBA=({p.Red},{p.Green},{p.Blue})");
    }

    // ── 3. 网格高洛德：2×2 单元双三角带插值 ────────────────────
    [Fact]
    public void Lattice_Render_ShouldInterpolateAcrossQuadCells()
    {
        using var bmp = RenderSingle(LatticeFill(), "lat");

        // P(30,25)mm 在 T2(红/白/蓝) 内 → 重心 (α=.875 红, β=.1 白, γ=.025 蓝) ≈ (223,25,6)
        var p1 = bmp.GetPixel(300, 250);
        Assert.True(p1.Red > 160 && p1.Red - p1.Green > 120,
            $"左上近红角应红主导，实际 RGBA=({p1.Red},{p1.Green},{p1.Blue})");

        // P(70,40)mm 落在红(20,20)-白(120,60) 共享对角线上 → 红/白 50/50 = (255,128,128)
        var pc = bmp.GetPixel(700, 400);
        Assert.True(pc.Red > 220
                    && pc.Green is > 90 and < 170
                    && pc.Blue is > 90 and < 170
                    && Math.Abs(pc.Green - pc.Blue) < 45,
            $"单元中心应红白均分，实际 RGBA=({pc.Red},{pc.Green},{pc.Blue})");

        // P(110,30)mm 在 T1(红/绿/白) 内 → 重心 (α=.1, β=.65 绿, γ=.25 白) ≈ (89,191,64)
        var p2 = bmp.GetPixel(1100, 300);
        Assert.True(p2.Green > 140 && p2.Green - p2.Red > 40,
            $"右上近绿角应绿主导，实际 RGBA=({p2.Red},{p2.Green},{p2.Blue})");
    }
}
