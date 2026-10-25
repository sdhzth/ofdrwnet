using System;
using System.IO;
using NetOfd.Graphics2D;
using NetOfd.Graphics2D.AWT;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.Font;

/// <summary>
/// 裁剪区域端到端测试：SetClip 后绘制超界矩形，仅裁剪区内像素着色（此前 Clip 静默失效）。
/// </summary>
public class ClipRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_clip_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void SetClip_ShouldLimitPaintedArea()
    {
        var outPath = Path.Combine(OutDir(), "clip.ofd");
        using (var doc = OFDGraphicsDocument.Create(outPath))
        {
            using (var g = doc.NewPage(100, 50))
            {
                // 裁剪到左半页（0..50mm）
                g.SetClip(new Rectangle2D(0, 0, 50, 50));
                g.SetColor(OFDColor.Red);
                g.FillRect(0, 0, 100, 50); // 矩形横跨整页
            }
        }

        using var maker = new NetOfd.Converter.OfdImageMaker(outPath);
        using var bmp = maker.MakePage(0, new NetOfd.Converter.ConverterOptions { PPM = 4, AntiAlias = false })!;

        // 左半：应为红色；右半：应保持白色
        AssertRed(bmp.GetPixel(20 * 4, 25 * 4));
        AssertRed(bmp.GetPixel(49 * 4, 25 * 4));
        AssertWhite(bmp.GetPixel(75 * 4, 25 * 4));
        AssertWhite(bmp.GetPixel(90 * 4, 10 * 4));
    }

    private static void AssertRed(SKColor c)
        => Assert.True(c.Red > 140 && c.Green < 90 && c.Blue < 90, $"应为红色，实际 {c.Red},{c.Green},{c.Blue}");

    private static void AssertWhite(SKColor c)
        => Assert.True(c.Red > 240 && c.Green > 240 && c.Blue > 240, $"应为白色（裁剪区外），实际 {c.Red},{c.Green},{c.Blue}");
}
