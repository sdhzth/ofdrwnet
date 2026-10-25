using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// 内容抽取器<b>跨语言验证</b>测试——用 Java ofdrw-reader 官方测试资源做锚点。
/// 锚定 Java ContentExtractorTest 的断言：
/// helloworld.ofd「你好呀，OFD Reader&amp;Writer！」=1 条；含 PageBlock 的
/// helloworld_with_pageblock.ofd 递归抽取同样 =1 条；keyword.ofd 矩形区域过滤。
/// </summary>
public class ContentExtractorJavaAnchorTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private static string ResFile(string name) => Path.Combine(Res, name);

    private static bool Has(string name) => File.Exists(ResFile(name));

    private static void SkipIfMissing(string name)
    {
        if (!Has(name)) Assert.True(true, $"Java 测试资源 {name} 不存在，跳过。");
    }

    // ── helloworld.ofd：Java 断言 getPageContent(1) == 1 条 ──

    [Fact]
    public void HelloWorld_GetPageContent_ShouldMatchJava()
    {
        if (!Has("helloworld.ofd")) { SkipIfMissing("helloworld.ofd"); return; }

        using var reader = new OFDReader(ResFile("helloworld.ofd"));
        var ex = new ContentExtractor(reader);

        var items = ex.GetPageContent(1);
        Assert.Single(items);
        Assert.Equal("你好呀，OFD Reader&Writer！", items[0]);
    }

    /// <summary>Java extractAll()：全文仅 1 条同内容。</summary>
    [Fact]
    public void HelloWorld_ExtractAll_ShouldMatchJava()
    {
        if (!Has("helloworld.ofd")) { SkipIfMissing("helloworld.ofd"); return; }

        using var reader = new OFDReader(ResFile("helloworld.ofd"));
        var ex = new ContentExtractor(reader);

        var items = ex.ExtractAll();
        Assert.Single(items);
        Assert.Equal("你好呀，OFD Reader&Writer！", items[0]);
    }

    /// <summary>Java traverse()：每页回调一次，第 1 页内容即全文。</summary>
    [Fact]
    public void HelloWorld_Traverse_ShouldMatchJava()
    {
        if (!Has("helloworld.ofd")) { SkipIfMissing("helloworld.ofd"); return; }

        using var reader = new OFDReader(ResFile("helloworld.ofd"));
        var ex = new ContentExtractor(reader);

        var seen = new List<(int Page, string Joined)>();
        ex.Traverse((pageNum, contents) => seen.Add((pageNum, string.Join("|", contents))));

        Assert.Single(seen);
        Assert.Equal((1, "你好呀，OFD Reader&Writer！"), seen[0]);
    }

    // ── helloworld_with_pageblock.ofd：PageBlock 递归抽取 ──

    [Fact]
    public void HelloWorldPageBlock_ExtractAll_ShouldMatchJava()
    {
        if (!Has("helloworld_with_pageblock.ofd")) { SkipIfMissing("helloworld_with_pageblock.ofd"); return; }

        using var reader = new OFDReader(ResFile("helloworld_with_pageblock.ofd"));
        var ex = new ContentExtractor(reader);

        // Java extractAllPageBlock()：PageBlock 包裹不影响抽取结果
        var items = ex.ExtractAll();
        Assert.Single(items);
        Assert.Equal("你好呀，OFD Reader&Writer！", items[0]);
    }

    [Fact]
    public void HelloWorldPageBlock_GetPageTextObject_ShouldMatchJava()
    {
        if (!Has("helloworld_with_pageblock.ofd")) { SkipIfMissing("helloworld_with_pageblock.ofd"); return; }

        using var reader = new OFDReader(ResFile("helloworld_with_pageblock.ofd"));
        var ex = new ContentExtractor(reader);

        var objs = ex.GetPageTextObject(1);
        Assert.Single(objs);
        Assert.Equal("你好呀，OFD Reader&Writer！", string.Concat(objs[0].GetTextCodes().Select(c => c.Text)));
    }

    // ── keyword.ofd：矩形区域过滤（Java 用 Rectangle(0,0,283,120)）──

    /// <summary>
    /// 页面下部文字（y≥120mm：签名串、旋转水印「事由户籍办理房产证明」）应被区域过滤剔除。
    /// </summary>
    [Fact]
    public void KeywordOfd_RegionFilter_ShouldKeepTopTextOnly()
    {
        if (!Has("keyword.ofd")) { SkipIfMissing("keyword.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword.ofd"));
        var ex = new ContentExtractor(reader, new RegionTextExtractorFilter(0, 0, 283, 120));

        var items = ex.GetPageContent(1);
        Assert.NotEmpty(items);
        Assert.All(items, it => Assert.False(string.IsNullOrWhiteSpace(it)));

        // 顶部区域（Boundary.Y 19.8 ~ 43.9）的文本应被保留
        Assert.Contains(items, it => it.Contains("中国建设银行股份有限公司泰安分行", StringComparison.Ordinal));
        // y≈141.7 的 45° 旋转水印与 y≈184.6 的签名串不在区域内 → 剔除
        Assert.DoesNotContain(items, it => it.Contains("事由户籍办理房产证明", StringComparison.Ordinal));
        Assert.DoesNotContain(items, it => it.Contains("301213AF0610489", StringComparison.Ordinal));
    }
}
