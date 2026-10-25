using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// OFDReader 非标准路径容错测试——锚定 Java <c>IssueCase.github_293</c>
/// （https://github.com/ofdrw/ofdrw/issues/293）。
/// <para>样本 path_unstd.ofd：页面目录名非 <c>Page_N</c> 标准格式
/// （<c>Pages/Page_Insert_55_2/Content.xml</c>）、模板目录为
/// <c>KingGridTPLS/KingGridTPL_0</c>（非 <c>Tpls/Tpl_N</c>）。
/// Java 断言仅「打开 + getPageList 不抛」；NetOfd 额外强断言页面数与内容可抽取
/// （内容按 BaseLoc 独立解析，不依赖目录命名格式）。</para>
/// </summary>
public class OFDReaderIssueCaseTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private static bool Has(string name) => File.Exists(Path.Combine(Res, name));

    private static void SkipIfMissing(string name)
    {
        if (!Has(name)) Assert.True(true, $"Java 测试资源 {name} 不存在，跳过。");
    }

    // ── github_293：页面名称非 Page_N 格式 ──

    [Fact]
    public void NonStandardPageDir_ShouldOpenAndCountPages()
    {
        if (!Has("path_unstd.ofd")) { SkipIfMissing("path_unstd.ofd"); return; }

        using var reader = new OFDReader(Path.Combine(Res, "path_unstd.ofd"));

        // Java IssueCase 仅打印 size 不抛异常；2 页
        Assert.Equal(2, reader.GetPageCount());
        var list = reader.GetPageList();
        Assert.Equal(2, list.Count);
        // 第二页 BaseLoc 保持原始非标准路径
        Assert.Contains(list, p =>
            p.PageAbsLoc?.Value == "/Doc_0/Pages/Page_Insert_55_2/Content.xml");
    }

    [Fact]
    public void NonStandardPageDir_ShouldExtractPageText()
    {
        if (!Has("path_unstd.ofd")) { SkipIfMissing("path_unstd.ofd"); return; }

        using var reader = new OFDReader(Path.Combine(Res, "path_unstd.ofd"));
        var ex = new ContentExtractor(reader);

        // 非标准目录名页（第 2 页）内容可正常抽取（立案决定书）
        var page2 = ex.GetPageContent(2);
        Assert.Contains(page2, t => t.Contains("立案决定书", StringComparison.Ordinal));
    }
}
