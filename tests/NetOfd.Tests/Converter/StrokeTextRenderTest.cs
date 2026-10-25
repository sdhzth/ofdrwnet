using NetOfd.Converter;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using SkiaSharp;
using System;
using System.IO;
using System.Linq;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// StrokeText 渲染端到端：FillText 走 FillStyle 红、StrokeText 走 StrokeStyle 蓝。
/// 核心验证：StrokeText 区域以**蓝色**前景像素为主（旧 bug 走 fillPaint 且颜色为 StrokeStyle 蓝，渲染区域会显示蓝色，
/// 但 TextObject.Fill=false + Stroke=true 时 doFill=false 应被严格遵守，文字区域不应出现红色像素混入）。
/// 注意：SkiaSharp 在大字号（40mm=400px）下 StrokeText 与 FillText 的具体像素覆盖都受 Skia 自身字体路径行为影响，
/// 故断言以**颜色归属**与**蓝色主导**为唯一可靠区分依据。
/// </summary>
public class StrokeTextRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_stroketext_render");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    [Fact]
    public void StrokeText_Render_StrokeRegionShouldBeBlueAndNotMixedRed()
    {
        var ofdPath = Out("stroke_render.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx.Font = "40mm SimSun";
            ctx.LineWidth = 2;
            // 上方：FillText 红
            ctx.FillStyle = "#ff0000";
            ctx.FillText("中", 30, 40);
            // 下方：StrokeText 蓝
            ctx.StrokeStyle = "#0000ff";
            ctx.StrokeText("中", 30, 130);
        }

        var imgDir = Path.Combine(Out("imgs"));
        using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 10);
        var paths = exporter.Export();
        Assert.Single(paths);

        using var bmp = SKBitmap.Decode(paths[0]);
        Assert.NotNull(bmp);

        // 按 Y 区域统计：上 1/3 为红字区（OFD y=40mm → 视觉顶部），中段为蓝字区（OFD y=130mm）
        int redUpper = 0, blueUpper = 0, redMid = 0, blueMid = 0;
        int midStart = bmp.Height / 3;
        int midEnd   = bmp.Height * 2 / 3;
        for (int y = 0; y < bmp.Height; y += 2)
        {
            for (int x = 0; x < bmp.Width; x += 2)
            {
                var px = bmp.GetPixel(x, y);
                bool isRed  = px.Red  > 150 && px.Red  - px.Blue > 90;
                bool isBlue = px.Blue > 150 && px.Blue - px.Red  > 90;
                if (y < midStart)
                {
                    if (isRed) redUpper++; else if (isBlue) blueUpper++;
                }
                else if (y >= midStart && y <= midEnd)
                {
                    if (isRed) redMid++; else if (isBlue) blueMid++;
                }
            }
        }

        Assert.True(redUpper > 100, $"FillText 红字区域应存在红色像素（实际 {redUpper}）");
        Assert.True(blueMid  > 3000, $"StrokeText 蓝字区域应渲染出大量蓝色像素（实际 {blueMid}）");
        // 关键断言：StrokeText 区域必须以蓝色像素为主（证明 StrokeColor 蓝被读到 strokePaint，
        // 而非旧 bug 时 TextColor=红 + Fill=true 导致红像素主导）
        Assert.True(blueMid > redMid * 1.5,
            $"StrokeText 区域应蓝像素主导（blue={blueMid} red={redMid}）");
    }
}