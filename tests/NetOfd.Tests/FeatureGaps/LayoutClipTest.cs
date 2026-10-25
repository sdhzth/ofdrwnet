using System;
using System.IO;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// Layout 画布裁剪端到端：ClipRect 后绘制跨界矩形，仅裁剪区内像素着色。
/// </summary>
public class LayoutClipTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_layout_clip_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void ClipRect_ShouldLimitPaintedArea()
    {
        var outPath = Path.Combine(OutDir(), "layout_clip.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));

            ctx.ClipRect(0, 0, 50, 297);          // 只允许左半页
            ctx.FillStyle = "#FF0000";
            ctx.FillRect(0, 0, 200, 100);         // 横跨整宽的矩形
        }

        using var maker = new NetOfd.Converter.OfdImageMaker(outPath);
        using var bmp = maker.MakePage(0, new NetOfd.Converter.ConverterOptions { PPM = 4, AntiAlias = false })!;

        AssertRed(bmp.GetPixel(20 * 4, 50 * 4));
        AssertRed(bmp.GetPixel(49 * 4, 50 * 4));
        AssertWhite(bmp.GetPixel(75 * 4, 50 * 4));
        AssertWhite(bmp.GetPixel(150 * 4, 20 * 4));
    }

    private static void AssertRed(SKColor c)
        => Assert.True(c.Red > 140 && c.Green < 90 && c.Blue < 90, $"应为红色，实际 {c.Red},{c.Green},{c.Blue}");

    private static void AssertWhite(SKColor c)
        => Assert.True(c.Red > 240 && c.Green > 240 && c.Blue > 240, $"应为白色（裁剪区外），实际 {c.Red},{c.Green},{c.Blue}");
}
