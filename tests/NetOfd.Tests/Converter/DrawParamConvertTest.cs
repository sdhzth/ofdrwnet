using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Converter;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.Resources;
using NetOfd.Pkg.Container;
using NetOfd.Reader;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// OFD 绘制参数（DrawParam：透明度 Alpha / 可见性 Visible / 混合模式 Composite）
/// 与图元级透明度级联的渲染保真度测试。
/// 覆盖：模型 XML 往返、ResourceManage 解析、半透明图元/图层渲染、可见性跳过。
/// </summary>
public class DrawParamConvertTest
{
    private const string OFD_NS = "http://www.ofdspec.org/2016";

    // ── 1. CT_DrawParam 模型 XML 往返 ──────────────────────────

    [Fact]
    public void CT_DrawParam_ModelRoundTrip()
    {
        var xml = $@"<DrawParam xmlns=""{OFD_NS}"" ID=""7"" Alpha=""128"" Visible=""false"" Composite=""Multiply""/>";
        var dp = new CT_DrawParam(XElement.Parse(xml));
        Assert.Equal("7", dp.Id);
        Assert.Equal(128, dp.Alpha);
        Assert.False(dp.Visible);
        Assert.Equal("Multiply", dp.Composite);

        // 缺省值
        var dp2 = new CT_DrawParam(XElement.Parse($"<DrawParam xmlns=\"{OFD_NS}\" ID=\"8\"/>"));
        Assert.Equal(255, dp2.Alpha);        // 缺省完全不透明
        Assert.True(dp2.Visible);            // 缺省可见
        Assert.Null(dp2.Composite);
    }

    // ── 2. CTGraphicUnit.Alpha 读写（经 CTPath 子类）────────────

    [Fact]
    public void CTGraphicUnit_Alpha_SetGet()
    {
        var path = new CTPath { Alpha = 64 };
        Assert.Equal(64, path.Alpha);

        var path2 = new CTPath();
        Assert.Null(path2.Alpha);            // 缺省无 Alpha 属性 → null（渲染时按 255 处理）
    }

    [Fact]
    public void CTGraphicUnit_Visible_DefaultTrue()
    {
        var path = new CTPath();
        Assert.True(path.Visible);
        path.Visible = false;
        Assert.False(path.Visible);
    }

    // ── 3. 端到端：ResourceManage 解析 DrawParam ───────────────

    [Fact]
    public void ResourceManage_GetDrawParam_ResolvesAlphaAndVisible()
    {
        using var ms = BuildOfdWithDrawParam();
        ms.Position = 0;
        using var reader = new OFDReader(ms);
        var resMgt = reader.GetResourceManager();

        var dp = resMgt.GetDrawParam(STRefID.Parse("7"));
        Assert.NotNull(dp);
        Assert.Equal(128, dp!.Alpha);
        Assert.False(dp.Visible);

        // 未引用 / 不存在 → null
        Assert.Null(resMgt.GetDrawParam(null));
        Assert.Null(resMgt.GetDrawParam(STRefID.Parse("999")));
    }

    // ── 4. 半透明图元渲染：有效 Alpha 生效 ─────────────────────

    [Fact]
    public void Render_SemiTransparentPath_ProducesPartialAlphaPixel()
    {
        using var ms = BuildOfdWithTransparentPath(alpha: "128");
        ms.Position = 0;
        using var reader = new OFDReader(ms);
        var resMgt = reader.GetResourceManager();
        var page = reader.GetPage(0);
        Assert.NotNull(page);

        // 透明背景：半透明图元叠加后位图的 alpha 通道即为其有效不透明度
        var opts = new ConverterOptions { PPM = 10, Background = SKColors.Transparent, AntiAlias = false };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 10, 10, opts);
        Assert.NotNull(bmp);

