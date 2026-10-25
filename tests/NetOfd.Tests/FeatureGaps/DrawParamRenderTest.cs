using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetOfd.Converter;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// 渲染器样式级联集成测试：路径未自带线宽/颜色时，应经 DrawParam ParentID 链取值。
/// </summary>
public class DrawParamRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_dp_render_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static byte[] BuildDocWithInheritedDrawParam()
    {
        // 父参数(ID=1)：红色描边、线宽 2mm；子参数(ID=2) 仅继承，无自有样式
        const string publicRes = """
            <ofd:Res xmlns:ofd="http://www.ofdspec.org/2016" BaseLoc="Res">
              <ofd:DrawParams>
                <ofd:DrawParam ID="1">
                  <ofd:LineWidth>2</ofd:LineWidth>
                  <ofd:StrokeColor Value="204 0 0"/>
                </ofd:DrawParam>
                <ofd:DrawParam ID="2" ParentID="1"/>
              </ofd:DrawParams>
            </ofd:Res>
            """;
        const string document = """
            <ofd:Document xmlns:ofd="http://www.ofdspec.org/2016">
              <ofd:CommonData>
                <ofd:PageArea><ofd:PhysicalBox>0 0 210 297</ofd:PhysicalBox></ofd:PageArea>
                <ofd:PublicRes>PublicRes.xml</ofd:PublicRes>
                <ofd:MaxUnitID>5</ofd:MaxUnitID>
              </ofd:CommonData>
              <ofd:Pages>
                <ofd:Page ID="1" BaseLoc="Pages/Page_0/Content.xml"/>
              </ofd:Pages>
            </ofd:Document>
            """;
        const string content = """
            <ofd:Page xmlns:ofd="http://www.ofdspec.org/2016">
              <ofd:Content>
                <ofd:Layer Type="Body">
                  <ofd:PathObject Boundary="0 0 210 297" Stroke="true" Fill="false" DrawParam="2">
                    <ofd:AbbreviatedData>M 20 150 L 190 150</ofd:AbbreviatedData>
                  </ofd:PathObject>
                </ofd:Layer>
              </ofd:Content>
            </ofd:Page>
            """;
        const string ofdXml = """
            <ofd:OFD xmlns:ofd="http://www.ofdspec.org/2016">
              <ofd:DocBody>
                <ofd:DocInfo><ofd:DocID>dp-render-test</ofd:DocID></ofd:DocInfo>
                <ofd:DocRoot>Doc_0/Document.xml</ofd:DocRoot>
              </ofd:DocBody>
            </ofd:OFD>
            """;

        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            ["OFD.xml"] = System.Text.Encoding.UTF8.GetBytes(ofdXml),
            ["Doc_0/Document.xml"] = System.Text.Encoding.UTF8.GetBytes(document),
            ["Doc_0/PublicRes.xml"] = System.Text.Encoding.UTF8.GetBytes(publicRes),
            ["Doc_0/Pages/Page_0/Content.xml"] = System.Text.Encoding.UTF8.GetBytes(content),
        };
        using var ms = new MemoryStream();
        using (var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Create, true))
            foreach (var kv in map)
            {
                var e = za.CreateEntry(kv.Key);
                using var s = e.Open();
                s.Write(kv.Value, 0, kv.Value.Length);
            }
        return ms.ToArray();
    }

    [Fact]
    public void Render_PathWithoutOwnStyle_ShouldInheritParentLineWidthAndColor()
    {
        var path = Path.Combine(OutDir(), "dp_render.ofd");
        File.WriteAllBytes(path, BuildDocWithInheritedDrawParam());

        using var maker = new OfdImageMaker(path);
        using var bmp = maker.MakePage(0, new ConverterOptions { PPM = 4, AntiAlias = false })!;

        // 线位于 y=150mm，ppm=4 → 像素 y≈600；在 x=300..500px 范围采样应命中红色描边（±3px 容差）
        bool foundRed = false;
        for (int x = 300; x <= 500 && !foundRed; x += 10)
        {
            for (int dy = -6; dy <= 6; dy++)
            {
                var c = bmp.GetPixel(x, 600 + dy);
                if (c.Red > 140 && c.Green < 90 && c.Blue < 90)
                {
                    foundRed = true;
                    break;
                }
            }
        }
        Assert.True(foundRed,
            "路径未带样式时应继承父 DrawParam 的红色与 2mm 线宽；若线宽/颜色丢失则采样失败");

        // 对照：线宽若按默认 0.353mm 渲染则仅约 1-2px 厚；统计命中列数以确认粗线
        int thickHits = 0;
        for (int x = 300; x <= 500; x += 25)
        {
            int col = 0;
            for (int dy = -8; dy <= 8; dy++)
            {
                var c = bmp.GetPixel(x, 600 + dy);
                if (c.Red > 140 && c.Green < 90 && c.Blue < 90) col++;
            }
            if (col >= 5) thickHits++; // 2mm@4ppm ≈ 8px 厚
        }
        Assert.True(thickHits >= 5, $"线宽应接近 2mm（多数采样列 ≥5px），实际厚列 {thickHits}/9");
    }
}
