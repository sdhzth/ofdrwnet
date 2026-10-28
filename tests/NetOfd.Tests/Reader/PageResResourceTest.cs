using NetOfd.Converter;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicType;
using NetOfd.Pkg.Container;
using NetOfd.Reader;
using SkiaSharp;
using System.IO.Compression;
using System.Text;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// 读取侧页级资源（PageRes）解析能力测试。
/// <para>
/// 背景：GB/T 48666-2026 §7.2 a) 与表 1 规定「仅在一个页面内使用的资源一般在页面资源
/// 列表（Page_N/PageRes.xml）中注册」。Java ofdrw 读取侧长期忽略页级资源
/// （源码注释「页面资源，暂时忽略」），早期 NetOfd 读取侧亦然——导致读取第三方合规 OFD 时
/// 页级图像资源整体丢失（MultiMediaMap.Count = 0、GetImageData 返回 null），
/// 渲染器随之静默跳过该图。本测试锁定修复后的行为。
/// </para>
/// </summary>
public class PageResResourceTest
{
    private const string Ns = "http://www.ofdspec.org/2016";

    // ── 测试夹具：构造「第三方合规」OFD ──────────────────────────
    //
    //  Doc_0/Document.xml
    //    CommonData/PublicRes  → PublicRes.xml      （文档级：字体 + MediaFile="Res/docimg.png"）
    //    CommonData/DocumentRes→ DocumentRes.xml    （文档级，BaseLoc="Res" 形态 → logo.png）
    //    Pages/Page_0/Page_1/Page_2
    //  Doc_0/Pages/Page_1/Content.xml  → <ofd:PageRes>PageRes.xml</ofd:PageRes>
    //  Doc_0/Pages/Page_1/PageRes.xml  → 无 BaseLoc，MultiMedia ID=9 → Image_2.png（同目录）
    //  Doc_0/Pages/Page_1/Image_2.png
    //  Doc_0/Pages/Page_2/Content.xml  → <ofd:PageRes>PageRes.xml</ofd:PageRes>
    //  Doc_0/Pages/Page_2/PageRes.xml  → BaseLoc="Res"，MultiMedia ID=8 → Image_1.png
    //  Doc_0/Pages/Page_2/Res/Image_1.png
    //  Doc_0/Res/logo.png              （文档级，BaseLoc="Res" 形态）
    //  Doc_0/Res/docimg.png            （文档级，MediaFile="Res/docimg.png" 形态）

