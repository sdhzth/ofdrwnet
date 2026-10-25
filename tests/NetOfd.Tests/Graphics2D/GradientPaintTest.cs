using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Graphics2D;
using NetOfd.Graphics2D.AWT;
using Xunit;

namespace NetOfd.Tests.Font;

/// <summary>
/// 渐变画刷契约测试：插值数学 + 写入 OFD 时的中点纯色近似（避免渐变填充静默丢失）。
/// </summary>
public class GradientPaintTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_gradient_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void LinearGradient_EndpointsAndMidpoint()
    {
        var red = new OFDColor(255, 0, 0);
        var blue = new OFDColor(0, 0, 255);
        var g = new OFDLinearGradientPaint(
            new Point2D(0, 0), new Point2D(10, 0),
            new[] { red, blue }, new[] { 0f, 1f }, OFDLinearGradientPaint.NO_CYCLE);

        Assert.Equal(255, g.GetColor(0).R);
        Assert.Equal(0, g.GetColor(0).B);
        Assert.Equal(0, g.GetColor(1).R);
        Assert.Equal(255, g.GetColor(1).B);

        var mid = g.GetColor(0.5);
        Assert.InRange(mid.R, 120, 136);
        Assert.InRange(mid.B, 120, 136);
    }

    [Fact]
    public void LinearGradient_RepeatCycle_ShouldWrap()
    {
        var c0 = new OFDColor(0, 0, 0);
        var c1 = new OFDColor(200, 200, 200);
        var g = new OFDLinearGradientPaint(
            new Point2D(0, 0), new Point2D(1, 0),
            new[] { c0, c1 }, new[] { 0f, 1f }, OFDLinearGradientPaint.REPEAT);

        // t=1.25 → 回绕到 0.25
        var c = g.GetColor(1.25);
        Assert.InRange(c.R, 40, 60);
    }

    [Fact]
    public void RadialGradient_And_GradientPaint_Interpolation()
    {
        var a = new OFDColor(0, 0, 0);
        var b = new OFDColor(100, 100, 100);
        var rg = new OFDRadialGradientPaint(
            new Point2D(5, 5), 10, new Point2D(5, 5),
            new[] { a, b }, new[] { 0f, 1f }, OFDLinearGradientPaint.NO_CYCLE);
        Assert.Equal(50, rg.GetColor(0.5).R);

        var gp = new OFDGradientPaint(0, 0, a, 10, 0, b, cyclic: true);
        Assert.Equal(0, gp.GetColor(0).R);
        Assert.Equal(100, gp.GetColor(1).R);
        // Cyclic：t=1.5 反射回 0.5
        Assert.Equal(50, gp.GetColor(1.5).R);
    }

    [Fact]
    public void GradientFill_ShouldWriteApproximatedSolidColor()
    {
        var outPath = Path.Combine(OutDir(), "gradient.ofd");
        using (var doc = OFDGraphicsDocument.Create(outPath))
        {
            using (var g = doc.NewPage())
            {
                var lg = new OFDLinearGradientPaint(
                    new Point2D(0, 0), new Point2D(50, 0),
                    new[] { new OFDColor(255, 0, 0), new OFDColor(0, 0, 255) },
                    new[] { 0f, 1f }, OFDLinearGradientPaint.NO_CYCLE);
                g.SetPaint(lg);
                g.FillRect(10, 10, 40, 20);
            }
        }

        var ofd = File.ReadAllBytes(outPath);
        using var ms = new MemoryStream(ofd);
        using var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var entry = za.Entries.First(e => e.FullName.EndsWith("Content.xml", StringComparison.Ordinal));
        using var s = entry.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        var content = XDocument.Parse(System.Text.Encoding.UTF8.GetString(buf.ToArray()));

        // 近似色应为中点 (128,0,128) 系（允许 ±8 舍入差）
        var fill = content.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "FillColor")?.Attribute("Value")?.Value;
        Assert.False(string.IsNullOrEmpty(fill), "渐变填充应写入近似的纯色 FillColor");
        var parts = fill!.Split(new[] { ' ', ',' }).Select(double.Parse).ToArray();
        Assert.InRange(parts[0], 120, 136);
        Assert.InRange(parts[2], 120, 136);
    }
}
