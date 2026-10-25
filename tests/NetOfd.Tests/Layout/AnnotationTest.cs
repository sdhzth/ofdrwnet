using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 注释（Annotation）功能测试：向已有 OFD 文档添加批注。
/// 对应 Java: ofdrw-layout edit.Annotation / AnnotationRender
/// </summary>
public class AnnotationTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_annot_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>构建两页的样例文档并返回字节。</summary>
    private static byte[] BuildSampleOfdBytes(string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            // OFDDoc 的流式内容渲染在最新页上，先建两页再写入内容
            doc.AddPage();
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("注释功能测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    private static byte[]? ReadEntry(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var e = za.GetEntry(entryName);
        if (e == null) return null;
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    private static XDocument EntryXml(byte[] ofd, string entryName)
        => XDocument.Parse(Encoding.UTF8.GetString(ReadEntry(ofd, entryName)!));

    /// <summary>文字 + 矩形绘制器。</summary>
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

    // ──────────────────────────────────────────────────────────────

    /// <summary>添加注释应创建注释结构并更新文档入口。</summary>
    [Fact]
    public void AddAnnotation_ShouldCreateStructureAndEntry()
    {
        var ofd = BuildSampleOfdBytes("Ann_Basic.ofd");

        var annotated = OfdAnnotator.AddAnnotation(ofd, 1,
            new Annotation(10, 20, 40, 20, AnnotType.Path, new TextDrawer { Text = "请核对诊断", DrawRect = true })
                .SetCreator("张医生")
                .SetRemark("复核意见"));

        // 注释入口文件与分页注释文件存在
        Assert.NotNull(ReadEntry(annotated, "Doc_0/Annots/Annotations.xml"));
        var annotFileBytes = ReadEntry(annotated, "Doc_0/Annots/Page_1/Annot_1.xml");
        Assert.NotNull(annotFileBytes);

        // 分页注释文件内容校验
        var pageAnnot = new PageAnnot(XDocument.Parse(Encoding.UTF8.GetString(annotFileBytes!)).Root!);
        var annots = pageAnnot.GetAnnots();
        Assert.Single(annots);
        var annot = annots[0];
        Assert.Equal(AnnotType.Path, annot.Type);
        Assert.Equal("张医生", annot.Creator);
        Assert.Equal(DateOnly.FromDateTime(DateTime.Now), annot.LastModDate);
        Assert.Equal("复核意见", annot.Remark);
        Assert.NotNull(annot.ID);

        // 外观边界与内容
        var appearance = annot.Appearance!;
        Assert.Equal(new NetOfd.Core.BasicType.STBox(10, 20, 40, 20), appearance.Boundary);
        var texts = appearance.Element.Elements()
            .Where(e => e.Name.LocalName == "TextObject").ToList();
        Assert.NotEmpty(texts);
        Assert.Contains(texts, t => t.Value.Contains("请核对诊断"));
        Assert.Contains(appearance.Element.Elements(), e => e.Name.LocalName == "PathObject");

        // Document.xml 登记注释入口
        var docXml = EntryXml(annotated, "Doc_0/Document.xml");
        var annLoc = docXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "Annotations")?.Value;
        Assert.Equal("/Doc_0/Annots/Annotations.xml", annLoc);

        // OFD.xml 与 Document.xml 的最大对象 ID 均不小于注释 ID
        var ofdXml = EntryXml(annotated, "OFD.xml");
        var maxUnit = long.Parse(ofdXml.Descendants().First(e => e.Name.LocalName == "MaxUnitID").Value);
        Assert.True(maxUnit >= annot.ID!.Value, "MaxUnitID 应覆盖注释对象 ID");
        var docMaxUnit = long.Parse(docXml.Descendants().First(e => e.Name.LocalName == "MaxUnitID").Value);
        Assert.True(docMaxUnit >= annot.ID.Value, "CommonData MaxUnitID 应覆盖注释对象 ID");
    }

    /// <summary>同一页两次添加注释应追加到同一分页注释文件。</summary>
    [Fact]
    public void AddAnnotation_TwiceOnSamePage_ShouldAppendToSameFile()
    {
        var ofd = BuildSampleOfdBytes("Ann_Twice.ofd");

        var once = OfdAnnotator.AddAnnotation(ofd, 1,
            new Annotation(5, 5, 30, 12, AnnotType.Highlight, new TextDrawer { Text = "第一条" }));
        var twice = OfdAnnotator.AddAnnotation(once, 1,
            new Annotation(50, 60, 30, 12, AnnotType.Path, new TextDrawer { Text = "第二条" }));

        // 只有一个分页注释文件，且包含两条注释
        var annotFiles = ListEntries(twice, "Doc_0/Annots/Page_1/")
            .Where(n => n.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)).ToList();
        var file = Assert.Single(annotFiles);
        var pageAnnot = new PageAnnot(
            XDocument.Parse(Encoding.UTF8.GetString(ReadEntry(twice, file)!)).Root!);
        Assert.Equal(2, pageAnnot.GetAnnots().Count);

        // 注释入口仍只有一个 Page 条目且指向该文件
        var annotations = new Annotations(EntryXml(twice, "Doc_0/Annots/Annotations.xml").Root!);
        var record = annotations.GetByPageId(GetPageId(twice, 1).ToString());
        Assert.NotNull(record);
        Assert.EndsWith(file, record!.FileLoc!.Value.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>不同页添加注释应在入口文件中形成两个条目。</summary>
    [Fact]
    public void AddAnnotation_DifferentPages_TwoRecords()
    {
        var ofd = BuildSampleOfdBytes("Ann_Pages.ofd");
        var withP1 = OfdAnnotator.AddAnnotation(ofd, 1,
            new Annotation(5, 5, 30, 12, AnnotType.Path, new TextDrawer { Text = "P1" }));
        var withP2 = OfdAnnotator.AddAnnotation(withP1, 2,
            new Annotation(5, 5, 30, 12, AnnotType.Path, new TextDrawer { Text = "P2" }));

        var annotations = new Annotations(EntryXml(withP2, "Doc_0/Annots/Annotations.xml").Root!);
        Assert.Equal(2, annotations.GetPages().Count);
        Assert.NotNull(annotations.GetByPageId(GetPageId(withP2, 1).ToString()));
        Assert.NotNull(annotations.GetByPageId(GetPageId(withP2, 2).ToString()));
        Assert.NotNull(ReadEntry(withP2, "Doc_0/Annots/Page_1/Annot_1.xml"));
        Assert.NotNull(ReadEntry(withP2, "Doc_0/Annots/Page_2/Annot_1.xml"));
    }

    /// <summary>页码超出范围应抛出异常。</summary>
    [Fact]
    public void AddAnnotation_PageOutOfRange_ShouldThrow()
    {
        var ofd = BuildSampleOfdBytes("Ann_Range.ofd");
        Assert.Throws<ArgumentException>(() =>
            OfdAnnotator.AddAnnotation(ofd, 99,
                new Annotation(1, 1, 10, 10, AnnotType.Path, new TextDrawer())));
    }

    /// <summary>注释模型属性序列化往返。</summary>
    [Fact]
    public void Annotation_ModelRoundtrip()
    {
        var annotation = new Annotation(1, 2, 30, 15, AnnotType.Stamp, new TextDrawer())
            .SetCreator("李医生")
            .SetLastModDate(new DateOnly(2026, 8, 21))
            .SetSubtype("Seal")
            .SetVisible(false)
            .SetPrint(false)
            .SetNoZoom(true)
            .SetNoRotate(true)
            .SetReadOnly(true)
            .SetRemark("备注信息")
            .AddParameter("k1", "v1")
            .AddParameter("k2", "v2");

        var annot = annotation.Build(100);
        var parsed = new Annot(XDocument.Parse(
            Encoding.UTF8.GetString(OfdAnnotator.ToUtf8Bytes(annot.Element))).Root!);

        Assert.Equal(100, parsed.ID!.Value);
        Assert.Equal(AnnotType.Stamp, parsed.Type);
        Assert.Equal("李医生", parsed.Creator);
        Assert.Equal(new DateOnly(2026, 8, 21), parsed.LastModDate);
        Assert.Equal("Seal", parsed.Subtype);
        Assert.False(parsed.Visible);       // 显式设置 false 后可读出 false
        Assert.False(parsed.Print);
        Assert.True(parsed.NoZoom);
        Assert.True(parsed.NoRotate);
        Assert.True(parsed.ReadOnly);
        Assert.Equal("备注信息", parsed.Remark);
        var ps = parsed.GetParameters();
        Assert.Equal("v1", ps["k1"]);
        Assert.Equal("v2", ps["k2"]);
        Assert.Equal(new NetOfd.Core.BasicType.STBox(1, 2, 30, 15), parsed.Appearance?.Boundary);
    }

    /// <summary>未显式设置的布尔属性不应写入（读取时使用规范默认值）。</summary>
    [Fact]
    public void Annotation_UnsetBooleans_ShouldUseSpecDefaults()
    {
        var annot = new Annotation(0, 0, 10, 10, AnnotType.Link, new TextDrawer()).Build(7);
        Assert.Equal("OFD R&W", annot.Creator); // 默认创建者
        Assert.Null(annot.Element.Attribute("Visible"));
        Assert.Null(annot.Element.Attribute("Print"));
        Assert.Null(annot.Element.Attribute("ReadOnly"));
        Assert.Null(annot.Element.Attribute("NoZoom"));
        Assert.Null(annot.Element.Attribute("NoRotate"));
    }

    /// <summary>注释中的自定义字体应登记到 PublicRes 并被文字对象引用。</summary>
    [Fact]
    public void AddAnnotation_CustomFont_ShouldDeclareInPublicRes()
    {
        var ofd = BuildSampleOfdBytes("Ann_Font.ofd");
        var annotated = OfdAnnotator.AddAnnotation(ofd, 1,
            new Annotation(10, 10, 40, 16, AnnotType.Path, new KaiTiDrawer()));

        var docXml = EntryXml(annotated, "Doc_0/Document.xml");
        // CommonData 中应登记 PublicRes 路径
        var pubResLoc = docXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "PublicRes")?.Value;
        Assert.NotNull(pubResLoc);
        var resPath = ResolveResPath(pubResLoc!);
        var resXml = EntryXml(annotated, resPath);
        var font = resXml.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Font"
                                 && string.Equals(e.Attribute("FontName")?.Value, "KaiTi", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(font);

        // 注释文字对象引用的字体 ID 应等于声明的 Font ID
        var fontId = font!.Attribute("ID")!.Value;
        var pageAnnot = new PageAnnot(EntryXml(annotated, "Doc_0/Annots/Page_1/Annot_1.xml").Root!);
        var textObj = pageAnnot.GetAnnots()[0].Appearance!.Element.Elements()
            .First(e => e.Name.LocalName == "TextObject");
        Assert.Equal(fontId, textObj.Attribute("Font")!.Value);
    }

    /// <summary>注释中绘制的图片应复制进包内并在 DocumentRes 中声明。</summary>
    [Fact]
    public void AddAnnotation_DrawImage_ShouldRegisterMedia()
    {
        // 生成一张测试图片
        var imgPath = Path.Combine(OutDir(), "annot_test.png");
        using (var bmp = new SkiaSharp.SKBitmap(20, 10))
        using (var canvas = new SkiaSharp.SKCanvas(bmp))
        using (var img = SkiaSharp.SKImage.FromBitmap(bmp))
        using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100))
        using (var fs = File.OpenWrite(imgPath))
        {
            canvas.Clear(SkiaSharp.SKColors.Red);
            data.SaveTo(fs);
        }

        var ofd = BuildSampleOfdBytes("Ann_Image.ofd");
        var annotated = OfdAnnotator.AddAnnotation(ofd, 1,
            new Annotation(10, 10, 30, 16, AnnotType.Path, new ImageDrawer(imgPath)));

        var docXml = EntryXml(annotated, "Doc_0/Document.xml");
        var docResLoc = docXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "DocumentRes")?.Value;
        Assert.NotNull(docResLoc);
        var resPath = ResolveResPath(docResLoc!);
        var resXml = EntryXml(annotated, resPath);
        Assert.Contains(resXml.Descendants(), e => e.Name.LocalName == "MultiMedia");

        // 图片字节存在于包内 Res 目录
        var mediaFile = resXml.Descendants().First(e => e.Name.LocalName == "MediaFile").Value;
        var entry = mediaFile.StartsWith('/') ? mediaFile.TrimStart('/') : $"Doc_0/{mediaFile}";
        Assert.NotNull(ReadEntry(annotated, entry));

        // 图像对象引用的媒体 ID 与声明一致
        var mediaId = resXml.Descendants().First(e => e.Name.LocalName == "MultiMedia").Attribute("ID")!.Value;
        var pageAnnot = new PageAnnot(EntryXml(annotated, "Doc_0/Annots/Page_1/Annot_1.xml").Root!);
        var imgObj = pageAnnot.GetAnnots()[0].Appearance!.Element.Elements()
            .First(e => e.Name.LocalName == "ImageObject");
        Assert.Equal(mediaId, imgObj.Attribute("ResourceID")!.Value);
    }

    // ── 辅助 ─────────────────────────────────────────────────────

    private class KaiTiDrawer : IDrawer
    {
        public void Draw(DrawContext ctx)
        {
            ctx.Font = "3mm KaiTi";
            ctx.FillText("楷体批注", 2, 8);
        }
    }

    private class ImageDrawer : IDrawer
    {
        private readonly string _path;
        public ImageDrawer(string path) => _path = path;

        public void Draw(DrawContext ctx) =>
            ctx.DrawImageFromFile(_path, 2, 2, 20, 10);
    }

    private static System.Collections.Generic.List<string> ListEntries(byte[] ofd, string prefix)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        return za.Entries
            .Select(e => e.FullName.Replace('\\', '/'))
            .Where(n => n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
    }

    private static long GetPageId(byte[] ofd, int pageNumber)
    {
        var docXml = EntryXml(ofd, "Doc_0/Document.xml");
        var pagesEl = docXml.Descendants().First(e => e.Name.LocalName == "Pages" && e.Parent?.Name.LocalName == "Document");
        var pages = new NetOfd.Core.BasicStructure.PageTree.Pages(pagesEl);
        return pages.GetPages()[pageNumber - 1].ID!.Value;
    }

    private static string ResolveResPath(string loc)
    {
        var n = loc.Trim().TrimStart('/').Replace('\\', '/');
        return n.Contains('/') ? n : $"Doc_0/{n}";
    }
}
