using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.PageDescription;

namespace NetOfd.Reader;

/// <summary>
/// 文本抽取过滤器：决定某个 TextCode 的文本是否参与抽取，并可改写抽取结果。
/// <para>对应 Java: <c>org.ofdrw.reader.extractor.ExtractorFilter</c>。</para>
/// </summary>
public interface IExtractorFilter
{
    /// <summary>
    /// 返回该 TextCode 允许抽取的文本；返回 <c>null</c> 或纯空白表示丢弃该条。
    /// <para>返回值可以不等于原文（Java 语义：返回值直接作为抽取结果入列），
    /// 因此过滤器同时具备「筛选」与「改写」两种能力。</para>
    /// </summary>
    /// <param name="textObject">该 TextCode 所属的文本对象</param>
    /// <param name="code">文字定位对象</param>
    string? AllowText(CTText textObject, TextCode code);
}

/// <summary>
/// 页面内容文字提取器：按页/按全文档抽取文本或 <see cref="CTText"/> 文本对象。
/// <para>对应 Java: <c>org.ofdrw.reader.ContentExtractor</c>。</para>
/// </summary>
/// <remarks>
/// <b>两种用法</b>：
/// <list type="bullet">
/// <item>静态方法（NetOfd 既有）：<see cref="ExtractPageText(PageObject)"/>、
/// <see cref="ExtractAllPagesText(IEnumerable{PageObject?})"/>、<see cref="ExtractPageTextItems"/>；</item>
/// <item>实例方法（对齐 Java）：基于 <see cref="OFDReader"/> 按<b>页码（从 1 开始）</b>抽取，
/// 支持 <see cref="IExtractorFilter"/> 过滤/改写，见 <see cref="GetPageContent"/>、
/// <see cref="GetPageTextObject"/>、<see cref="ExtractAll"/>、<see cref="ExtractAllTextObject"/>、<see cref="Traverse"/>。</item>
/// </list>
/// <para><b>遍历顺序</b>：图层按 Z 序（<c>GetLayersInZOrder</c>）遍历，图层内递归展开
/// 嵌套 <c>PageBlock</c>，与视觉/阅读顺序一致。Java 原版用文档顺序遍历图层，此处为 NetOfd 增值。</para>
/// <para><b>注意</b>：Java 与本实现均<b>不合并模板页（TemplatePage）</b>内容——
/// 仅抽取页面自身 Content 中的文本。</para>
/// </remarks>
public class ContentExtractor
{
    private readonly OFDReader _reader;
    private readonly IExtractorFilter? _filter;

    /// <summary>构造文字抽取器</summary>
    /// <param name="reader">OFD 解析器</param>
    public ContentExtractor(OFDReader reader) : this(reader, null)
    {
    }

