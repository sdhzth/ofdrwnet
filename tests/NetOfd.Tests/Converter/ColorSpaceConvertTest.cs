using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Converter;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Core.PageDescription.Color.colorSpace;
using NetOfd.Core.Resources;
using NetOfd.Pkg.Container;
using NetOfd.Reader;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// OFD 颜色空间（ColorSpace / Palette / Index）渲染保真度测试。
/// 覆盖：类型解析、模型 XML 往返、调色板索引解析、
/// CMYK→RGB（OFD 0-255 语义）、默认 RGB 回退，以及经由 ResourceManage 的端到端解析。
/// </summary>
public class ColorSpaceConvertTest
{
    private const string OFD_NS = "http://www.ofdspec.org/2016";

    // ── 1. 颜色空间类型解析 ────────────────────────────────────

    [Theory]
    [InlineData("GRAY", OFDColorSpaceType.GRAY)]
    [InlineData("rgb", OFDColorSpaceType.RGB)]
    [InlineData("Cmyk", OFDColorSpaceType.CMYK)]
    public void ColorSpaceType_Parse_IsCaseInsensitive(string raw, OFDColorSpaceType expected)
        => Assert.Equal(expected, OFDColorSpaceTypeExtensions.GetInstance(raw));

    [Fact]
    public void CT_ColorSpace_UnknownType_FallsBackToRgb()
    {
        var x = XElement.Parse(
            $"<ColorSpace xmlns=\"{OFD_NS}\" ID=\"1\" Type=\"WHAT\">" +
            $"<Palette><CV>10 20 30</CV></Palette></ColorSpace>");
        var cs = new CT_ColorSpace(x);
        Assert.Equal(OFDColorSpaceType.RGB, cs.Type); // 未知类型回退为 RGB，避免渲染中断
    }

    // ── 2. CT_ColorSpace / Palette / CV 模型 XML 往返 ──────────

    [Fact]
    public void CT_ColorSpace_ModelRoundTrip()
    {
        var xml = $@"<ColorSpace xmlns=""{OFD_NS}"" ID=""5"" Type=""CMYK"" BitsPerComponent=""8"">
  <Palette>
    <CV>0 0 0 0</CV>
    <CV>0 255 255 0</CV>
  </Palette>
</ColorSpace>";
        var cs = new CT_ColorSpace(XElement.Parse(xml));
        Assert.Equal(5L, (long)cs.ID!);
        Assert.Equal(OFDColorSpaceType.CMYK, cs.Type);
        Assert.Equal(8, cs.BitsPerComponent);
        Assert.NotNull(cs.Palette);
        Assert.Equal("0 255 255 0", cs.Palette!.GetColorTextByIndex(1));
    }

    [Fact]
    public void Palette_CV_IndexResolution_AndOutOfRange()
    {
        var p = new Palette();
        p.AddCV(new CV { ColorText = "0 0 0" });
        p.AddCV(new CV { ColorText = "255 0 0" });
        p.AddCV(new CV { ColorText = "0 255 0" });

        Assert.Equal("0 0 0", p.GetColorTextByIndex(0));
        Assert.Equal("255 0 0", p.GetColorTextByIndex(1));
        Assert.Equal("0 255 0", p.GetColorTextByIndex(2));
        Assert.Null(p.GetColorTextByIndex(3));   // 越界 → null
        Assert.Null(p.GetColorTextByIndex(-1));  // 负索引 → null
        Assert.Equal(3, p.GetCVs().Count);
    }

    [Fact]
    public void CT_Color_IndexAndColorSpace_SetGet()
    {
        var c = new CT_Color
        {
            Value = "255 0 0",
            ColorSpace = STRefID.Parse("10"),
            Index = 2,
            Alpha = 128
        };
        Assert.Equal("255 0 0", c.Value);
        Assert.Equal(10L, (long)c.ColorSpace!);
        Assert.Equal(2, c.Index);
        Assert.Equal(128, c.Alpha);
    }

    // ── 3. CMYK→RGB（OFD 0-255 语义，与 Java 0-100 不同）──────

    [Theory]
    [InlineData(255, 0, 0, 0, 0, 255, 255)]   // 纯青
    [InlineData(0, 255, 0, 0, 255, 0, 255)]   // 纯洋红
    [InlineData(0, 0, 255, 0, 255, 255, 0)]   // 纯黄
    [InlineData(0, 0, 0, 255, 0, 0, 0)]       // 纯黑
    [InlineData(0, 0, 0, 0, 255, 255, 255)]   // 白
    [InlineData(0, 255, 255, 0, 255, 0, 0)]   // 红（m+y）
    public void ColorConvert_CmykToRgb(int c, int m, int y, int k, int r, int g, int b)
        => Assert.Equal(new[] { r, g, b }, ColorConvert.CmykToRgb(c, m, y, k));

    // ── 4. 默认 RGB 回退（未引用 ColorSpace 时按行内值解释）───

    [Fact]
    public void ColorConvert_Rgb_InlineRgb_DefaultRgb()
    {
        var rgb = ColorConvert.Rgb(null, CT_Color.FromRgb(255, 0, 0));
        Assert.Equal(new[] { 255, 0, 0 }, rgb);
    }

