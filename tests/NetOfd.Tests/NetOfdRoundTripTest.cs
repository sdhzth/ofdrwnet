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
/// .NET 写出 → .NET 读回（round-trip）验证：用 NetOfd.Layout 生成的 <c>dotnet_sample.ofd</c>，
/// 验证 NetOfd.Reader 能正确打开、遍历两页、抽取全部中文文本、加载字体与嵌入图像，
/// 并用 OfdImageMaker 完成端到端渲染。该文件此前已通过 Java OFDRW 反向读取验证
/// （E:\电子病历\ofd-gen 的 ReadCheck.java 成功读取 2 页、8 段中文文本）。
///
/// 这是"NetOfd 写出格式自洽"的闭环证明：自己写、自己读、Java 也能读。
/// </summary>
public class NetOfdRoundTripTest
{
    private static string SamplePath()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", "dotnet_sample.ofd");
        Assert.True(File.Exists(path), $"测试资源缺失，请确认 csproj 已 CopyToOutputDirectory：{path}");
        return path;
    }

    [Fact]
    public void Parse_DotNetGeneratedOfD_OpensAndCountsPages()
    {
        using var reader = new OFDReader(SamplePath());

        // OFDDoc 生成 2 页
        Assert.Equal(2, reader.GetPageCount());

        // 逐页均可成功加载（BaseLoc 路径解析 + 页面 Content 反序列化）
        for (int i = 0; i < reader.GetPageCount(); i++)
        {
            var page = reader.GetPage(i);
            Assert.NotNull(page);
        }

        Assert.NotNull(reader.GetDocument());
    }

    [Fact]
    public void Parse_DotNetGeneratedOfD_ExtractsChineseText()
    {
        using var reader = new OFDReader(SamplePath());

        var allText = ContentExtractor.ExtractAllPagesText(reader.GetPages());
        var joined = string.Join("\n", allText);

        // 第 0 页：标题栏 + 彩色文本 + 矩形标注
        Assert.Contains("NetOfd 生成示例文档", joined);
        Assert.Contains("红色示例文本", joined);
        Assert.Contains("绿色示例文本", joined);
        Assert.Contains("蓝色粗体示例文本", joined);
        Assert.Contains("边框框", joined);
        Assert.Contains("黄填充蓝框", joined);
        Assert.Contains("嵌入图片", joined);

        // 第 1 页：纯文本多页遍历
        Assert.Contains("第二页：仅包含文本，用于校验多页遍历与文本提取。", joined);
        Assert.Contains("Java OFDRW 应当能够从此 OFD 中正确读取两页并提取全部文字。", joined);
    }

    [Fact]
    public void Parse_DotNetGeneratedOfD_LoadsImage()
    {
        using var reader = new OFDReader(SamplePath());
        var resMgt = reader.GetResourceManager();

        // ResManager.AddMedia 采用字节内嵌（而非物理路径复制），图像资源应可加载
        var mmMap = resMgt.GetMultiMediaMap();
        var imageEntry = mmMap.Values.FirstOrDefault(m => m.Type == "Image");
        Assert.NotNull(imageEntry);

        var imageData = resMgt.GetImageData(imageEntry!);
        Assert.NotNull(imageData);
        Assert.True(imageData!.Length > 0, "嵌入图像（logo_dotnet.png）应非空，验证 ResManager 字节内嵌");
    }

    /// <summary>
    /// 端到端渲染：用 OfdImageMaker 将 .NET 生成的 dotnet_sample.ofd 逐页渲染为位图，
    /// 验证渲染管线在 NetOfd 自己产出的文档上同样可用，并落盘第 0 页便于人工目检。
    /// </summary>
    [Fact]
    public void Render_DotNetGeneratedOfD_ProducesNonEmptyBitmaps()
    {
        using var maker = new OfdImageMaker(SamplePath());
        Assert.Equal(2, maker.PageCount);

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

            if (i == 0)
            {
                string outPath = Path.Combine(AppContext.BaseDirectory, "render_dotnet_page0.png");
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