        var px = bmp!.GetPixel(50, 50); // 100×100 位图中心，落在红色矩形内部
        Assert.Equal(255, px.Red);
        Assert.True(px.Alpha is >= 120 and <= 136,
            $"期望半透明 alpha≈128，实际 {px.Alpha}");
    }

    [Fact]
    public void Render_OpaquePath_ProducesFullAlphaPixel()
    {
        using var ms = BuildOfdWithTransparentPath(alpha: null);
        ms.Position = 0;
        using var reader = new OFDReader(ms);
        var resMgt = reader.GetResourceManager();
        var page = reader.GetPage(0);

        var opts = new ConverterOptions { PPM = 10, Background = SKColors.Transparent, AntiAlias = false };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 10, 10, opts);

        var px = bmp!.GetPixel(50, 50);
        Assert.Equal(255, px.Alpha);  // 不透明图元 → alpha=255
    }

    // ── 5. 图层 DrawParam Visible=false → 整层跳过 ────────────

    [Fact]
    public void Render_LayerDrawParamInvisible_SkipsLayer()
    {
        using var ms = BuildOfdWithInvisibleLayer();
        ms.Position = 0;
        using var reader = new OFDReader(ms);
        var resMgt = reader.GetResourceManager();
        var page = reader.GetPage(0);

        var opts = new ConverterOptions { PPM = 10, Background = SKColors.Transparent, AntiAlias = false };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 10, 10, opts);

        // 图层被隐藏且无其他内容 → 整图透明（无任何绘制）
        var px = bmp!.GetPixel(50, 50);
        Assert.Equal(0, px.Alpha);
    }

    // ── 辅助：构造最小合法 OFD ─────────────────────────────────

    private static MemoryStream BuildOfdWithDrawParam()
    {
        const string ofdXml = $@"<OFD xmlns=""{OFD_NS}"">
  <DocBody>
    <DocRoot>Doc_0/Document.xml</DocRoot>
  </DocBody>
</OFD>";

        const string documentXml = $@"<Document xmlns=""{OFD_NS}"">
  <CommonData>
    <PublicRes>Doc_0/PublicRes.xml</PublicRes>
  </CommonData>
  <Pages>
    <Page BaseLoc=""Pages/Page_0/Content.xml""/>
  </Pages>
</Document>";

        const string publicResXml = $@"<Res xmlns=""{OFD_NS}"">
  <DrawParams>
    <DrawParam ID=""7"" Alpha=""128"" Visible=""false"" Composite=""Multiply""/>
  </DrawParams>
</Res>";

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "OFD.xml", ofdXml);
            WriteEntry(zip, "Doc_0/Document.xml", documentXml);
            WriteEntry(zip, "Doc_0/PublicRes.xml", publicResXml);
            // 页面占位（本测试不消费页面内容）
            WriteEntry(zip, "Doc_0/Pages/Page_0/Content.xml",
                $@"<Page xmlns=""{OFD_NS}""><Content><Layer><PathObject Boundary=""0 0 1 1"" Fill=""true""><FillColor Value=""255 0 0""/><AbbreviatedData>M 0 0 L 1 0 L 1 1 L 0 1 C</AbbreviatedData></PathObject></Layer></Content></Page>");
        }
        return ms;
    }

    private static MemoryStream BuildOfdWithTransparentPath(string? alpha)
    {
        string alphaAttr = alpha is null ? "" : $" Alpha=\"{alpha}\"";

        const string ofdXml = $@"<OFD xmlns=""{OFD_NS}"">
  <DocBody>
    <DocRoot>Doc_0/Document.xml</DocRoot>
  </DocBody>
</OFD>";

        const string documentXml = $@"<Document xmlns=""{OFD_NS}"">
  <CommonData/>
  <Pages>
    <Page BaseLoc=""Pages/Page_0/Content.xml""/>
  </Pages>
</Document>";

        // 10mm×10mm 红色矩形，可选半透明
        string contentXml = $@"<Page xmlns=""{OFD_NS}"">
  <Content>
    <Layer>
      <PathObject Boundary=""0 0 10 10"" Fill=""true""{alphaAttr}>
        <FillColor Value=""255 0 0""/>
        <AbbreviatedData>M 0 0 L 10 0 L 10 10 L 0 10 C</AbbreviatedData>
      </PathObject>
    </Layer>
  </Content>
</Page>";

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "OFD.xml", ofdXml);
            WriteEntry(zip, "Doc_0/Document.xml", documentXml);
            WriteEntry(zip, "Doc_0/Pages/Page_0/Content.xml", contentXml);
        }
        return ms;
    }

    private static MemoryStream BuildOfdWithInvisibleLayer()
    {
        const string ofdXml = $@"<OFD xmlns=""{OFD_NS}"">
  <DocBody>
    <DocRoot>Doc_0/Document.xml</DocRoot>
  </DocBody>
</OFD>";

        const string documentXml = $@"<Document xmlns=""{OFD_NS}"">
  <CommonData>
    <PublicRes>Doc_0/PublicRes.xml</PublicRes>
  </CommonData>
  <Pages>
    <Page BaseLoc=""Pages/Page_0/Content.xml""/>
  </Pages>
</Document>";

        const string publicResXml = $@"<Res xmlns=""{OFD_NS}"">
  <DrawParams>
    <DrawParam ID=""7"" Visible=""false""/>
  </DrawParams>
</Res>";

        const string contentXml = $@"<Page xmlns=""{OFD_NS}"">
  <Content>
    <Layer DrawParam=""7"">
      <PathObject Boundary=""0 0 10 10"" Fill=""true"">
        <FillColor Value=""255 0 0""/>
        <AbbreviatedData>M 0 0 L 10 0 L 10 10 L 0 10 C</AbbreviatedData>
      </PathObject>
    </Layer>
  </Content>
</Page>";

        var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(zip, "OFD.xml", ofdXml);
            WriteEntry(zip, "Doc_0/Document.xml", documentXml);
            WriteEntry(zip, "Doc_0/PublicRes.xml", publicResXml);
            WriteEntry(zip, "Doc_0/Pages/Page_0/Content.xml", contentXml);
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
