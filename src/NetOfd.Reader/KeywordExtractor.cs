using System.Text;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Reader.Model;

namespace NetOfd.Reader;

/// <summary>
/// 关键字抽取：在 OFD 文档中定位关键字出现的<b>精确外接矩形</b>（页码 + 毫米坐标）。
/// <para>对应 Java: <c>org.ofdrw.reader.keyword.KeywordExtractor</c>。</para>
/// <para>与 Java 一致的能力：
/// <list type="bullet">
/// <item><b>跨 TextCode 断词</b>：排版把一句话拆成多个 TextCode（每字/每词一段）时，
/// 通过前缀 / 后缀匹配把相邻同页 TextCode 拼接后检索（Java searchNextText 语义）；</item>
/// <item><b>精确框</b>：按 DeltaX/DeltaY（支持 <c>g n v</c> 压缩，经 <see cref="DeltaTool"/> 展开）
/// 逐字推进计算关键字字符的实际坐标，合并出外接矩形；支持 CTM 仿射变换；</item>
/// <item><b>模板页</b>：页面引用的模板层文本按「正文层 + 模板层」顺序参与检索；</item>
/// <item><b>多关键词 / 指定页</b>重载，页码 1-based，越界抛 <see cref="ArgumentException"/>。</item>
/// </list>
/// 高度模型差异：Java 用 AWT 字体度量 <c>getStringBounds</c> 取行高；NetOfd 无 AWT，
/// 以文字对象字号（em，mm）为框高——位置/宽度/页码语义与 Java 完全一致，框高为净 em 高。</para>
/// </summary>
public class KeywordExtractor
{
    private readonly OFDReader _reader;

    public KeywordExtractor(OFDReader reader)
    {
        _reader = reader ?? throw new ArgumentNullException(nameof(reader));
    }

    // ── 静态 API（对齐 Java）────────────────────────────────────

    /// <summary>定位单个关键字（全部页）。</summary>
    public static List<KeywordPosition> GetKeywordPositionList(OFDReader reader, string keyword)
        => GetKeywordPositionList(reader, new[] { keyword }, null);

    /// <summary>定位多个关键字（全部页）。</summary>
    public static List<KeywordPosition> GetKeywordPositionList(OFDReader reader, string[] keywords)
        => GetKeywordPositionList(reader, keywords, null);

    /// <summary>定位单个关键字（指定页，页码从 1 开始）。</summary>
    public static List<KeywordPosition> GetKeywordPositionList(OFDReader reader, string keyword, int[] pages)
        => GetKeywordPositionList(reader, new[] { keyword }, pages);

    /// <summary>定位多个关键字（指定页，页码从 1 开始，越界抛异常）。</summary>
    public static List<KeywordPosition> GetKeywordPositionList(OFDReader reader, string[] keywords, int[]? pages)
    {
        if (reader is null) throw new ArgumentNullException(nameof(reader));

        var result = new List<KeywordPosition>();
        string[] ks = (keywords ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).ToArray();
        if (ks.Length == 0) return result;

        int pageCount = reader.GetPageCount();
        HashSet<int>? pageFilter = null;
        if (pages is { Length: > 0 })
        {
            foreach (int p in pages)
                if (p < 1 || p > pageCount)
                    throw new ArgumentException($"页码不正确，支持范围[1-{pageCount}]", nameof(pages));
            pageFilter = new HashSet<int>(pages);
        }

        // 1) 收集全部 TextCode 上下文（页序 → 层序[正文 + 模板] → TextCode 序，对齐 Java）
        var ctxs = new List<CodeCtx>();
        for (int page = 1; page <= pageCount; page++)
        {
            if (pageFilter is not null && !pageFilter.Contains(page)) continue;

            var po = reader.GetPageByNum(page);
            var layers = new List<CTLayer>();
            var content = po.Content;
            if (content is not null) layers.AddRange(content.GetLayers());

            // 模板页层（Java 顺序：正文层之后追加各模板引用层）
            foreach (var tpl in po.GetTemplates())
            {
                if (tpl.TemplateID is null) continue;
                var entity = reader.GetTemplate(tpl.TemplateID.Value.ToString());
                var tplContent = entity?.Page.Content;
                if (tplContent is not null) layers.AddRange(tplContent.GetLayers());
            }

            foreach (var layer in layers)
                Collect(ctxs, layer, page);
        }

        // 2) 主循环：完整包含 / 前缀断词 / 后缀断词三类匹配（对齐 Java 逐 TextCode 检索）
        for (int i = 0; i < ctxs.Count; i++)
        {
            var ctx = ctxs[i];
            string content = ctx.Code.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(content)) continue;

            foreach (string keyword in ks)
            {
                int textIndex = content.IndexOf(keyword, StringComparison.Ordinal);
                if (textIndex >= 0)
                {
                    // 完整包含关键字（含同段多次出现）
                    AddNormalKeyword(keyword, ctx, textIndex, result);
                }
                else if (keyword.StartsWith(content, StringComparison.Ordinal) && i != ctxs.Count - 1)
                {
                    // 内容整段是关键字前缀 → 尝试向后拼接
                    AddPrefixBreakTextCodeList(keyword, ctxs, i, result);
                }
                else
                {
                    int startIndex = CheckPostfixMatch(content, keyword);
                    // 内容尾部与关键字头重合 → 尝试向后拼接
                    if (startIndex >= 0)
                        AddPostfixBreakTextCodeList(keyword, ctxs, i, startIndex, result);
                }
            }
        }

