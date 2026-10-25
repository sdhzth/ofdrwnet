using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// 关键字抽取测试——用 Java ofdrw-reader 官方测试资源做<b>跨语言验证</b>。
/// 锚定 Java KeywordExtractorTest 的断言：
/// multiKeywordInTextCode.ofd「打发」=7；keyword.ofd「办理」=1 (page=1)、
/// ["办理","不动产权"]=3；keyword2.ofd「马上融」跨 TextCode 断词召回。
/// </summary>
public class KeywordExtractorTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private static string ResFile(string name) => Path.Combine(Res, name);

    private static bool Has(string name) => File.Exists(ResFile(name));

    private static void SkipIfMissing(string name)
    {
        if (!Has(name)) Assert.True(true, $"Java 测试资源 {name} 不存在，跳过。");
    }

    // ── 单 TextCode 内多次出现（Java: multiKeywordInTextCode.ofd 「打发」→ 7）──

    [Fact]
    public void SingleTextCode_MultipleOccurrences_ShouldFindSeven()
    {
        if (!Has("multiKeywordInTextCode.ofd")) { SkipIfMissing("multiKeywordInTextCode.ofd"); return; }

        using var reader = new OFDReader(ResFile("multiKeywordInTextCode.ofd"));
        var list = KeywordExtractor.GetKeywordPositionList(reader, "打发");

        Assert.Equal(7, list.Count);
        Assert.All(list, p => Assert.Equal("打发", p.Keyword));
        Assert.All(list, p => Assert.Equal(1, p.Page));
        Assert.All(list, p => Assert.True(p.Box.Width > 0 && p.Box.Height > 0));
    }

    // ── 复杂真实文档（含 CTM 旋转、模板页、签名）──

    [Fact]
    public void RealDocument_SingleKeyword_ShouldFindOne()
    {
        if (!Has("keyword.ofd")) { SkipIfMissing("keyword.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword.ofd"));
        var list = KeywordExtractor.GetKeywordPositionList(reader, "办理");

        // Java KeywordExtractorTest.getKeyWordPositionList: keyword.ofd「办理」→ 1, page=1
        Assert.Single(list);
        Assert.Equal(1, list[0].Page);
        Assert.Equal("办理", list[0].Keyword);
        Assert.True(list[0].Box.Width > 0 && list[0].Box.Height > 0);
    }

    [Fact]
    public void RealDocument_MultiKeywords_ShouldFindThree()
    {
        if (!Has("keyword.ofd")) { SkipIfMissing("keyword.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword.ofd"));
        var list = KeywordExtractor.GetKeywordPositionList(reader, new[] { "办理", "不动产权" });

        // Java KeywordExtractorTest.getMultiKeyWordPositionList: → 3
        Assert.Equal(3, list.Count);
        Assert.All(list, p => Assert.Equal(1, p.Page));
        Assert.Equal(2, list.Count(p => p.Keyword == "不动产权"));
        Assert.Equal(1, list.Count(p => p.Keyword == "办理"));
        Assert.All(list, p => Assert.True(p.Box.Width > 0 && p.Box.Height > 0));
    }

    // ── 跨 TextCode 断词：跨段框应与整段框几何一致 ──

    [Fact]
    public void CrossTextCode_ShouldMergeAndMatchWholeSegmentGeometry()
    {
        if (!Has("keyword2.ofd")) { SkipIfMissing("keyword2.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword2.ofd"));
        var list = KeywordExtractor.GetKeywordPositionList(reader, "马上融");

        // Page_1：「重庆市马」+"上融资" 跨两个 TextCode（postfix 断词拼接）；
        // Page_2：「重庆市马上融」整段在一个 TextCode（normal）。
        // 两处文本几何相同 → 两框应精确一致（Java mergeKeywordPosition 语义）。
        Assert.Equal(2, list.Count);
        Assert.All(list, p => Assert.Equal("马上融", p.Keyword));
        Assert.Contains(list, p => p.Page == 1);
        Assert.Contains(list, p => p.Page == 2);

        var crossPage = list.First(p => p.Page == 1).Box;
        var wholePage = list.First(p => p.Page == 2).Box;

        // 跨页 deltaX 浮点末位有 1e-4 量级差异（两页由生成器独立累计）→ 容差 1e-3 mm
        Assert.True(Math.Abs(crossPage.TopLeftX - wholePage.TopLeftX) < 1e-3, "跨段/整段框起点 X 应一致");
        Assert.True(Math.Abs(crossPage.TopLeftY - wholePage.TopLeftY) < 1e-3, "跨段/整段框起点 Y 应一致");
        Assert.True(Math.Abs(crossPage.Width - wholePage.Width) < 1e-3,
            $"跨段/整段框宽应一致：cross={crossPage.Width:F6} whole={wholePage.Width:F6}");
        Assert.True(Math.Abs(crossPage.Height - wholePage.Height) < 1e-3, "跨段/整段框高应一致");

        // 三个全角字（马 上 融）间距 4.234mm → 框宽 ≈ 4.234*3 ≈ 12.70（首字 em + 2 段距）
        Assert.True(Math.Abs(wholePage.Width - 4.234035968780518 * 3) < 1e-3,
            $"框宽应≈3 个 em（实际 {wholePage.Width:F4}）");
    }

    // ── 页码范围 / 参数校验 ──

    [Fact]
    public void PageFilter_ShouldRestrictSearch()
    {
        if (!Has("keyword2.ofd")) { SkipIfMissing("keyword2.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword2.ofd"));

        Assert.Single(KeywordExtractor.GetKeywordPositionList(reader, "马上融", new[] { 1 }));
        Assert.Single(KeywordExtractor.GetKeywordPositionList(reader, "马上融", new[] { 2 }));
        Assert.Equal(2, KeywordExtractor.GetKeywordPositionList(reader, "马上融", new[] { 1, 2 }).Count);
    }

    [Fact]
    public void OutOfRangePage_ShouldThrow()
    {
        if (!Has("keyword2.ofd")) { SkipIfMissing("keyword2.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword2.ofd"));
        Assert.Throws<ArgumentException>(() => KeywordExtractor.GetKeywordPositionList(reader, "马上融", new[] { 0 }));
        Assert.Throws<ArgumentException>(() => KeywordExtractor.GetKeywordPositionList(reader, "马上融", new[] { 99 }));
    }

    [Fact]
    public void EmptyOrBlankKeyword_ShouldReturnEmpty()
    {
        if (!Has("keyword.ofd")) { SkipIfMissing("keyword.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword.ofd"));
        Assert.Empty(KeywordExtractor.GetKeywordPositionList(reader, ""));
        Assert.Empty(KeywordExtractor.GetKeywordPositionList(reader, "  "));
        Assert.Empty(KeywordExtractor.GetKeywordPositionList(reader, new[] { "", "  " }));
        // 空串被过滤，但 "办理" 保留 → 仍能命中 1
        Assert.Single(KeywordExtractor.GetKeywordPositionList(reader, new[] { "办理", "" }));
    }

    [Fact]
    public void InstanceApi_ShouldDelegateToStatic()
    {
        if (!Has("keyword2.ofd")) { SkipIfMissing("keyword2.ofd"); return; }

        using var reader = new OFDReader(ResFile("keyword2.ofd"));
        var finder = new KeywordExtractor(reader);
        Assert.Equal(2, finder.FindKeyword("马上融").Count);
        Assert.Single(finder.FindKeyword("马上融", new[] { 1 }));
        Assert.Equal(2, finder.FindKeywords(new[] { "马上融", "担保" }).Count(p => p.Keyword == "马上融"));
    }

    // ── 模板页（TemplatePage）文字检索 ──
    // Java preparedContextData 会把页面引用的模板层文字并入检索（对齐 reader 关键字
    // 引擎），而 ContentExtractor 不合并模板——两个行为必须同时成立。

    [Fact]
    public void TemplateText_ShouldBeFoundByKeywordExtractor_ButNotByContentExtractor()
    {
        var outPath = Path.Combine(Path.GetTempPath(), "netofd_kw_tpl_" + Guid.NewGuid().ToString("N")[..8] + ".ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(outPath))
        {
            // 模板页：含文字「泰安市不动产登记中心」（VirtualPage 要求绝对定位）
            var tpl = new NetOfd.Layout.VirtualPage(NetOfd.Layout.PageLayout.A4());
            tpl.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("泰安市不动产登记中心模板文字")
                .SetFontSize(5d)
                .SetPosition(NetOfd.Layout.Element.Position.Absolute)
                .SetXY(20, 20).SetWidth(120).SetHeight(8));

            // 主页面：引用模板 + 正文
            var vPage = new NetOfd.Layout.VirtualPage(NetOfd.Layout.PageLayout.A4());
            vPage.AddTemplate(tpl);
            vPage.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("正文内容不含该关键字")
                .SetFontSize(5d)
                .SetPosition(NetOfd.Layout.Element.Position.Absolute)
                .SetXY(20, 60).SetWidth(120).SetHeight(8));
            doc.AddVPage(vPage);
        }

        try
        {
            using var reader = new OFDReader(outPath);

            // 1) KeywordExtractor 合并模板层文字 → 命中 1 处，归属正文页 page=1
            var list = KeywordExtractor.GetKeywordPositionList(reader, "不动产登记中心");
            Assert.Single(list);
            Assert.Equal(1, list[0].Page);
            Assert.Equal("不动产登记中心", list[0].Keyword);
            Assert.True(list[0].Box.Width > 0 && list[0].Box.Height > 0);
            Assert.True(list[0].Box.TopLeftX >= 20 && list[0].Box.TopLeftY >= 20,
                "模板文字框应位于模板绝对定位区域(20,20)内");

            // 2) ContentExtractor 不合并模板（Java 语义）→ 正文抽取不到模板文字
            var ex = new NetOfd.Reader.ContentExtractor(reader);
            var pageText = ex.GetPageContent(1);
            Assert.DoesNotContain(pageText, t => t.Contains("不动产登记中心", StringComparison.Ordinal));
        }
        finally
        {
            if (File.Exists(outPath)) File.Delete(outPath);
        }
    }
}
