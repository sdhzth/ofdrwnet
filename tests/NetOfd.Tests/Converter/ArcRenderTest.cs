using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NetOfd.Converter;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// 椭圆弧（<c>A</c> 命令）渲染回归测试：验证 OfdPathParser 将弧线转换为贝塞尔近似，
/// 不再静默跳过（此前含圆/椭圆/圆角的 OFD 会被画丢）。
/// </summary>
public class ArcRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_arc_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void Parse_Semicircle_ShouldProduceBezierSegmentsNotJustMove()
    {
        // 半径 50 的半圆：起点 (0,0) → 终点 (100,0)，圆心 (50,0)，弧深 ≈50
        var path = OfdPathParser.Parse("M 0 0 A 50 50 0 1 1 100 0");

        // 至少应有 MoveTo + 2 段三次贝塞尔（180° 被切成 2×90°）
        Assert.True(path.PointCount > 1, "弧线应生成贝塞尔段，而非仅起点（此前会被跳过）");

        // 弧线应偏离直线（若为直线/跳过则所有点 y≈0）
        float maxY = float.MinValue, minY = float.MaxValue;
        for (int i = 0; i < path.PointCount; i++)
        {
            var p = path.GetPoint(i);
            maxY = Math.Max(maxY, p.Y);
            minY = Math.Min(minY, p.Y);
        }
        Assert.True(Math.Max(maxY, -minY) > 40,
            $"半圆弧应偏离直线至 |y|≈50，实际 y∈[{minY},{maxY}]");

        // 终点应精确回到 (100,0)
        var last = path.GetPoint(path.PointCount - 1);
        Assert.True(Math.Abs(last.X - 100) < 1e-3 && Math.Abs(last.Y - 0) < 1e-3,
            $"终点应为 (100,0)，实际 ({last.X},{last.Y})");
    }

    [Fact]
    public void Parse_FullCircleViaTwoArcs_ShouldReturnToStart()
    {
        // 两个 180° 弧合成整圆：起点终点均为 (100,50)，圆心 (100,100) 半径 50 → y∈[50,150]
        var path = OfdPathParser.Parse("M 100 50 A 50 50 0 1 1 100 150 A 50 50 0 1 1 100 50 Z");
        var first = path.GetPoint(0);
        var last = path.GetPoint(path.PointCount - 1);
        Assert.True(Math.Abs(first.X - last.X) < 1e-3 && Math.Abs(first.Y - last.Y) < 1e-3,
            "整圆应闭合回到起点");
        // 圆应覆盖 y∈[50,150]（圆心100、半径50）
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < path.PointCount; i++)
        {
            var p = path.GetPoint(i);
            minY = Math.Min(minY, p.Y);
            maxY = Math.Max(maxY, p.Y);
        }
        Assert.True(minY < 60 && maxY > 140, $"整圆应覆盖 y∈[50,150]，实际 [{minY},{maxY}]");
    }

    [Fact]
    public void Render_CirclePath_ShouldFillCenterPixel()
    {
        // 构造一个填充正圆的 OFD（内容层直接写 A 命令），渲染后圆心应被填充
        const string publicRes = "<ofd:Res xmlns:ofd=\"http://www.ofdspec.org/2016\" BaseLoc=\"Res\"/>";
        const string document = """
            <ofd:Document xmlns:ofd="http://www.ofdspec.org/2016">
              <ofd:CommonData>
                <ofd:PageArea><ofd:PhysicalBox>0 0 200 200</ofd:PhysicalBox></ofd:PageArea>
                <ofd:PublicRes>PublicRes.xml</ofd:PublicRes>
                <ofd:MaxUnitID>5</ofd:MaxUnitID>
              </ofd:CommonData>
              <ofd:Pages>
                <ofd:Page ID="1" BaseLoc="Pages/Page_0/Content.xml"/>
              </ofd:Pages>
            </ofd:Document>
            """;
        // 圆心 (100,100) 半径 50 的整圆（两段 180° 弧）
        const string content = """
            <ofd:Page xmlns:ofd="http://www.ofdspec.org/2016">
              <ofd:Content>
                <ofd:Layer Type="Body">
                  <ofd:PathObject Boundary="0 0 200 200" Fill="true" Stroke="false">
                    <ofd:AbbreviatedData>M 100 50 A 50 50 0 1 1 100 150 A 50 50 0 1 1 100 50 Z</ofd:AbbreviatedData>
                    <ofd:FillColor Value="0 0 0"/>
                  </ofd:PathObject>
                </ofd:Layer>
              </ofd:Content>
            </ofd:Page>
            """;
        const string ofdXml = """
            <ofd:OFD xmlns:ofd="http://www.ofdspec.org/2016">
              <ofd:DocBody>
                <ofd:DocInfo><ofd:DocID>arc-circle-test</ofd:DocID></ofd:DocInfo>
                <ofd:DocRoot>Doc_0/Document.xml</ofd:DocRoot>
              </ofd:DocBody>
            </ofd:OFD>
            """;

        var path = Path.Combine(OutDir(), "arc_circle.ofd");
        var map = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["OFD.xml"] = System.Text.Encoding.UTF8.GetBytes(ofdXml),
            ["Doc_0/Document.xml"] = System.Text.Encoding.UTF8.GetBytes(document),
            ["Doc_0/PublicRes.xml"] = System.Text.Encoding.UTF8.GetBytes(publicRes),
            ["Doc_0/Pages/Page_0/Content.xml"] = System.Text.Encoding.UTF8.GetBytes(content),
        };
        byte[] bytes;
        using (var ms = new MemoryStream())
        {
            using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
                foreach (var kv in map)
                {
                    var e = za.CreateEntry(kv.Key);
                    using var s = e.Open();
                    s.Write(kv.Value, 0, kv.Value.Length);
                }
            bytes = ms.ToArray();
        }
        File.WriteAllBytes(path, bytes);

        using var maker = new OfdImageMaker(path);
        using var bmp = maker.MakePage(0, new ConverterOptions { PPM = 4, AntiAlias = false });
        Assert.NotNull(bmp);

        // 圆心 (100mm,100mm) @ ppm=4 → 像素 (400,400)
        var center = bmp.GetPixel(400, 400);
        Assert.True(center.Red < 60 && center.Green < 60 && center.Blue < 60,
            $"圆心应被填充为黑色，实际 RGB=({center.Red},{center.Green},{center.Blue})");
    }
}