    [Fact]
    public void ColorConvert_Rgb_InlineGray_DefaultGray()
    {
        var g = ColorConvert.Rgb(null, CT_Color.FromGray(128));
        Assert.Equal(new[] { 128, 128, 128 }, g);
    }

    [Fact]
    public void ColorConvert_Rgb_InlineCmyk_DefaultRgbInterpretation()
    {
        // 未引用 ColorSpace 时，4 通道按 CMYK(0-255) 解释
        var c = new CT_Color { Value = "0 255 255 0" }; // 红
        Assert.Equal(new[] { 255, 0, 0 }, ColorConvert.Rgb(null, c));
    }

    [Fact]
    public void ColorConvert_Rgb_EmptyValueNoIndex_ReturnsBlack()
        => Assert.Equal(new[] { 0, 0, 0 }, ColorConvert.Rgb(null, new CT_Color()));

    [Fact]
    public void ColorConvert_Rgb_IndexWithoutResMgt_ReturnsNull()
    {
        // 有 Index 但无 resMgt 且未引用 ColorSpace → 调色板无法解析 → null
        var c = new CT_Color { Index = 1 };
        Assert.Null(ColorConvert.Rgb(null, c));
    }

    // ── 5. OfdColorUtil 颜色空间感知重载 ───────────────────────

    [Fact]
    public void OfdColorUtil_ToSKColor_ColorSpaceRefFallsBackToInline_WhenResMgtNull()
    {
        // 引用了 ColorSpace 但未提供 resMgt → 走行内解析（保证不崩溃）
        var c = new CT_Color { Value = "255 0 0", ColorSpace = STRefID.Parse("10") };
        var sk = OfdColorUtil.ToSKColor(c, SKColors.Black, resMgt: null);
        Assert.Equal(255, sk.Red);
        Assert.Equal(0, sk.Green);
        Assert.Equal(0, sk.Blue);
    }

    [Fact]
    public void OfdColorUtil_ToSKColorOrNull_NullAndTransparent()
    {
        Assert.Null(OfdColorUtil.ToSKColorOrNull(null));
        // 透明兜底色视为“无颜色”
        Assert.Null(OfdColorUtil.ToSKColorOrNull(new CT_Color(), resMgt: null));
    }

    // ── 6. 端到端：ResourceManage 解析 ColorSpace + 调色板索引 ──

    [Fact]
    public void ResourceManage_GetColorSpace_ResolvesPaletteIndex()
    {
        using var ms = BuildOfdWithColorSpace();
        ms.Position = 0;
        using var reader = new OFDReader(ms);
        var resMgt = reader.GetResourceManager();

        // 颜色空间经 ResourceManage 载入
        var cs = resMgt.GetColorSpace(STRefID.Parse("5"));
        Assert.NotNull(cs);
        Assert.Equal(OFDColorSpaceType.RGB, cs!.Type);

        // 调色板索引解析：index=1 → "255 0 0"
        var ctColor = new CT_Color { ColorSpace = STRefID.Parse("5"), Index = 1 };
        var rgb = ColorConvert.Rgb(resMgt, ctColor);
        Assert.Equal(new[] { 255, 0, 0 }, rgb);

        // SKColor 转换（含默认不透明）
        var sk = OfdColorUtil.ToSKColor(ctColor, SKColors.Black, resMgt);
        Assert.Equal(255, sk.Red);
        Assert.Equal(0, sk.Green);
        Assert.Equal(0, sk.Blue);
        Assert.Equal(255, sk.Alpha);
    }

    // ── 辅助：构造最小合法 OFD（含 ColorSpace + Palette）────────

    private static MemoryStream BuildOfdWithColorSpace()
    {
        const string ofdXml = $@"<OFD xmlns=""{OFD_NS}"">
  <DocBody>
    <DocRoot>Doc_0/Document.xml</DocRoot>
  </DocBody>
</OFD>";

        const string documentXml = $@"<Document xmlns=""{OFD_NS}"">
  <CommonData>
    <DocumentRes>Doc_0/DocumentRes.xml</DocumentRes>
  </CommonData>
  <Pages>
    <Page BaseLoc=""Pages/Page_0/Content.xml""/>
  </Pages>
</Document>";

        const string docResXml = $@"<Res xmlns=""{OFD_NS}"">
  <ColorSpaces>
    <ColorSpace ID=""5"" Type=""RGB"" BitsPerComponent=""8"">
      <Palette>
        <CV>0 0 0</CV>
        <CV>255 0 0</CV>
        <CV>0 255 0</CV>
      </Palette>
    </ColorSpace>
  </ColorSpaces>
</Res>";

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "OFD.xml", ofdXml);
            WriteEntry(zip, "Doc_0/Document.xml", documentXml);
            WriteEntry(zip, "Doc_0/DocumentRes.xml", docResXml);
        }
        return ms;
    }

    private static void WriteEntry(ZipArchive zip, string name, string xml)
    {
        var entry = zip.CreateEntry(name);
        using var s = entry.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(false));
        w.Write(xml);
    }
}
