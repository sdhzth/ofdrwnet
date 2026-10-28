using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using NetOfd.Layout.Engine;
using NetOfd.Reader;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 生成侧页级资源落位测试（GB/T 48666-2026 §7.2 a）。
/// <para>
/// 标准原文：「被文档页面内容使用的字型、绘制参数和在多个页面中共同使用的资源<b>应</b>在
/// 文档资源列表（DocumentRes.xml）文件中注册，仅在一个页面内使用的资源<b>一般</b>在页面
/// 资源列表（Page_N/PageRes.xml）文件中注册。」
/// </para>
/// <para>
/// 本组用例验证 netofd 写端能按引用范围自动分流：单页专用 → 页级；多页共用 → 文档级。
/// Java ofdrw 生成侧（<c>ResManager#addMultiMedia</c>）一律写文档级，不做下沉；
/// ofdgo 官方引擎读取侧完整支持页级资源（<c>loadPageResources</c>），
/// netofd 读取侧亦已补齐，故下沉具备双向互操作基础。
/// </para>
/// </summary>
public class PageResPlacementTest
{
    private const string OfdNs = "http://www.ofdspec.org/2016";

    // ── 输出目录 ─────────────────────────────────────────────────
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_pageres_placement_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>4×4 纯红 PNG（真实 PNG 字节）。</summary>
    private static byte[] RedPng()
    {
        using var bmp = new SKBitmap(4, 4);
        bmp.Erase(SKColors.Red);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    /// <summary>写出一个临时 PNG 并返回其路径。</summary>
    private static string WritePng(string name)
    {
        var path = Out(name);
        File.WriteAllBytes(path, RedPng());
        return path;
    }

    private static ZipArchive OpenOfd(string ofdPath) =>
        ZipFile.OpenRead(ofdPath);

    private static bool HasEntry(ZipArchive zip, string name) =>
        zip.Entries.Any(e => e.FullName.Replace('\\', '/') == name);

    private static byte[] ReadEntry(ZipArchive zip, string name)
    {
        var e = zip.Entries.First(x => x.FullName.Replace('\\', '/') == name);
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    private static XElement? ReadXmlEntry(ZipArchive zip, string name)
    {
        if (!HasEntry(zip, name)) return null;
        return XElement.Parse(new System.Text.UTF8Encoding(false).GetString(ReadEntry(zip, name)).TrimStart('\uFEFF'));
    }

    // ──────────────────────────────────────────────────────────────
    // 1. 单页专用图像 → 下沉到 Page_N/PageRes.xml + Page_N/Res/
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 仅被单个页面引用的图像应下沉到该页的 PageRes.xml + Page_N/Res/（§7.2 a 推荐性条款）。
    /// </summary>
    [Fact]
    public void SinglePageImage_ShouldSinkToPageRes()
    {
        var png = WritePng("single.png");
        var outPath = Out("SinglePageImage.ofd");

        using (var doc = new OFDDoc(outPath))
        {
            doc.AddPage();          // Page_0
            doc.AddPage();          // Page_1 ← 图像画在此页
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx.DrawImageFromFile(png, 10, 10, 30, 20);
            doc.Close();
        }

        Assert.True(File.Exists(outPath), "应产出 OFD 文件");

        using var zip = OpenOfd(outPath);

        // 页级资源文件与声明
        Assert.True(HasEntry(zip, "Doc_0/Pages/Page_1/PageRes.xml"),
            "单页专用图像应登记到 Doc_0/Pages/Page_1/PageRes.xml");
        Assert.True(HasEntry(zip, "Doc_0/Pages/Page_1/Res/Image_1.png"),
            "单页专用图像字节应落在 Doc_0/Pages/Page_1/Res/Image_1.png");

        // 不应再留文档级副本
        Assert.False(HasEntry(zip, "Doc_0/Res/Image_1.png"),
            "下沉后 Doc_0/Res/Image_1.png 不应存在");
        Assert.False(HasEntry(zip, "Doc_0/DocumentRes.xml"),
            "全部资源均已下沉时不应产生空的 DocumentRes.xml");

        // MediaFile 相对路径（以资源文件所在目录为基准）
        var pageRes = ReadXmlEntry(zip, "Doc_0/Pages/Page_1/PageRes.xml");
        Assert.NotNull(pageRes);
        var mm = pageRes!.Element(XName.Get("MultiMedias", OfdNs))
                        ?.Elements(XName.Get("MultiMedia", OfdNs))
                        .FirstOrDefault();
        Assert.NotNull(mm);
        Assert.Equal("Res/Image_1.png",
            mm!.Element(XName.Get("MediaFile", OfdNs))?.Value);

        // 字节一致
        Assert.Equal(RedPng(), ReadEntry(zip, "Doc_0/Pages/Page_1/Res/Image_1.png"));
    }

    /// <summary>Content.xml 中的 &lt;ofd:PageRes&gt; 必须排在 &lt;ofd:Content&gt; 之前（GB/T 33190 表 13）。</summary>
    [Fact]
    public void PageResDeclaration_ShouldPrecedeContent()
    {
        var png = WritePng("order.png");
        var outPath = Out("PageResOrder.ofd");

        using (var doc = new OFDDoc(outPath))
        {
            doc.AddPage();
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx.DrawImageFromFile(png, 10, 10, 30, 20);
            doc.Close();
        }

        using var zip = OpenOfd(outPath);
        var page = ReadXmlEntry(zip, "Doc_0/Pages/Page_1/Content.xml");
        Assert.NotNull(page);

        var names = page!.Elements().Select(e => e.Name.LocalName).ToList();
        int iPageRes = names.IndexOf("PageRes");
        int iContent = names.IndexOf("Content");
        Assert.True(iPageRes >= 0, "Content.xml 应含 <ofd:PageRes> 声明");
        if (iContent >= 0)
            Assert.True(iPageRes < iContent,
                $"<ofd:PageRes> 必须排在 <ofd:Content> 之前（实测顺序: {string.Join(",", names)}）");

        // 声明值
        Assert.Equal("PageRes.xml",
            page.Elements().First(e => e.Name.LocalName == "PageRes").Value);
    }

    /// <summary>下沉后应能被读取侧回读出图像字节（读取侧 PageRes 解析能力闭环）。</summary>
    [Fact]
    public void SunkImage_ShouldBeReadableBack()
    {
        var png = WritePng("readback.png");
        var outPath = Out("SunkImageReadback.ofd");

        using (var doc = new OFDDoc(outPath))
        {
            doc.AddPage();
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx.DrawImageFromFile(png, 10, 10, 30, 20);
            doc.Close();
        }

        using var reader = new OFDReader(outPath);
        var resMgt = reader.GetResourceManager();
        var map = resMgt.GetMultiMediaMap();
        Assert.True(map.Count > 0, "读取侧应能加载页级资源（MultiMediaMap 不得为空）");

        var bytes = resMgt.GetImageData(map.Values.First());
        Assert.NotNull(bytes);
        Assert.Equal(RedPng().Length, bytes!.Length);
    }

    // ──────────────────────────────────────────────────────────────
    // 2. 多页共用图像 → 留在文档级 DocumentRes.xml（§7.2 a 强制条款）
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 被 ≥2 个页面引用的图像属「多页共用」，按 §7.2 a 强制条款（「应」）
    /// 必须登记在 DocumentRes.xml，不得下沉到页级。
    /// </summary>
    [Fact]
    public void SharedImage_ShouldStayInDocumentRes()
    {
        var png = WritePng("shared.png");
        var outPath = Out("SharedImage.ofd");

        using (var doc = new OFDDoc(outPath))
        {
            doc.AddPage();                                              // Page_0
            var ctx0 = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx0.DrawImageFromFile(png, 10, 10, 30, 20);                // Page_0 引用

            doc.AddPage();                                              // Page_1
            var ctx1 = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx1.DrawImageFromFile(png, 10, 60, 30, 20);                // Page_1 引用（同一文件）
            doc.Close();
        }

        using var zip = OpenOfd(outPath);

        Assert.True(HasEntry(zip, "Doc_0/DocumentRes.xml"),
            "多页共用图像应登记到 Doc_0/DocumentRes.xml");
        Assert.True(HasEntry(zip, "Doc_0/Res/Image_1.png"),
            "多页共用图像字节应落在 Doc_0/Res/Image_1.png");

        foreach (var n in new[] { "Doc_0/Pages/Page_0/PageRes.xml", "Doc_0/Pages/Page_1/PageRes.xml" })
            Assert.False(HasEntry(zip, n), $"多页共用图像不得下沉到 {n}");

        var docRes = ReadXmlEntry(zip, "Doc_0/DocumentRes.xml");
        Assert.NotNull(docRes);
        Assert.Single(docRes!.Element(XName.Get("MultiMedias", OfdNs))
                             !.Elements(XName.Get("MultiMedia", OfdNs)));

        // CommonData 必须登记 DocumentRes 路径
        var document = ReadXmlEntry(zip, "Doc_0/Document.xml");
        Assert.NotNull(document);
        var commonData = document!.Element(XName.Get("CommonData", OfdNs));
        Assert.NotNull(commonData!.Element(XName.Get("DocumentRes", OfdNs)));
    }

    // ──────────────────────────────────────────────────────────────
    // 3. DocumentOnly 策略回退（Java ofdrw 基线行为）
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// <see cref="MediaPlacement.DocumentOnly"/> 下即使单页专用也一律登记到文档级，
    /// 与 Java ofdrw 生成侧基线行为一致（需要与旧产物逐字节一致时使用）。
    /// </summary>
    [Fact]
    public void DocumentOnlyPlacement_ShouldKeepDocumentLevel()
    {
        var png = WritePng("doconly.png");
        var outPath = Out("DocumentOnlyPlacement.ofd");

        using (var doc = new OFDDoc(outPath))
        {
            doc.SetMediaPlacement(MediaPlacement.DocumentOnly);
            doc.AddPage();
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx.DrawImageFromFile(png, 10, 10, 30, 20);
            doc.Close();
        }

        using var zip = OpenOfd(outPath);

        Assert.True(HasEntry(zip, "Doc_0/DocumentRes.xml"));
        Assert.True(HasEntry(zip, "Doc_0/Res/Image_1.png"));
        Assert.False(HasEntry(zip, "Doc_0/Pages/Page_1/PageRes.xml"),
            "DocumentOnly 策略下不应产生 PageRes.xml");
        Assert.False(HasEntry(zip, "Doc_0/Pages/Page_1/Res/Image_1.png"),
            "DocumentOnly 策略下不应产生页级 Res 目录内容");
    }
}
