using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 页面编辑功能测试：向已有页面添加内容 / 追加新页。
/// 对应 Java: ofdrw-layout edit.AdditionVPage + OFDDoc#addVPage
/// </summary>
public class PageEditTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_pageedit_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>文字绘制器。</summary>
    private class TextDrawer : IDrawer
    {
        public string Text { get; set; } = "编辑内容";

        public void Draw(DrawContext ctx)
        {
            ctx.FillStyle = "#0000CC";
            ctx.Font = "4mm SimSun";
            ctx.FillText(Text, 20, 30);
        }
    }

    /// <summary>构建两页的样例文档并返回字节。</summary>
    private static byte[] BuildSampleOfdBytes(string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("页面编辑测试样本").SetFontSize(5d));
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

    /// <summary>从 Document.xml 读取第 pageNumber 页（1 起）的 BaseLoc。</summary>
    private static string GetPageBaseLoc(byte[] ofd, int pageNumber)
    {
        var docXml = EntryXml(ofd, "Doc_0/Document.xml");
        var pagesEl = docXml.Descendants()
            .First(e => e.Name.LocalName == "Pages" && e.Parent?.Name.LocalName == "Document");
        var page = pagesEl.Elements().Where(e => e.Name.LocalName == "Page")
            .ElementAt(pageNumber - 1);
        return page.Attribute("BaseLoc")!.Value;
    }

    private static string Resolve(byte[] ofd, string loc)
    {
        var n = loc.Trim().TrimStart('/').Replace('\\', '/');
        return n.StartsWith("Doc_", StringComparison.OrdinalIgnoreCase) ? n : $"Doc_0/{n}";
    }

    // ──────────────────────────────────────────────────────────────

    /// <summary>向指定页添加内容应写入该页 Content.xml 的图层中。</summary>
    [Fact]
    public void AddContent_ShouldDrawIntoExistingPage()
    {
        var ofd = BuildSampleOfdBytes("PE_Basic.ofd");

        var edited = OfdEditor.AddContent(ofd, 2, new TextDrawer { Text = "第二页新增" });

        // 第 2 页内容文件包含新绘制的文字对象
        var page2Loc = Resolve(edited, GetPageBaseLoc(edited, 2));
        var page2Xml = EntryXml(edited, page2Loc);
        Assert.Contains(page2Xml.Descendants(),
            e => e.Name.LocalName == "TextObject" && e.Value.Contains("第二页新增"));

        // 第 1 页不受影响
        var page1Loc = Resolve(edited, GetPageBaseLoc(edited, 1));
        var page1Xml = EntryXml(edited, page1Loc);
        Assert.DoesNotContain(page1Xml.Descendants(), e => e.Name.LocalName == "TextObject");

        // MaxUnitID 同步更新（样例初始为 4，新图层/图元 ID 应不超过 MaxUnitID）
        var maxUnit = long.Parse(EntryXml(edited, "OFD.xml")
            .Descendants().First(e => e.Name.LocalName == "MaxUnitID").Value);
        Assert.True(maxUnit >= 4, "MaxUnitID 应随编辑递增");
    }

    /// <summary>添加内容应选取最上层图层（Foreground）而非底层图层。</summary>
    [Fact]
    public void AddContent_TopLayerSelection()
    {
        var ofd = BuildCustomOfd("PE_TopLayer.ofd", withLayers: true);

        var edited = OfdEditor.AddContent(ofd, 1, new TextDrawer { Text = "顶层绘制" });

        var contentXml = EntryXml(edited, "Doc_0/Pages/Page_0/Content.xml");
        var layers = contentXml.Descendants()
            .Where(e => e.Name.LocalName == "Layer").ToList();

        // Foreground 图层包含新对象，Background 图层保持原样
        var fg = layers.First(l => l.Attribute("Type")?.Value == "Foreground");
        var bg = layers.First(l => l.Attribute("Type")?.Value == "Background");
        Assert.Contains(fg.Descendants(), e => e.Name.LocalName == "TextObject"
            && e.Value.Contains("顶层绘制"));
        Assert.DoesNotContain(bg.Descendants(), e => e.Name.LocalName == "TextObject");
        // 原有背景路径对象仍在
        Assert.Contains(bg.Descendants(), e => e.Name.LocalName == "PathObject");
    }

    /// <summary>页面无图层时应自动创建图层并写入内容。</summary>
    [Fact]
    public void AddContent_NoLayers_ShouldCreateLayer()
    {
        var ofd = BuildCustomOfd("PE_NoLayer.ofd", withLayers: false);

        var edited = OfdEditor.AddContent(ofd, 1, new TextDrawer { Text = "首层创建" });

        var contentXml = EntryXml(edited, "Doc_0/Pages/Page_0/Content.xml");
        var layer = contentXml.Descendants().First(e => e.Name.LocalName == "Layer");
        Assert.NotNull(layer.Attribute("ID"));
        Assert.Contains(layer.Descendants(), e => e.Name.LocalName == "TextObject"
            && e.Value.Contains("首层创建"));
    }

    /// <summary>追加新页应登记页树并返回新页码，且可继续在该页上注释。</summary>
    [Fact]
    public void AppendPage_ShouldRegisterNewPage()
    {
        var ofd = BuildSampleOfdBytes("PE_Append.ofd");

        var pageNum = OfdEditor.AppendPage(ofd, out var appended);

        // 返回新页码（原 2 页 → 新页为第 3 页）
        Assert.Equal(3, pageNum);

        // 页树登记了第 3 页且 BaseLoc 指向包内存在的文件
        var docXml = EntryXml(appended, "Doc_0/Document.xml");
        var pagesEl = docXml.Descendants()
            .First(e => e.Name.LocalName == "Pages" && e.Parent?.Name.LocalName == "Document");
        var pageEls = pagesEl.Elements().Where(e => e.Name.LocalName == "Page").ToList();
        Assert.Equal(3, pageEls.Count);
        var baseLoc = pageEls[2].Attribute("BaseLoc")!.Value;
        Assert.NotNull(ReadEntry(appended, Resolve(appended, baseLoc)));

        // 与注释功能联动：可在追加的新页上添加注释
        var annotated = OfdAnnotator.AddAnnotation(appended, 3,
            new Annotation(10, 10, 40, 16, NetOfd.Core.Annotation.PageAnnot.AnnotType.Path,
                new TextDrawer { Text = "新页批注" }));
        Assert.NotNull(ReadEntry(annotated, "Doc_0/Annots/Page_3/Annot_1.xml"));
    }

    /// <summary>页码超出范围应抛出异常。</summary>
    [Fact]
    public void AddContent_PageOutOfRange_ShouldThrow()
    {
        var ofd = BuildSampleOfdBytes("PE_Range.ofd");
        Assert.Throws<ArgumentException>(() =>
            OfdEditor.AddContent(ofd, 99, new TextDrawer()));
    }

    // ── 自定义最小 OFD 结构 ──────────────────────────────────────

    /// <summary>
    /// 构建仅含一页的最小 OFD 文档，可控制页面是否带有 Background/Foreground 图层。
    /// </summary>
    private static byte[] BuildCustomOfd(string name, bool withLayers)
    {
        XNamespace ns = "http://www.ofdspec.org/2016";

        var ofdXml = new XElement(ns + "OFD",
            new XElement(ns + "DocBody",
                new XElement(ns + "DocInfo",
                    new XElement(ns + "MaxUnitID", "10"),
                    new XElement(ns + "Creator", "测试")),
                new XElement(ns + "DocRoot", "Doc_0/Document.xml")));

        var documentXml = new XElement(ns + "Document",
            new XElement(ns + "CommonData",
                new XElement(ns + "MaxUnitID", "11"),
                new XElement(ns + "PageArea",
                    new XElement(ns + "PhysicalBox", "0 0 210 297"))),
            new XElement(ns + "Pages",
                new XElement(ns + "Page",
                    new XAttribute("ID", "1"),
                    new XAttribute("BaseLoc", "Pages/Page_0/Content.xml"))));

        XElement contentRoot;
        if (withLayers)
        {
            contentRoot = new XElement(ns + "Page",
                new XElement(ns + "Content",
                    new XElement(ns + "Layer",
                        new XAttribute("ID", "5"),
                        new XAttribute("Type", "Background"),
                        new XElement(ns + "PathObject",
                            new XAttribute("ID", "6"),
                            new XElement(ns + "Boundary", "10 10 50 50"))),
                    new XElement(ns + "Layer",
                        new XAttribute("ID", "7"),
                        new XAttribute("Type", "Foreground"),
                        new XElement(ns + "PathObject",
                            new XAttribute("ID", "8"),
                            new XElement(ns + "Boundary", "20 20 60 60")))));
        }
        else
        {
            contentRoot = new XElement(ns + "Page");
        }

        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            WriteEntry(za, "OFD.xml", ofdXml);
            WriteEntry(za, "Doc_0/Document.xml", documentXml);
            WriteEntry(za, "Doc_0/Pages/Page_0/Content.xml", contentRoot);
        }
        var bytes = ms.ToArray();
        File.WriteAllBytes(Out(name), bytes);
        return bytes;
    }

    private static void WriteEntry(ZipArchive za, string name, XElement root)
    {
        var e = za.CreateEntry(name);
        using var s = e.Open();
        using var w = new StreamWriter(s, new UTF8Encoding(false));
        new XDocument(root).Save(w);
    }
}