    /// <summary>4×4 纯红 PNG（真实 PNG 字节，供 SkiaSharp 解码）</summary>
    private static byte[] RedPng()
    {
        using var bmp = new SKBitmap(4, 4);
        bmp.Erase(SKColors.Red);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>文档级图像（BaseLoc="Res" 形态，Java ofdrw 样例同款）</summary>
    private static readonly byte[] LogoPng = RedPng();

    /// <summary>文档级图像（MediaFile="Res/xxx.png" 形态，NetOfd 写端同款）</summary>
    private static readonly byte[] DocImgPng = RedPng();

    /// <summary>页级图像（PageRes.xml 带 BaseLoc="Res"）</summary>
    private static readonly byte[] PageImg1Png = RedPng();

    /// <summary>页级图像（PageRes.xml 无 BaseLoc，文件与 PageRes.xml 同目录）</summary>
    private static readonly byte[] PageImg2Png = RedPng();

    private static string BuildThirdPartyOfd()
    {
        string path = Path.Combine(Path.GetTempPath(), "netofd_pageres_" + Guid.NewGuid().ToString("N") + ".ofd");

        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create, leaveOpen: true);

        void AddXml(string name, string xml) => AddEntry(zip, name, Encoding.UTF8.GetBytes(xml));
        void AddBin(string name, byte[] data) => AddEntry(zip, name, data);

        AddXml("OFD.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:OFD xmlns:ofd="{Ns}" Version="1.2" DocType="OFD">
              <ofd:DocBody>
                <ofd:DocRoot>/Doc_0/Document.xml</ofd:DocRoot>
              </ofd:DocBody>
            </ofd:OFD>
            """);

        AddXml("Doc_0/Document.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Document xmlns:ofd="{Ns}">
              <ofd:CommonData>
                <ofd:MaxUnitID>20</ofd:MaxUnitID>
                <ofd:PublicRes>PublicRes.xml</ofd:PublicRes>
                <ofd:DocumentRes>DocumentRes.xml</ofd:DocumentRes>
              </ofd:CommonData>
              <ofd:Pages>
                <ofd:Page ID="1" BaseLoc="Pages/Page_0/Content.xml"/>
                <ofd:Page ID="2" BaseLoc="Pages/Page_1/Content.xml"/>
                <ofd:Page ID="3" BaseLoc="Pages/Page_2/Content.xml"/>
              </ofd:Pages>
            </ofd:Document>
            """);

        // 文档级公共资源：字体 + 一个图像（MediaFile 显式带 "Res/" 前缀，NetOfd 写端形态）
        AddXml("Doc_0/PublicRes.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Res xmlns:ofd="{Ns}">
              <ofd:Fonts>
                <ofd:Font ID="5" FontName="宋体" FamilyName="宋体"/>
              </ofd:Fonts>
              <ofd:MultiMedias>
                <ofd:MultiMedia ID="13" Type="Image" Format="PNG">
                  <ofd:MediaFile>Res/docimg.png</ofd:MediaFile>
                </ofd:MultiMedia>
              </ofd:MultiMedias>
            </ofd:Res>
            """);
        AddBin("Doc_0/Res/docimg.png", DocImgPng);

        // 文档级资源：BaseLoc="Res" 形态（Java ofdrw 样例同款）
        AddXml("Doc_0/DocumentRes.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Res xmlns:ofd="{Ns}" BaseLoc="Res">
              <ofd:MultiMedias>
                <ofd:MultiMedia ID="12" Type="Image" Format="PNG">
                  <ofd:MediaFile>logo.png</ofd:MediaFile>
                </ofd:MultiMedia>
              </ofd:MultiMedias>
            </ofd:Res>
            """);
        AddBin("Doc_0/Res/logo.png", LogoPng);

        // 第 0 页：普通页，无页级资源
        AddXml("Doc_0/Pages/Page_0/Content.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Page xmlns:ofd="{Ns}">
              <ofd:Content>
                <ofd:Layer Type="Body" ID="2"/>
              </ofd:Content>
            </ofd:Page>
            """);

        // 第 1 页：页级资源（PageRes.xml 无 BaseLoc，媒体文件与 PageRes.xml 同目录）
        AddXml("Doc_0/Pages/Page_1/Content.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Page xmlns:ofd="{Ns}">
              <ofd:PageRes>PageRes.xml</ofd:PageRes>
              <ofd:Content>
                <ofd:Layer Type="Body" ID="3">
                  <ofd:ImageObject Boundary="10 10 40 40" ResourceID="9"/>
                </ofd:Layer>
              </ofd:Content>
            </ofd:Page>
            """);
        AddXml("Doc_0/Pages/Page_1/PageRes.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Res xmlns:ofd="{Ns}">
              <ofd:MultiMedias>
                <ofd:MultiMedia ID="9" Type="Image" Format="PNG">
                  <ofd:MediaFile>Image_2.png</ofd:MediaFile>
                </ofd:MultiMedia>
              </ofd:MultiMedias>
            </ofd:Res>
            """);
        AddBin("Doc_0/Pages/Page_1/Image_2.png", PageImg2Png);

        // 第 2 页：页级资源（PageRes.xml 带 BaseLoc="Res"，媒体文件在 Page_2/Res/ 下）
        AddXml("Doc_0/Pages/Page_2/Content.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Page xmlns:ofd="{Ns}">
              <ofd:PageRes>PageRes.xml</ofd:PageRes>
              <ofd:Content>
                <ofd:Layer Type="Body" ID="4">
                  <ofd:ImageObject Boundary="10 10 40 40" ResourceID="8"/>
                </ofd:Layer>
              </ofd:Content>
            </ofd:Page>
            """);
        AddXml("Doc_0/Pages/Page_2/PageRes.xml",
            $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <ofd:Res xmlns:ofd="{Ns}" BaseLoc="Res">
              <ofd:MultiMedias>
                <ofd:MultiMedia ID="8" Type="Image" Format="PNG">
                  <ofd:MediaFile>Image_1.png</ofd:MediaFile>
                </ofd:MultiMedia>
              </ofd:MultiMedias>
            </ofd:Res>
            """);
        AddBin("Doc_0/Pages/Page_2/Res/Image_1.png", PageImg1Png);

        zip.Dispose();
        return path;
    }

    private static void AddEntry(ZipArchive zip, string name, byte[] data)
    {
        var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
        using var s = entry.Open();
        s.Write(data, 0, data.Length);
    }

    // ── 用例 ────────────────────────────────────────────────

    /// <summary>
    /// 读取侧必须能加载页级资源文件（Page_N/PageRes.xml）中登记的多媒体资源。
    /// <para>修复前：MultiMediaMap.Count = 0、GetMultiMedia(8) = null。</para>
    /// </summary>
    [Fact]
    public void Reader_LoadsPageLevelMultiMedia()
    {
        string path = BuildThirdPartyOfd();
        try
        {
            using var reader = new OFDReader(path);
            var resMgt = reader.GetResourceManager();

            var mmMap = resMgt.GetMultiMediaMap();

            // 页级资源（Page_1 / Page_2 各一条）必须被加载
            Assert.True(mmMap.ContainsKey(8), "Page_2/PageRes.xml 登记的 ID=8 应被加载");
            Assert.True(mmMap.ContainsKey(9), "Page_1/PageRes.xml 登记的 ID=9 应被加载");

            // 文档级资源不得因页级支持而丢失
            Assert.True(mmMap.ContainsKey(12), "DocumentRes.xml 登记的 ID=12 应被加载");
            Assert.True(mmMap.ContainsKey(13), "PublicRes.xml 登记的 ID=13 应被加载");

            Assert.Equal("Image", mmMap[8].Type);
            Assert.Equal("PNG", mmMap[8].Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 页级资源（PageRes.xml 带 BaseLoc="Res"，媒体文件在 Page_2/Res/ 下）的字节必须可取。
    /// <para>修复前：GetImageData 返回 null（图像整体丢失，渲染器静默跳过）。</para>
    /// </summary>
    [Fact]
    public void Reader_ResolvesPageLevelImageBytes_WithBaseLoc()
    {
        string path = BuildThirdPartyOfd();
        try
        {
            using var reader = new OFDReader(path);
            var resMgt = reader.GetResourceManager();

            var mm = resMgt.GetMultiMedia(8);
            Assert.NotNull(mm);

            var bytes = resMgt.GetImageData(mm!);
            Assert.NotNull(bytes);
            Assert.Equal(PageImg1Png.Length, bytes!.Length);

            // 解析出的包内绝对路径应落在页级 Res 目录
            Assert.Equal("/Doc_0/Pages/Page_2/Res/Image_1.png", resMgt.GetMediaFilePath(8));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>
    /// 页级资源（PageRes.xml 无 BaseLoc，媒体文件与 PageRes.xml 同目录）的字节必须可取。
    /// </summary>
    [Fact]
    public void Reader_ResolvesPageLevelImageBytes_WithoutBaseLoc()
    {
        string path = BuildThirdPartyOfd();
        try
        {
            using var reader = new OFDReader(path);
            var resMgt = reader.GetResourceManager();

            var mm = resMgt.GetMultiMedia(9);
            Assert.NotNull(mm);

            var bytes = resMgt.GetImageData(mm!);
            Assert.NotNull(bytes);
            Assert.Equal(PageImg2Png.Length, bytes!.Length);

            Assert.Equal("/Doc_0/Pages/Page_1/Image_2.png", resMgt.GetMediaFilePath(9));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>文档级资源解析零回归：BaseLoc="Res" 与 "Res/xxx.png" 两种写法都应取到字节。</summary>
    [Theory]
    [InlineData(12)]   // DocumentRes.xml（BaseLoc="Res"）→ Doc_0/Res/logo.png
    [InlineData(13)]   // PublicRes.xml（MediaFile="Res/docimg.png"）→ Doc_0/Res/docimg.png
    public void Reader_KeepsDocumentLevelImageWorking(long id)
    {
        string path = BuildThirdPartyOfd();
        try
        {
            using var reader = new OFDReader(path);
            var resMgt = reader.GetResourceManager();

            var mm = resMgt.GetMultiMedia(id);
            Assert.NotNull(mm);

            var bytes = resMgt.GetImageData(mm!);
            Assert.NotNull(bytes);
            Assert.True(bytes!.Length > 0, "文档级图像字节不应为空");
            Assert.Equal("/Doc_0/Res/" + (id == 12 ? "logo.png" : "docimg.png"), resMgt.GetMediaFilePath(id));
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>页级资源必须能被渲染器取用（端到端：读取 → 资源 → 渲染出像素）。</summary>
    [Fact]
    public void Renderer_DrawsPageLevelImage()
    {
        string path = BuildThirdPartyOfd();
        try
        {
            using var reader = new OFDReader(path);
            var resMgt = reader.GetResourceManager();

            var page = reader.GetPage(2);
            Assert.NotNull(page);

            var opts = new ConverterOptions { PPM = 8, Background = SKColors.White };
            using var bmp = OfdPageRenderer.Render(page!, resMgt, 210, 297, opts);
            Assert.NotNull(bmp);

            // 页级图像（纯红）应被绘制 → 存在非白像素
            int redPixels = 0;
            using var pix = bmp!.PeekPixels();
            for (int y = 0; y < pix.Height; y++)
            {
                for (int x = 0; x < pix.Width; x++)
                {
                    var c = pix.GetPixelColor(x, y);
                    if (c.Red > 200 && c.Green < 80 && c.Blue < 80) redPixels++;
                }
            }
            Assert.True(redPixels > 100, $"页级图像应被渲染（红色像素），实际 {redPixels}");
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>轻量用法不得因新增页级解析而崩溃（Document 无 CommonData / Pages）。</summary>
    [Fact]
    public void ResourceManage_LightweightUsage_DoesNotThrow()
    {
        using var docDir = new DocDir("Doc_0", null);
        var resMgt = new ResourceManage(docDir, new Document());

        Assert.Empty(resMgt.GetMultiMediaMap());
        Assert.Empty(resMgt.GetFontMap());
        Assert.Empty(resMgt.GetColorSpaceMap());
        Assert.Empty(resMgt.GetDrawParamMap());
        Assert.Empty(resMgt.GetCompositeGraphicUnitMap());
    }

    /// <summary>无页级资源声明的文档（页级 PageRes 缺失）不得影响文档级资源加载。</summary>
    [Fact]
    public void Reader_DocumentWithoutPageRes_StillLoadsDocumentResources()
    {
        string path = BuildThirdPartyOfd();
        try
        {
            using var reader = new OFDReader(path);
            var resMgt = reader.GetResourceManager();

            // 第 0 页没有 PageRes 声明 → 不应抛异常
            var page0 = reader.GetPage(0);
            Assert.NotNull(page0);

            var font = resMgt.GetFont(5);
            Assert.NotNull(font);
            Assert.Equal("宋体", font!.FamilyName);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
