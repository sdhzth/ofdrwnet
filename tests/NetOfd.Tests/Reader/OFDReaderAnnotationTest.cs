using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// OFDReader 注解读取 API 测试——对齐 Java <c>OFDReader.getAnnotations()</c> /
/// <c>getAnnotationEntities()</c>，用 Java 官方测试资源做锚点。
/// <para>样本：发票示例.ofd（reader 官方资源，Annotations.xml 位于 Doc_0/Annots/，
/// FileLoc 为相对 Annotations.xml 所在目录的路径）；AddWatermarkAnnot.ofd
/// （layout 官方资源，FileLoc 为根路径绝对定位 /Doc_0/Pages/Page_0/Annotation.xml）。</para>
/// </summary>
public class OFDReaderAnnotationTest
{
    private const string ReaderRes = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";
    private const string LayoutRes = "E:/电子病历/ofdrw-master/ofdrw-layout/src/test/resources";

    private static bool Has(string file) => File.Exists(file);

    private static void SkipIfMissing(string file)
    {
        if (!Has(file)) Assert.True(true, $"Java 测试资源 {file} 不存在，跳过。");
    }

    // ── 发票示例.ofd：Annotations.xml 在 Doc_0/Annots/，FileLoc 相对 Annotations.xml 目录 ──

    [Fact]
    public void InvoiceSample_GetAnnotations_ShouldReturnEntry()
    {
        var file = Path.Combine(ReaderRes, "发票示例.ofd");
        if (!Has(file)) { SkipIfMissing(file); return; }

        using var reader = new OFDReader(file);
        var annotations = reader.GetAnnotations();

        Assert.NotNull(annotations);
        // 入口含 1 个 <Page PageID="61">
        var pages = annotations!.GetPages();
        Assert.Single(pages);
        Assert.NotNull(pages[0].PageID);
        Assert.Equal(61L, pages[0].PageID!.Value);
        Assert.NotNull(pages[0].FileLoc);
        Assert.Equal("Page_0/Annotation.xml", pages[0].FileLoc!.Value);
    }

    [Fact]
    public void InvoiceSample_GetAnnotationEntities_ShouldReadPageAnnot()
    {
        var file = Path.Combine(ReaderRes, "发票示例.ofd");
        if (!Has(file)) { SkipIfMissing(file); return; }

        using var reader = new OFDReader(file);
        var entities = reader.GetAnnotationEntities();

        // 1 页注解，页面 ID "61"，两个 Watermark 注释对象
        Assert.Single(entities);
        Assert.Equal("61", entities[0].PageId);
        Assert.Equal(2, entities[0].Annots.Count);
        Assert.All(entities[0].Annots, a => Assert.Equal(AnnotType.Watermark, a.Type));
        Assert.All(entities[0].Annots, a => Assert.NotNull(a.ID));
    }

    // ── AddWatermarkAnnot.ofd：FileLoc 为根路径绝对定位 ──

    [Fact]
    public void AddWatermarkSample_GetAnnotationEntities_ShouldResolveRootPathFileLoc()
    {
        var file = Path.Combine(LayoutRes, "AddWatermarkAnnot.ofd");
        if (!Has(file)) { SkipIfMissing(file); return; }

        using var reader = new OFDReader(file);
        var annotations = reader.GetAnnotations();

        Assert.NotNull(annotations);
        // 3 页水印注解：PageID 1/59/116，FileLoc 均指向 /Doc_0/Pages/Page_X/Annotation.xml
        var pages = annotations!.GetPages();
        Assert.Equal(3, pages.Count);
        Assert.Equal(new[] { "1", "59", "116" },
            pages.Select(p => p.PageID!.Value.ToString()).ToArray());
        Assert.All(pages, p => Assert.StartsWith("/Doc_0/Pages/", p.FileLoc!.Value));

        var entities = reader.GetAnnotationEntities();
        Assert.Equal(3, entities.Count);
        Assert.Equal(new[] { "1", "59", "116" },
            entities.Select(e => e.PageId).ToArray());
        // 每页恰 1 个 Watermark 注释（文本外观校验：Creator 为 "OFD R&W"）
        Assert.All(entities, e =>
        {
            Assert.Single(e.Annots);
            Assert.Equal(AnnotType.Watermark, e.Annots[0].Type);
            Assert.Equal("OFD R&W", e.Annots[0].Creator);
            Assert.Equal("2020-05-15", e.Annots[0].LastModDate?.ToString("yyyy-MM-dd"));
        });
    }

    // ── 无注解文档：GetAnnotations 返回 null、GetAnnotationEntities 返回空集合 ──

    [Fact]
    public void NoAnnotations_ShouldReturnNullAndEmpty()
    {
        var file = Path.Combine(ReaderRes, "helloworld.ofd");
        if (!Has(file)) { SkipIfMissing(file); return; }

        using var reader = new OFDReader(file);
        Assert.Null(reader.GetAnnotations());
        Assert.Empty(reader.GetAnnotationEntities());
    }
}
