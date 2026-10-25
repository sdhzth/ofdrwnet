using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Edit;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 增量 M：StreamCollect 等价物（编辑模式 OFDDoc 从末页续写流式段 + 自动分页）。
/// 对齐 Java OFDDoc.addStreamCollect 的"在已有文档插入流式段落"语义。
/// </summary>
public class StreamCollectTest
{
    private static string Temp() => Path.Combine(Path.GetTempPath(), $"netofd_streamcollect_{Guid.NewGuid():N}.ofd");

    private static int PageCount(string path)
    {
        using var z = ZipFile.OpenRead(path);
        return z.Entries.Count(e => Regex.IsMatch(e.FullName, @"Doc_0/Pages/Page_\d+/Content\.xml$"));
    }

    private static XDocument LoadContent(string path, int pageIndex0)
    {
        using var z = ZipFile.OpenRead(path);
        var e = z.GetEntry($"Doc_0/Pages/Page_{pageIndex0}/Content.xml")
               ?? throw new InvalidOperationException($"缺少 Page_{pageIndex0}/Content.xml");
        return XDocument.Load(e.Open());
    }

    private static List<double> TextObjectY(XDocument x)
        => x.Descendants()
            .Where(n => n.Name.LocalName == "TextObject" && n.Attribute("Boundary") != null)
            .Select(t => double.Parse(t.Attribute("Boundary")!.Value.Split(' ')[1], CultureInfo.InvariantCulture))
            .ToList();

    [Fact]
    public void AddStream_AppendsAfterExistingContent_OnSamePage()
    {
        var basePath = Temp();
        using (var doc = new OFDDoc(basePath))
        {
            doc.Add(new Paragraph("首页既有段落内容"));
            doc.Close();
        }
        int basePages = PageCount(basePath);
        var baseLast = LoadContent(basePath, basePages - 1);
        double baseMaxY = TextObjectY(baseLast).DefaultIfEmpty(0).Max();

        var outPath = Temp();
        OfdEditor.AddStream(basePath, outPath,
            new Paragraph("追加段落A"),
            new Div().SetWidth(50).SetHeight(10)
                .SetBackgroundColor(new CT_Color { Value = "200 230 255" }));

        // 短内容不新增页
        Assert.Equal(basePages, PageCount(outPath));
        var outLast = LoadContent(outPath, PageCount(outPath) - 1);
        var outYs = TextObjectY(outLast);
        // 既有段落未被破坏（其 Y 仍在产物中）
        Assert.Contains(baseMaxY, outYs);
        // 追加段落落在既有内容之后
        Assert.True(outYs.Max() > baseMaxY, $"追加内容 Y({outYs.Max()}) 应 > 既有底({baseMaxY})");
    }

    [Fact]
    public void AddStream_LongContent_AutoPaginates()
    {
        var basePath = Temp();
        using (var doc = new OFDDoc(basePath))
        {
            doc.Add(new Paragraph("首页"));
            doc.Close();
        }
        int basePages = PageCount(basePath);

        var outPath = Temp();
        var many = new List<Div>();
        for (int i = 0; i < 40; i++)
            many.Add(new Paragraph($"自动分页验证段落 {i} 用于触发流式布局分页机制"));
        OfdEditor.AddStream(basePath, outPath, many.ToArray());

        // 超长内容应自动新增页
        Assert.True(PageCount(outPath) > basePages,
            $"预期自动分页新增页，实际 base={basePages} out={PageCount(outPath)}");
    }

    [Fact]
    public void AddStream_ByteOverload_ReturnsValidOfd()
    {
        var basePath = Temp();
        using (var doc = new OFDDoc(basePath))
        {
            doc.Add(new Paragraph("首页"));
            doc.Close();
        }
        var bytes = File.ReadAllBytes(basePath);
        var ok = OfdEditor.AddStream(bytes, new Div[] { new Paragraph("字节追加段落") }, out var outBytes);
        Assert.True(ok);
        Assert.True(outBytes.Length > 0);

        using var ms = new MemoryStream(outBytes);
        using var z = new ZipArchive(ms);
        Assert.NotNull(z.GetEntry("OFD.xml"));
        Assert.NotNull(z.GetEntry("Doc_0/Document.xml"));
    }

    [Fact]
    public void AddStream_PreservesExistingPageCount_WhenShort()
    {
        var basePath = Temp();
        using (var doc = new OFDDoc(basePath))
        {
            doc.Add(new Paragraph("首页"));
            doc.Close();
        }
        int basePages = PageCount(basePath);
        var outPath = Temp();
        // 单个短段落追加，不应改变页数
        OfdEditor.AddStream(basePath, outPath, new Paragraph("短追加"));
        Assert.Equal(basePages, PageCount(outPath));
    }
}