        return result;
    }

    // ── 实例 API（NetOfd 便捷入口，内部走静态主流程）────────────

    /// <summary>在文档中查找关键字，返回所有出现位置（精确矩形）。</summary>
    public List<KeywordPosition> FindKeyword(string keyword)
        => GetKeywordPositionList(_reader, keyword);

    /// <summary>查找多个关键字。</summary>
    public List<KeywordPosition> FindKeywords(string[] keywords)
        => GetKeywordPositionList(_reader, keywords);

    /// <summary>在指定页（从 1 开始）查找关键字。</summary>
    public List<KeywordPosition> FindKeyword(string keyword, int[] pages)
        => GetKeywordPositionList(_reader, keyword, pages);

    // ── 匹配与合并（对齐 Java 内部方法）─────────────────────────

    /// <summary>内容整段包含关键字：计算每处出现的外接矩形。</summary>
    private static void AddNormalKeyword(string keyword, CodeCtx ctx, int textIndex, List<KeywordPosition> result)
    {
        var text = ctx.Text;
        if (text.Boundary is null) return; // Java: boundary 为空不定位

        string content = ctx.Code.Text ?? string.Empty;
        int idx = textIndex;
        while (idx >= 0)
        {
            double height = text.Size; // NetOfd：em 高(mm)；Java 用 AWT strHeight(keyword, font)
            var dx = DeltaTool.GetDelta(ctx.Code.DeltaX, content.Length);
            var dy = DeltaTool.GetDelta(ctx.Code.DeltaY, content.Length);

            KeywordPosition position;
            var ctm = text.CTM;
            if (ctm is { Count: >= 6 })
                position = GetCtmKeywordPosition(ctx, idx, height, ctm, dx, dy, keyword.Length);
            else
                position = GetKeywordPosition(ctx, idx, height, dx, dy, keyword.Length);

            position.Keyword = keyword;
            result.Add(position);

            idx = content.IndexOf(keyword, idx + keyword.Length, StringComparison.Ordinal);
        }
    }

    /// <summary>前缀断词：内容是关键字前缀，尝试与后续同页 TextCode 拼接。</summary>
    private static void AddPrefixBreakTextCodeList(
        string keyword, List<CodeCtx> all, int textCodeIndex, List<KeywordPosition> result)
    {
        var ctx = all[textCodeIndex];
        var merge = new List<CodeCtx> { ctx };
        SearchNextText(keyword, all, merge, textCodeIndex, ctx.Code.Text ?? string.Empty);

        var builder = new StringBuilder();
        foreach (var c in merge) builder.Append(c.Code.Text);
        if (builder.ToString().IndexOf(keyword, StringComparison.Ordinal) >= 0)
            MergeKeywordPosition(keyword, 0, merge, result);
    }

    /// <summary>后缀断词：内容尾部与关键字头重合（startIndex 起），尝试与后续拼接。</summary>
    private static void AddPostfixBreakTextCodeList(
        string keyword, List<CodeCtx> all, int textCodeIndex, int startIndex, List<KeywordPosition> result)
    {
        var ctx = all[textCodeIndex];
        string tail = (ctx.Code.Text ?? string.Empty)[startIndex..];
        var merge = new List<CodeCtx> { ctx };
        SearchNextText(keyword, all, merge, textCodeIndex, tail);

        var builder = new StringBuilder();
        foreach (var c in merge) builder.Append(c.Code.Text);
        if (builder.ToString().IndexOf(keyword, StringComparison.Ordinal) >= 0)
            MergeKeywordPosition(keyword, startIndex, merge, result);
    }

    /// <summary>从下一个 TextCode 起同页自然顺序拼接，直到覆盖关键字或不可能命中。</summary>
    private static void SearchNextText(
        string keyword, List<CodeCtx> all, List<CodeCtx> merge, int fromIndex, string firstMatch)
    {
        var mergeText = new StringBuilder(firstMatch);
        int currentPage = merge[0].Page;

        for (int j = fromIndex + 1; j < all.Count; j++)
        {
            var next = all[j];
            string nextText = next.Code.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(nextText)) continue; // Java: 空白段跳过
            if (next.Page != currentPage) break;               // 跨页不拼接

            mergeText.Append(nextText);
            string merged = mergeText.ToString();
            if (merged.Equals(keyword, StringComparison.Ordinal)
                || merged.StartsWith(keyword, StringComparison.Ordinal))
            {
                merge.Add(next);
                break;
            }
            if (keyword.StartsWith(merged, StringComparison.Ordinal))
            {
                merge.Add(next);
            }
            else
            {
                break;
            }
        }
    }

    /// <summary>检查内容尾部是否与关键字头重合；返回重合起始索引，否则 -1。</summary>
    private static int CheckPostfixMatch(string content, string keyword)
    {
        int startIndex = content.LastIndexOf(keyword[0]);
        if (startIndex < 0) return -1;

        bool match = true;
        for (int j = startIndex, k = 0; j < content.Length && k < keyword.Length; j++, k++)
        {
            if (content[j] != keyword[k])
            {
                match = false;
                break;
            }
        }
        return match ? startIndex : -1;
    }

    // ── 外接矩形计算（对齐 Java getKeywordPosition / mergeKeywordPosition）──

    /// <summary>无 CTM：逐字推进求关键字覆盖字符的 min/max，扩展行高为框。</summary>
    private static KeywordPosition GetKeywordPosition(
        CodeCtx ctx, int textIndex, double lineHeight, List<double> dx, List<double> dy, int keywordLength)
    {
        var box = ctx.Text.Boundary!;
        double x = ctx.Code.X + box.TopLeftX;
        double y = ctx.Code.Y + box.TopLeftY;

        double maxX = double.MinValue, maxY = double.MinValue;
        double minX = double.MaxValue, minY = double.MaxValue;
        for (int i = 0; i < textIndex + keywordLength; i++)
        {
            if (i >= textIndex)
            {
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
            }
            if (i < dx.Count) x += dx[i];
            if (i < dy.Count) y += dy[i];
        }

        double w = maxX - minX + ctx.Text.Size;
        double h = maxY - minY + lineHeight;
        return new KeywordPosition
        {
            Page = ctx.Page,
            Box = new STBox(minX, minY - lineHeight, w, h),
        };
    }

    /// <summary>有 CTM：推进到关键字起点，四角仿射变换后取外接框并叠 Boundary 左上角。</summary>
    private static KeywordPosition GetCtmKeywordPosition(
        CodeCtx ctx, int textIndex, double height, STArray ctm, List<double> dx, List<double> dy, int keywordLength)
    {
        double[] m = ctm.Items.ToArray();
        double x = ctx.Code.X;
        double y = ctx.Code.Y;
        for (int i = 0; i < textIndex; i++)
        {
            if (i < dx.Count) x += dx[i];
            if (i < dy.Count) y += dy[i];
        }

        double stringWidth = GetStringWidth(textIndex, keywordLength, dx, ctx.Text.Size);

        var lt = Transform(m, x, y - height);
        var lb = Transform(m, x, y);
        var rt = Transform(m, x + stringWidth, y - height);
        var rb = Transform(m, x + stringWidth, y);
        var ctmBox = MergeBox(lt, lb, rt, rb);

        var boundary = ctx.Text.Boundary!;
        ctmBox.TopLeftX += boundary.TopLeftX;
        ctmBox.TopLeftY += boundary.TopLeftY;

        return new KeywordPosition { Page = ctx.Page, Box = ctmBox };
    }

    /// <summary>合并跨 TextCode 命中的各段外接框为一个总框（对齐 Java mergeKeywordPosition）。</summary>
    private static void MergeKeywordPosition(
        string keyword, int firstStartIndex, List<CodeCtx> merge, List<KeywordPosition> result)
    {
        var boxList = new List<STBox>();
        int page = 0, totalLength = 0, keywordLength = keyword.Length;

        for (int i = 0; i < merge.Count; i++)
        {
            var ctx = merge[i];
            string content = ctx.Code.Text ?? string.Empty;
            var text = ctx.Text;
            int textLength = content.Length;
            if (page == 0) page = ctx.Page;
            if (text.Boundary is null) continue;

            if (i == 0 && firstStartIndex > 0)
            {
                textLength = totalLength = content.Length - firstStartIndex;
            }
            else if ((totalLength + textLength) > keywordLength)
            {
                textLength = keywordLength - totalLength;
            }
            else
            {
                totalLength += content.Length;
            }

            var dx = DeltaTool.GetDelta(ctx.Code.DeltaX, content.Length);
            var dy = DeltaTool.GetDelta(ctx.Code.DeltaY, content.Length);

            double width;
            if (i == 0 && firstStartIndex > 0)
                width = GetStringWidth(firstStartIndex, textLength, dx, text.Size);
            else
                width = GetStringWidth(0, textLength, dx, text.Size);
            if (width == 0) width = text.Size;

            double height = text.Size; // NetOfd：em 高；Java 用 AWT strHeight(keyword, font)

            var ctm = text.CTM;
            if (ctm is { Count: >= 6 })
            {
                double[] m = ctm.Items.ToArray();
                double x = ctx.Code.X, y = ctx.Code.Y;
                if (i == 0 && firstStartIndex > 0)
                {
                    for (int j = 0; j < firstStartIndex && j < dx.Count; j++) x += dx[j];
                    for (int j = 0; j < firstStartIndex && j < dy.Count; j++) y += dy[j];
                }

                var lb = Transform(m, x, y);
                var rt = Transform(m, x + width, y - height);
                var b = MergeBox(lb, rt);
                b.TopLeftX += text.Boundary!.TopLeftX;
                b.TopLeftY += text.Boundary!.TopLeftY;
                boxList.Add(b);
            }
            else
            {
                double bx = ctx.Code.X + text.Boundary!.TopLeftX;
                double by = ctx.Code.Y + text.Boundary!.TopLeftY;
                if (i == 0 && firstStartIndex > 0)
                {
                    for (int j = 0; j < firstStartIndex && j < dx.Count; j++) bx += dx[j];
                    for (int j = 0; j < firstStartIndex && j < dy.Count; j++) by += dy[j];
                }
                boxList.Add(new STBox(bx, by - height, width, height));
            }
        }

        if (boxList.Count > 0)
        {
            result.Add(new KeywordPosition { Page = page, Box = MergeBox(boxList), Keyword = keyword });
        }
    }

    // ── 几何/收集辅助 ───────────────────────────────────────────

    /// <summary>文本宽度 = 首字 em + 中间 DeltaX 推进和（对齐 Java getStringWidth）。</summary>
    private static double GetStringWidth(int textIndex, int keywordLength, List<double> deltaX, double fontSize)
    {
        double width = fontSize;
        int end = textIndex + keywordLength - 1;
        for (int i = textIndex; i < end && i < deltaX.Count; i++)
            width += deltaX[i];
        return width;
    }

    /// <summary>仿射变换：x' = a*x + c*y + e；y' = b*x + d*y + f。</summary>
    private static (double X, double Y) Transform(double[] m, double sx, double sy)
        => (m[0] * sx + m[2] * sy + m[4], m[1] * sx + m[3] * sy + m[5]);

    /// <summary>多角点外接矩形。</summary>
    private static STBox MergeBox(params (double X, double Y)[] pts)
    {
        double minX = pts[0].X, minY = pts[0].Y, maxX = pts[0].X, maxY = pts[0].Y;
        for (int i = 1; i < pts.Length; i++)
        {
            minX = Math.Min(minX, pts[i].X);
            minY = Math.Min(minY, pts[i].Y);
            maxX = Math.Max(maxX, pts[i].X);
            maxY = Math.Max(maxY, pts[i].Y);
        }
        return new STBox(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>多框外接矩形（对齐 Java mergeBox）。</summary>
    private static STBox MergeBox(List<STBox> boxList)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        foreach (var b in boxList)
        {
            minX = Math.Min(minX, b.TopLeftX);
            minY = Math.Min(minY, b.TopLeftY);
            maxX = Math.Max(maxX, b.TopLeftX + b.Width);
            maxY = Math.Max(maxY, b.TopLeftY + b.Height);
        }
        return new STBox(minX, minY, maxX - minX, maxY - minY);
    }

    /// <summary>收集图层中的 TextCode 上下文（GetGraphicObjects 已递归展开嵌套 PageBlock，保文档序）。</summary>
    private static void Collect(List<CodeCtx> sink, CTLayer layer, int page)
    {
        foreach (var obj in layer.GetGraphicObjects())
        {
            if (obj is CTText textObj)
            {
                foreach (var code in textObj.GetTextCodes())
                    sink.Add(new CodeCtx(code, page, textObj));
            }
        }
    }

    /// <summary>TextCode 检索上下文：文字定位对象 + 所在页 + 所属文字对象。</summary>
    private sealed record CodeCtx(TextCode Code, int Page, CTText Text);
}
