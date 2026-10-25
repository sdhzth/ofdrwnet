using NetOfd.Converter;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using SkiaSharp;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// 注释层 / 复合对象渲染测试（2026-10-23 补齐）：
/// 对齐 Java ofdrw-converter AWTMaker.writePage 的「绘制注解对象」与 writeComposite。
/// 覆盖：①Annot.Appearance 外观渲染（电子印章/批注路径）；②Content 层 CompositeObject
/// 经 CompositeGraphicUnit 资源解引用渲染（此前 CTLayer.Wrap 丢弃该图元，为真实缺口）。
/// </summary>
public class AnnotationRenderTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_annot_render_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>构建单页文档并返回字节（页 1 含一行正文）。</summary>
    private static byte[] BuildSampleOfdBytes(string name)
    {
        var path = Path.Combine(OutDir(), name);
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("注释渲染测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    /// <summary>文字 + 矩形绘制器（与 Java 侧批注一致：蓝字 + 红框）。</summary>
    private class TextDrawer : IDrawer
    {
        public string Text { get; set; } = "批注内容";
        public bool DrawRect { get; set; }

        public void Draw(DrawContext ctx)
        {
            if (DrawRect)
            {
                ctx.StrokeStyle = "#FF0000";
                ctx.LineWidth = 0.353;
                ctx.StrokeRect(1, 1, 38, 18);
            }
            ctx.FillStyle = "#0000CC";
            ctx.Font = "4mm SimSun";
            ctx.FillText(Text, 3, 10);
        }
    }

    /// <summary>
    /// 注释外观应被渲染：在注释 Boundary 区域内检测到批注文字的蓝色像素。
    /// 修复前渲染器完全跳过注释层，该区域为纯白。
    /// </summary>
    [Fact]
    public void Render_AnnotationAppearance_ShouldPaintBlueTextInZone()
    {
        var ofd = BuildSampleOfdBytes("AnnRender_Basic.ofd");
        var annotated = OfdAnnotator.AddAnnotation(ofd, 1,
            new Annotation(10, 60, 40, 20, AnnotType.Path, new TextDrawer { Text = "批注渲染", DrawRect = true })
                .SetCreator("张医生"));

        var path = Path.Combine(OutDir(), "AnnRender_Basic_annotated.ofd");
        File.WriteAllBytes(path, annotated);

        using var maker = new OfdImageMaker(path);
        using var bmp = maker.MakePage(0, new ConverterOptions { PPM = 10 });
        Assert.NotNull(bmp);

        // 注释 Boundary(10,60,40,20)mm @PPM=10 → 像素 (100,600)-(500,800)
        int blue = 0;
        for (int y = 600; y < 800; y++)
        {
            for (int x = 100; x < 500; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Blue > 150 && c.Red < 100 && c.Green < 100) blue++;
            }
        }
        Assert.True(blue > 20, $"注释区域应渲染出批注文字（蓝色像素），实际 {blue}");
    }

    /// <summary>
    /// 对照：未加注释的同一样本，同一区域应为纯白（证明蓝像素确实来自注释层）。
    /// </summary>
    [Fact]
    public void Render_PlainSample_AnnotationZoneIsBlank()
    {
        var ofd = BuildSampleOfdBytes("AnnRender_Plain.ofd");
        var path = Path.Combine(OutDir(), "AnnRender_Plain.ofd");
        File.WriteAllBytes(path, ofd);

        using var maker = new OfdImageMaker(path);
        using var bmp = maker.MakePage(0, new ConverterOptions { PPM = 10 });
        Assert.NotNull(bmp);

        int blue = 0;
        for (int y = 600; y < 800; y++)
        {
            for (int x = 100; x < 500; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Blue > 150 && c.Red < 100 && c.Green < 100) blue++;
            }
        }
        Assert.Equal(0, blue);
    }

    /// <summary>
    /// Content 层 CompositeObject 应经 CompositeGraphicUnit 资源解引用渲染：
    /// 手工构造最小 OFD（图层内 CompositeObject → 矢量图资源 → 红色 ImageObject），
    /// 断言 Boundary 区域出现红色像素。修复前 CTLayer.Wrap 丢弃该图元（区域纯白）。
    /// </summary>
    [Fact]
    public void Render_CompositeObject_ShouldDrawVectorGContent()
    {
        var path = Path.Combine(OutDir(), "CompositeRender.ofd");
        BuildMinimalCompositeOfd(path);

        using var maker = new OfdImageMaker(path);
        using var bmp = maker.MakePage(0, new ConverterOptions { PPM = 10 });
        Assert.NotNull(bmp);

        // CompositeObject Boundary(10,10,40,30)mm @PPM=10 → 像素 (100,100)-(500,400)
        int red = 0;
        for (int y = 110; y < 390; y++)
        {
            for (int x = 110; x < 490; x++)
            {
                var c = bmp.GetPixel(x, y);
                if (c.Red > 150 && c.Green < 120 && c.Blue < 120) red++;
            }
        }
        Assert.True(red > 100, $"复合对象区域应渲染出红色图像，实际 {red}");
    }

    /// <summary>构造含 CompositeObject 的最小 OFD：图层引用矢量图资源（红色 100x100 图）。</summary>
    private static void BuildMinimalCompositeOfd(string path)
    {
        // 红色 100x100 PNG
        byte[] pngBytes;
        using (var bmp = new SKBitmap(100, 100))
        {
            using var canvas = new SKCanvas(bmp);
            canvas.Clear(SKColors.Red);
            using var img = SKImage.FromBitmap(bmp);
            using var data = img.Encode(SKEncodedImageFormat.Png, 100);
            pngBytes = data.ToArray();
        }

        const string ofdXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<OFD><DocBody><DocInfo/><DocRoot>Doc_0/Document.xml</DocRoot></DocBody></OFD>";
        const string documentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Document>" +
            "<CommonData MaxUnitID=\"100\">" +
            "<PageArea>0 0 210 297</PageArea>" +
            "<PublicRes>Doc_0/PublicRes.xml</PublicRes>" +
            "<DocumentRes>Doc_0/DocumentRes.xml</DocumentRes>" +
            "</CommonData>" +
            "<Pages><Page ID=\"1\" BaseLoc=\"Pages/Page_0/Content.xml\"/></Pages>" +
            "</Document>";
        const string publicResXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><Res/>";
        // 矢量图资源 20：内含红色 ImageObject（引用多媒体 10）
        const string documentResXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Res>" +
            "<MultiMedias><MultiMedia ID=\"10\" Type=\"Image\"><MediaFile>Res/img.png</MediaFile></MultiMedia></MultiMedias>" +
            "<CompositeGraphicUnits>" +
            "<CompositeGraphicUnit ID=\"20\" Width=\"40\" Height=\"30\">" +
            "<Content ID=\"21\">" +
            "<ImageObject ID=\"22\" Boundary=\"0 0 40 30\" ResourceID=\"10\"/>" +
            "</Content>" +
            "</CompositeGraphicUnit>" +
            "</CompositeGraphicUnits>" +
            "</Res>";
        // 图层内 CompositeObject 引用矢量图资源 20
        const string contentXml =
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
            "<Page>" +
            "<Area><PhysicalBox>0 0 210 297</PhysicalBox></Area>" +
            "<Content><Layer ID=\"2\">" +
            "<CompositeObject ID=\"30\" Boundary=\"10 10 40 30\" ResourceID=\"20\"/>" +
            "</Layer></Content>" +
            "</Page>";

        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        void Add(string entry, byte[] bytes)
        {
            using var s = zip.CreateEntry(entry).Open();
            s.Write(bytes, 0, bytes.Length);
        }
        Add("OFD.xml", Encoding.UTF8.GetBytes(ofdXml));
        Add("Doc_0/Document.xml", Encoding.UTF8.GetBytes(documentXml));
        Add("Doc_0/PublicRes.xml", Encoding.UTF8.GetBytes(publicResXml));
        Add("Doc_0/DocumentRes.xml", Encoding.UTF8.GetBytes(documentResXml));
        Add("Doc_0/Pages/Page_0/Content.xml", Encoding.UTF8.GetBytes(contentXml));
        Add("Doc_0/Res/img.png", pngBytes);
    }
}