    /// <summary>构造文字抽取器（带过滤器）</summary>
    /// <param name="reader">OFD 解析器</param>
    /// <param name="filter">文本抽取过滤器，可为 null</param>
    public ContentExtractor(OFDReader reader, IExtractorFilter? filter)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
        _filter = filter;
    }

    /// <summary>当前使用的过滤器（可能为 null）。</summary>
    public IExtractorFilter? Filter => _filter;

    // ── 实例 API（对齐 Java）────────────────────────────────────

    /// <summary>
    /// 抽取指定页（页码从 1 开始）内的所有文字，<b>每个 TextCode 一条</b>。
    /// 对应 Java <c>ContentExtractor#getPageContent(int)</c>。
    /// </summary>
    /// <param name="pageNum">页码，从 1 开始</param>
    /// <param name="skipBlank">是否丢弃 null/纯空白片段；Java 默认不过滤，此处默认 true</param>
    public List<string> GetPageContent(int pageNum, bool skipBlank = true)
    {
        var res = new List<string>();
        var content = RequirePage(pageNum).Content;
        if (content is null) return res;

        foreach (var layer in content.GetLayersInZOrder())
            CollectText(layer, res, skipBlank);
        return res;
    }

    /// <summary>
    /// 抽取指定页（页码从 1 开始）内的所有文本对象 <c>CTText</c>。
    /// 对应 Java <c>ContentExtractor#getPageTextObject(int)</c>。
    /// <para>带过滤器时：文本对象只要有<b>任意一个</b> TextCode 通过过滤，该对象即入选（且只入选一次）。</para>
    /// </summary>
    public List<CTText> GetPageTextObject(int pageNum)
    {
        var res = new List<CTText>();
        var content = RequirePage(pageNum).Content;
        if (content is null) return res;

        foreach (var layer in content.GetLayersInZOrder())
            CollectTextObjects(layer, res);
        return res;
    }

    /// <summary>校验页码并取页对象（页码从 1 开始，越界抛 <see cref="ArgumentException"/>）。</summary>
    private PageObject RequirePage(int pageNum)
    {
        if (pageNum < 1) throw new ArgumentException("页码（pageNum）必须从 1 开始", nameof(pageNum));
        int count = _reader.GetPageCount();
        if (pageNum > count) throw new ArgumentException($"页码超出范围：文档共 {count} 页", nameof(pageNum));
        return _reader.GetPageByNum(pageNum);
    }

    /// <summary>
    /// 抽取整个文档的所有文本（按页、按 TextCode 逐条）。
    /// 对应 Java <c>ContentExtractor#extractAll()</c>。
    /// </summary>
    public List<string> ExtractAll(bool skipBlank = true)
    {
        var res = new List<string>();
        int count = _reader.GetPageCount();
        for (int pageNum = 1; pageNum <= count; pageNum++)
        {
            var pageContent = GetPageContent(pageNum, skipBlank);
            if (pageContent.Count > 0) res.AddRange(pageContent);
        }
        return res;
    }

    /// <summary>
    /// 抽取整个文档的所有文本对象 <c>CTText</c>。
    /// 对应 Java <c>ContentExtractor#extractAllTextObject()</c>。
    /// </summary>
    public List<CTText> ExtractAllTextObject()
    {
        var res = new List<CTText>();
        int count = _reader.GetPageCount();
        for (int pageNum = 1; pageNum <= count; pageNum++)
        {
            var list = GetPageTextObject(pageNum);
            if (list.Count > 0) res.AddRange(list);
        }
        return res;
    }

    /// <summary>
    /// 逐页遍历：对每一页回调（页码从 1 开始，该页抽取到的文本片段列表）。
    /// 对应 Java <c>ContentExtractor#traverse(Receiver)</c>。
    /// <para>Java 语义：仅当该页抽取结果非空时才回调。</para>
    /// </summary>
    public void Traverse(Action<int, List<string>> receiver)
    {
        if (receiver is null) throw new ArgumentNullException(nameof(receiver));

        int count = _reader.GetPageCount();
        for (int pageNum = 1; pageNum <= count; pageNum++)
        {
            var pageContent = GetPageContent(pageNum);
            if (pageContent.Count > 0) receiver(pageNum, pageContent);
        }
    }

    // ── 遍历实现 ────────────────────────────────────────────────

    /// <summary>收集图层内的文本片段（递归展开嵌套 PageBlock）。</summary>
    private void CollectText(CTLayer layer, List<string> sink, bool skipBlank)
    {
        foreach (var obj in layer.GetGraphicObjects())
        {
            if (obj is not CTText textObj) continue;

            foreach (var code in textObj.GetTextCodes())
            {
                if (_filter is null)
                {
                    var raw = code.Text;
                    if (!skipBlank || !string.IsNullOrWhiteSpace(raw)) sink.Add(raw ?? string.Empty);
                    continue;
                }

                // 过滤器返回值直接作为抽取结果入列（可改写文本）
                var allow = _filter.AllowText(textObj, code);
                if (allow is not null && allow.Trim().Length > 0) sink.Add(allow);
            }
        }
    }

    /// <summary>收集图层内的文本对象（递归展开嵌套 PageBlock）。</summary>
    private void CollectTextObjects(CTLayer layer, List<CTText> sink)
    {
        foreach (var obj in layer.GetGraphicObjects())
        {
            if (obj is not CTText textObj) continue;

            if (_filter is null)
            {
                sink.Add(textObj);
                continue;
            }

            // Java 语义：只要有一个 TextCode 通过过滤，该 TextObject 即入选
            foreach (var code in textObj.GetTextCodes())
            {
                var allow = _filter.AllowText(textObj, code);
                if (allow is not null && allow.Trim().Length > 0)
                {
                    sink.Add(textObj);
                    break;
                }
            }
        }
    }

    // ── 静态 API（NetOfd 既有，保持不变）────────────────────────

    /// <summary>提取页面全部文本</summary>
    public static string ExtractPageText(PageObject page)
    {
        var sb = new System.Text.StringBuilder();
        var content = page.Content;
        if (content is null) return string.Empty;

        // 与 ExtractPageTextItems 保持同一遍历顺序（Z 序 + 递归 PageBlock），
        // 避免同页两种 API 产出不同顺序的文本
        foreach (var layer in content.GetLayersInZOrder())
            ExtractLayerText(layer, sb);
        return sb.ToString();
    }

    private static void ExtractLayerText(CTLayer layer, System.Text.StringBuilder sb)
    {
        foreach (var obj in layer.GetGraphicObjects())
        {
            if (obj is CTText textObj)
            {
                foreach (var code in textObj.GetTextCodes())
                {
                    if (!string.IsNullOrEmpty(code.Text))
                        sb.Append(code.Text);
                }
            }
        }
    }

    /// <summary>从多个页面提取文本（按页分段）</summary>
    public static List<string> ExtractAllPagesText(IEnumerable<PageObject?> pages)
    {
        var result = new List<string>();
        foreach (var page in pages)
        {
            result.Add(page is null ? string.Empty : ExtractPageText(page));
        }
        return result;
    }

    /// <summary>
    /// 按 <c>TextCode</c> 粒度提取页面文本片段，**每个 TextCode 一条**。
    /// 对应 Java <c>ContentExtractor.getPageContent(int)</c> 的返回语义
    /// （Java 端每个 TextCode 的 content 单独入列，导出为文本时逐条换行）。
    /// </summary>
    /// <param name="page">页对象；为 null 返回空列表</param>
    /// <param name="skipBlank">是否跳过纯空白片段，默认 true</param>
    public static List<string> ExtractPageTextItems(PageObject? page, bool skipBlank = true)
    {
        var result = new List<string>();
        var content = page?.Content;
        if (content is null) return result;

        // 按 Z 序遍历图层；GetGraphicObjects 已递归展开 PageBlock
        foreach (var layer in content.GetLayersInZOrder())
        {
            foreach (var obj in layer.GetGraphicObjects())
            {
                if (obj is not CTText textObj) continue;
                foreach (var code in textObj.GetTextCodes())
                {
                    string? s = code.Text;
                    if (s is null) continue;
                    if (skipBlank && s.Trim().Length == 0) continue;
                    result.Add(s);
                }
            }
        }
        return result;
    }
}
