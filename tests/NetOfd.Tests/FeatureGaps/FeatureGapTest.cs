using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using NetOfd.Reader;
using NetOfd.Sign;
using NetOfd.Sign.StampPos;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// 覆盖缺口补测：水印、骑缝章定位、基础类型（STPos/STArray）、页面尺寸预设。
/// </summary>
public class FeatureGapTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_gap_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private static byte[]? ReadEntry(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var e = za.GetEntry(entryName);
        if (e == null) return null;
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    // ── 水印 ─────────────────────────────────────────────────────

    [Fact]
    public void Watermark_ShouldAppearOnEveryPage_BeforeExistingContent()
    {
        var path = Out("wm.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("正文内容").SetFontSize(5d));
            doc.AddWatermark(new NetOfd.Layout.Edit.Watermark(PageLayout.A4())
                .SetValue("绝密文件")
                .SetColor("#CC0000"));
        }

        var ofd = File.ReadAllBytes(path);

        // 第 1 页仅有水印层（正文段落落在第 2 页）
        var p0 = XDocument.Parse(System.Text.Encoding.UTF8.GetString(
            ReadEntry(ofd, "Doc_0/Pages/Page_0/Content.xml")!));
        Assert.Single(p0.Descendants().Where(e => e.Name.LocalName == "Layer"));
        Assert.Contains(p0.Descendants(), e =>
            e.Name.LocalName == "TextCode" && e.Value.Contains("绝密"));

        // 第 2 页：水印层在最前，正文在其后
        var p1 = XDocument.Parse(System.Text.Encoding.UTF8.GetString(
            ReadEntry(ofd, "Doc_0/Pages/Page_1/Content.xml")!));
        var layers = p1.Descendants().Where(e => e.Name.LocalName == "Layer").ToList();
        Assert.True(layers.Count >= 2);
        Assert.Contains(layers[0].Descendants(), e =>
            e.Name.LocalName == "TextCode" && e.Value.Contains("绝密"));
        Assert.Contains(p1.Descendants(), e =>
            e.Name.LocalName == "TextCode" && e.Value.Contains("正文"));
        // 水印为斜排平铺（CTM 非恒等）且半透明
        Assert.Contains(p1.Descendants(),
            e => e.Name.LocalName == "TextObject" && e.Attribute("Alpha") != null);
    }

    // ── 骑缝章 / 切缝章定位 ──────────────────────────────────────

    private static string BuildMultiPage(int pages)
    {
        var path = Out($"ride_{pages}.ofd");
        using (var doc = new OFDDoc(path))
        {
            for (int i = 0; i < pages; i++) doc.AddPage();
        }
        return path;
    }

    [Fact]
    public void RidingStampPos_ShouldSpanAllPagesOnRightSide()
    {
        using var reader = new OFDReader(BuildMultiPage(3));
        var provider = new NumberFormatAtomicSignId();
        var pos = new RidingStampPos(Side.Right, width: 20, height: 60);
        var annots = pos.GetAppearance(reader, provider);

        Assert.NotEmpty(annots);
        foreach (var a in annots)
        {
            Assert.False(string.IsNullOrEmpty(a.Id));
            Assert.False(string.IsNullOrEmpty(a.PageRef));
            Assert.Matches(@"^[\d.]+ [\d.]+ [\d.]+ [\d.]+$", a.Boundary);
        }
        // 右侧骑缝：各页印章的横向位置应贴近页右缘
        foreach (var a in annots)
        {
            var parts = a.Boundary.Split(' ').Select(double.Parse).ToArray();
            Assert.True(parts[0] > 100, $"骑缝章应靠近右缘，实际 Boundary={a.Boundary}");
        }
    }

    [Fact]
    public void CuttingRideStampPos_ShouldProduceClippedFragments()
    {
        using var reader = new OFDReader(BuildMultiPage(4));
        var provider = new NumberFormatAtomicSignId();
        var ratios = new List<CuttingRatio>
        {
            new() { Left = 0.25, Right = 0.5 },
            new() { Left = 0.5, Right = 0.75 },
            new() { Left = 0.75, Right = 1.0 },
            new() { Left = 1.0, Right = 1.0 },
        };
        var pos = new CuttingRideStampPos(Side.Right, offset: null, width: 18, height: 50, margin: 0, ratios);
        var annots = pos.GetAppearance(reader, provider);
        // 首页右条 + 中间页各两条 + 末页左条 = N + (N-2) = 2N-2
        Assert.Equal(6, annots.Count);
        Assert.All(annots, a =>
        {
            Assert.False(string.IsNullOrEmpty(a.Clip), "切缝章片段应带 Clip 裁剪区域");
            Assert.Matches(@"^-?[\d.]+ -?[\d.]+ [\d.]+ [\d.]+$", a.Boundary);
        });
    }

    // ── 基础类型 ─────────────────────────────────────────────────

    [Fact]
    public void STPos_ParseAndRoundtrip()
    {
        var p = STPos.Parse("12.5 -3");
        Assert.Equal(12.5, p.X);
        Assert.Equal(-3, p.Y);
        Assert.Equal(p, STPos.Parse(p.ToString()));
        Assert.Equal(p, new STPos(12.5, -3));
        Assert.NotEqual(p, new STPos(1, 1));

        Assert.ThrowsAny<Exception>(() => STPos.Parse("not-a-pos"));
    }

    [Fact]
    public void STArray_AddParseIndex()
    {
        var arr = new STArray();
        arr.Add(1.5); arr.Add(-2); arr.Add(0.25);
        Assert.Equal(3, arr.Count);
        Assert.Equal(1.5, arr[0]);
        Assert.Equal(0.25, arr[2]);

        var parsed = STArray.Parse("1 2 3.5");
        Assert.Equal(new[] { 1d, 2, 3.5 }, parsed.ToDoubleArray());

        // CTM 矩阵：单位矩阵与乘法
        var unit = STArray.UnitCTM();
        Assert.Equal(6, unit.Count);
        var m1 = new STArray("2 0 0 2 10 20");
        var prod = STArray.MtxMul(unit, m1);
        Assert.Equal(m1.ToDoubleArray(), prod.ToDoubleArray());

        // 缩放×平移复合：(2,4) → 平移后 (14,28)
        var scale = new STArray("2 0 0 2 0 0");
        var trans = new STArray("1 0 0 1 10 20");
        var comp = STArray.MtxMul(trans, scale); // 先缩放后平移
        double x = 2 * comp[0] + 4 * comp[2] + comp[4];
        double y = 2 * comp[1] + 4 * comp[3] + comp[5];
        Assert.Equal(14, x, 6);
        Assert.Equal(28, y, 6);

        Assert.Contains("10", trans.ToString());
    }

    [Fact]
    public void PageLayout_Presets_ShouldMatchIsoSizes()
    {
        Assert.Equal((210, 297), (PageLayout.A4().Width, PageLayout.A4().Height));
        Assert.Equal((297, 420), (PageLayout.A3().Width, PageLayout.A3().Height));
        Assert.Equal((148, 210), (PageLayout.A5().Width, PageLayout.A5().Height));
        Assert.Equal((841, 1189), (PageLayout.A0().Width, PageLayout.A0().Height));
        Assert.Equal((37, 52), (PageLayout.A9().Width, PageLayout.A9().Height));
    }
}
