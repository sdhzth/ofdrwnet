using NetOfd.Converter;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.Resources;
using NetOfd.Reader;
using SkiaSharp;
using System.Linq;
using Xunit;

namespace NetOfd.Tests;

/// <summary>
/// 跨语言解析验证：用 Java OFDRW（ofdrw-layout 2.4.0）生成的真实 OFD 文档，
/// 验证 .NET 端 NetOfd 能否正确打开、遍历多页、抽取中文文本、加载字体与图像资源。
///
/// 该样本由 E:\电子病历\ofd-gen 通过 Java 生成（见 ofd-gen/src/main/java/Gen.java），
/// 全面使用 <c>ofd:</c> 命名空间前缀（Java OFDRW 默认写出方式），
/// 覆盖：命名空间容错、多页遍历、TextCode 中文抽取、Font/Image 资源加载。
/// </summary>
public class JavaGeneratedOfDTest
{
    private static string SamplePath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "sample.ofd");
        Assert.True(File.Exists(path), $"测试资源缺失，请确认 csproj 已 CopyToOutputDirectory：{path}");
        return path;
    }

    [Fact]
    public void Parse_JavaGeneratedOfD_OpensAndCountsPages()
    {
        using var reader = new OFDReader(SamplePath());

        // Document.xml 声明 3 页（Java OFDRW 自动分页：标题页 + 矩形页 + 纯文本页）
        Assert.Equal(3, reader.GetPageCount());

        // 逐页均可成功加载（命名空间容错 + BaseLoc 路径解析）
        for (int i = 0; i < reader.GetPageCount(); i++)
        {
            var page = reader.GetPage(i);
            Assert.NotNull(page);
        }

        Assert.NotNull(reader.GetDocument());
    }

    [Fact]
    public void Parse_JavaGeneratedOfD_ExtractsChineseText()
    {
        using var reader = new OFDReader(SamplePath());

        var allText = ContentExtractor.ExtractAllPagesText(reader.GetPages());
        var joined = string.Join("\n", allText);

        // 第 0 页标题（含全角括号与箭头等特殊字符，验证 Unicode 保真）
        Assert.Contains("OFDRW 生成示例文档", joined);
        Assert.Contains("Java", joined);
        Assert.Contains("解析验证", joined);

        // 第 1 页：纯色矩形 + 说明
        Assert.Contains("第二页：两个纯色矩形与一行说明文字。", joined);

        // 第 2 页：多页遍历与文本提取校验
        Assert.Contains("第三页：仅包含文本，用于校验多页遍历与文本提取。", joined);
        Assert.Contains("NetOfd 应能从该 OFD 中正确读取三页并提取全部文字。", joined);
    }

    [Fact]
    public void Parse_JavaGeneratedOfD_LoadsFontsAndImage()
    {
        using var reader = new OFDReader(SamplePath());
        var resMgt = reader.GetResourceManager();

        // PublicRes.xml：<Font FontName="宋体" FamilyName="宋体" ID="5"/>
        var font = resMgt.GetFont(5);
        Assert.NotNull(font);
        Assert.Equal("宋体", font!.FamilyName);

        // DocumentRes.xml：<MultiMedia Type="Image" Format="PNG" ID="12"><MediaFile>logo.png</MediaFile>
        var mmMap = resMgt.GetMultiMediaMap();
        Assert.True(mmMap.ContainsKey(12), "DocumentRes 应含 ID=12 的图像资源");
        var imageData = resMgt.GetImageData(mmMap[12]);
        Assert.NotNull(imageData);
        Assert.True(imageData!.Length > 0, "图像数据（logo.png）应非空，验证图像资源解析");

        // 第 0 页存在 ImageObject ResourceID=12，引用上述图像
        var page0 = reader.GetPage(0);
        Assert.NotNull(page0);
        var bodyLayer = page0!.Content?.GetBodyLayer();
        Assert.NotNull(bodyLayer);
        var imageObjects = bodyLayer!.GetGraphicObjects().OfType<CTImage>().ToList();
        Assert.NotEmpty(imageObjects);
        Assert.Contains(imageObjects, img => img.ResourceID?.Value == 12);
    }

    /// <summary>
    /// 端到端渲染：用 OfdImageMaker 将 Java 生成的 sample.ofd 逐页渲染为位图，
    /// 验证整个渲染管线（页面尺寸解析 → 多图层 → 文本/图像/路径绘制）在真实 Java 输出上可用。
    /// 同时落盘第 0 页 PNG 供人工目检。
    /// </summary>
    [Fact]
    public void Render_JavaGeneratedOfD_ProducesNonEmptyBitmaps()
    {
        using var maker = new OfdImageMaker(SamplePath());
        Assert.Equal(3, maker.PageCount);

        var opts = new ConverterOptions { PPM = 8, Background = SKColors.White };
        int totalNonWhite = 0;

        for (int i = 0; i < maker.PageCount; i++)
        {
            using var bmp = maker.MakePage(i, opts);
            Assert.NotNull(bmp);
            // A4 @8ppm ≈ 1680×2376
            Assert.True(bmp!.Width > 1000 && bmp.Height > 1000,
                $"页面{i}位图尺寸应约为 A4@8ppm，实际 {bmp.Width}×{bmp.Height}");

            int nonWhite = CountNonWhite(bmp);
            Assert.True(nonWhite > 1000,
                $"页面{i}应渲染出可见内容（非白像素），实际={nonWhite}");
            totalNonWhite += nonWhite;

            // 落盘首页面便于人工目检
            if (i == 0)
            {
                string outPath = Path.Combine(AppContext.BaseDirectory, "render_sample_page0.png");
                using var img = SKImage.FromBitmap(bmp);
                using var data = img.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(outPath, data.ToArray());
            }
        }

        Assert.True(totalNonWhite > 5000,
            $"整文档应渲染出大量可见内容，实际={totalNonWhite}");
    }

    /// <summary>统计位图中非白（R/G/B 任一 &lt; 250）像素数量。</summary>
    private static int CountNonWhite(SKBitmap bmp)
    {
        int count = 0;
        using var pix = bmp.PeekPixels();
        var info = pix.Info;
        for (int y = 0; y < info.Height; y++)
        {
            for (int x = 0; x < info.Width; x++)
            {
                var c = pix.GetPixelColor(x, y);
                if (c.Red < 250 || c.Green < 250 || c.Blue < 250) count++;
            }
        }
        return count;
    }
}
