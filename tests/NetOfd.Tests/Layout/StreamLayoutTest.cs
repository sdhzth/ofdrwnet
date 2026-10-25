using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 流式布局引擎 v1 测试：验证 <see cref="OFDDoc.Add"/> 的块级元素沿页 y 轴流式堆叠，
/// 修复「多段落叠画页顶、无 y 推进、无自动分页」的核心缺陷。
/// </summary>
public class StreamLayoutTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_stream_layout_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>按页目录序号读取文档全部页面 Content.xml（Doc_0/Pages/Page_N/Content.xml）</summary>
    private static List<XDocument> AllPageContents(string ofd)
    {
        using var za = ZipFile.OpenRead(ofd);
        var entries = za.Entries
            .Where(e => e.FullName.EndsWith("/Content.xml", StringComparison.OrdinalIgnoreCase))
            .Select(e => new
            {
                Name = e.FullName,
                Order = int.TryParse(
                    e.FullName.Replace("\\", "/").Split('/').Reverse()
                        .Skip(1).FirstOrDefault()?.Replace("Page_", ""),
                    out var n) ? n : int.MaxValue
            })
            .OrderBy(x => x.Order)
            .ToList();

        var docs = new List<XDocument>();
        foreach (var entryInfo in entries)
        {
            var e = za.GetEntry(entryInfo.Name)!;
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            docs.Add(XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray())));
        }
        return docs;
    }

    private static IEnumerable<XElement> TextObjects(XDocument content)
        => content.Descendants().Where(el => el.Name.LocalName == "TextObject");

    private static (double X, double Y, double W, double H) Boundary(XElement textObj)
    {
        var p = ((string?)textObj.Attribute("Boundary"))!
            .Split(' ').Select(double.Parse).ToArray();
        return (p[0], p[1], p[2], p[3]);
    }

    private static string PageText(XDocument content)
    {
        var sb = new StringBuilder();
        foreach (var tc in content.Descendants().Where(el => el.Name.LocalName == "TextCode"))
            sb.Append(tc.Value);
        return sb.ToString();
    }

    /// <summary>单个 TextObject 内的全部文本（TextCode 元素内容，跨行拼接）。</summary>
    private static string TextOf(XElement textObj)
    {
        var sb = new StringBuilder();
        foreach (var tc in textObj.Descendants().Where(el => el.Name.LocalName == "TextCode"))
            sb.Append(tc.Value);
        return sb.ToString();
    }

    /// <summary>页面中背景/边框矩形图元（FillRect/StrokeRect 渲染为 &lt;PathObject&gt;）的包围盒。
    /// 自 2026-10-23 起 netofd 路径图元带 Boundary（GB/T 33190 合规），AbbreviatedData 坐标为
    /// 相对 Boundary 左上角的局部坐标；真实页面坐标 = Boundary.XY + 局部包围盒偏移。
    /// 此处据此还原绝对页面坐标，使浮动布局位置断言语义不变。</summary>
    private static List<(double X, double Y, double W, double H)> RectShapes(XDocument content)
    {
        var list = new List<(double, double, double, double)>();
        foreach (var el in content.Descendants())
        {
            if (el.Name.LocalName != "PathObject") continue;
            var ad = el.Descendants().FirstOrDefault(d => d.Name.LocalName == "AbbreviatedData")?.Value;
            if (ad is null) continue;
            var nums = System.Text.RegularExpressions.Regex.Matches(ad, @"-?\d+(?:\.\d+)?")
                .Select(m => double.Parse(m.Value, System.Globalization.CultureInfo.InvariantCulture))
                .ToList();
            if (nums.Count < 4) continue;
            double minX = nums[0], minY = nums[1], maxX = nums[0], maxY = nums[1];
            for (int i = 2; i + 1 < nums.Count; i += 2)
            {
                minX = Math.Min(minX, nums[i]); maxX = Math.Max(maxX, nums[i]);
                minY = Math.Min(minY, nums[i + 1]); maxY = Math.Max(maxY, nums[i + 1]);
            }
            // Boundary 偏移还原（无 Boundary 时视为原点，兼容旧产物）
            var bAttr = (string?)el.Attribute("Boundary");
            if (bAttr is not null)
            {
                var b = bAttr.Split(' ').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
                minX += b[0]; minY += b[1]; maxX += b[0]; maxY += b[1];
            }
            list.Add((minX, minY, maxX - minX, maxY - minY));
        }
        return list;
    }

    /// <summary>
    /// 核心缺陷回归：连续 Add 多个段落时，各段文本行 Boundary.Y 必须严格递增（逐段向下排），
    /// 不再全部叠画在页面顶部。
    /// </summary>
    [Fact]
    public void Add_MultipleParagraphs_ShouldStackVerticallyWithoutOverlap()
    {
        var path = Path.Combine(OutDir(), "stack_paragraphs.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("第一段落文本").SetFontSize(5d));
            doc.Add(new Paragraph().AddText("第二段落文本").SetFontSize(5d));
            doc.Add(new Paragraph().AddText("第三段落文本").SetFontSize(5d));
            doc.Close();
        }

        var contents = AllPageContents(path);
        Assert.Single(contents); // 3 个短段落应全部落在一页

        var ys = TextObjects(contents[0]).Select(t => Boundary(t).Y).ToList();
        Assert.Equal(3, ys.Count);
        Assert.True(ys[0] < ys[1] && ys[1] < ys[2],
            $"段落应沿 y 轴依次下移（修复叠画缺陷），实际 Y=[{string.Join(", ", ys)}]");

        // 首段应位于页内容区顶部（MarginTop + 基线偏移 yOffset≈10）
        Assert.InRange(ys[0], 30, 40);
        // 行高 ≈ 字号 5mm×1.2 + 段距，段与段之间应有明显间隔
        Assert.True(ys[1] - ys[0] >= 8 && ys[2] - ys[1] >= 8,
            $"相邻段落间距过小，疑似重叠，Y=[{string.Join(", ", ys)}]");
    }

    /// <summary>
    /// 大量段落超过单页容量时自动分页（块级翻页），每页不再无限叠画。
    /// </summary>
    [Fact]
    public void Add_ManyParagraphs_ShouldAutoCreatePages()
    {
        var path = Path.Combine(OutDir(), "many_paragraphs.ofd");
        using (var doc = new OFDDoc(path))
        {
            for (int i = 0; i < 60; i++)
                doc.Add(new Paragraph().AddText($"第{i:D2}段：用于触发自动分页的流式内容。").SetFontSize(5d));
            doc.Close();
            Assert.True(doc.PageCount > 1,
                $"60 个段落远超一页容量，应自动分页，实际页数 {doc.PageCount}");
        }
    }

    /// <summary>
    /// 超长单个段落按行跨页：文字在页底处断开并续排到下一页，不截断、不丢失。
    /// </summary>
    [Fact]
    public void Add_LongParagraph_ShouldWrapAcrossPagesByLine()
    {
        var path = Path.Combine(OutDir(), "long_paragraph.ofd");
        // 约 990 字 × 5mm ≈ 35 行（A4 单页约 29 行），必然跨 2 页
        var body = string.Concat(Enumerable.Repeat(
            "跨页测试文本段落内容连续不断行，验证流式引擎行级分页能力与文字完整性。", 45));
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText(body).SetFontSize(5d));
            doc.Close();
            Assert.InRange(doc.PageCount, 2, 3);
        }

        var contents = AllPageContents(path);
        Assert.True(contents.Count >= 2, "超长段落应跨多页");

        var page0 = PageText(contents[0]);
        var page1 = PageText(contents[1]);
        Assert.True(page0.Length > 0, "第 1 页应含文本");
        Assert.True(page1.Length > 0, "第 2 页应续排段落剩余行");

        // 全文拼接后应与原文本一致（行级跨页不丢字）
        var joined = string.Concat(contents.Select(PageText));
        Assert.True(joined.Contains(body[..20]) && joined.Contains("文字完整性。"),
            "跨页后全文应完整保留首尾内容");

        // 第 1 页最后一行与第 2 页第一行不应重叠：第 2 页首行从页顶 MarginTop 区域开始
        var p0Last = TextObjects(contents[0]).Select(t => Boundary(t).Y).Max();
        var p1First = TextObjects(contents[1]).Select(t => Boundary(t).Y).Min();
        Assert.True(p1First > 0 && p1First < 40,
            $"第 2 页首行应从新页顶部内容区开始，实际 Y={p1First}");
        Assert.True(p0Last <= 297 - 25.4 + 1,
            $"第 1 页最后一行不应越出页内容底，实际 Y={p0Last}");
    }

    /// <summary>
    /// 绝对定位元素不参与流式堆叠：画在其指定 X/Y 处，且不推进后续段落的光标。
    /// </summary>
    [Fact]
    public void Add_AbsoluteDiv_ShouldNotAdvanceFlowCursor()
    {
        var path = Path.Combine(OutDir(), "absolute_isolated.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(180, 200)
                .SetWidth(20).SetHeight(20)
                .SetBackgroundColor(new CT_Color { Value = "255,0,0" }));
            doc.Add(new Paragraph().AddText("流式段落应从内容区顶部开始").SetFontSize(5d));
            doc.Close();
        }

        var contents = AllPageContents(path);
        var ys = TextObjects(contents[0]).Select(t => Boundary(t).Y).ToList();
        Assert.Single(ys);
        // 不受 absolute 元素影响：首段仍在 MarginTop + yOffset≈10 处
        Assert.InRange(ys[0], 30, 40);
    }

    /// <summary>
    /// 带固定高度的块级 Div 按整块排布：两块总高超页时第二块整体移入新页。
    /// </summary>
    [Fact]
    public void Add_HeightFixedDivs_ShouldPaginateWholeBlock()
    {
        var path = Path.Combine(OutDir(), "div_paginate.ofd");
        using (var doc = new OFDDoc(path))
        {
            // A4 内容区高约 246mm：两块 150mm 高块必须分两页
            doc.Add(new Div().SetWidth(100).SetHeight(150)
                .SetBackgroundColor(new CT_Color { Value = "200,200,200" }));
            doc.Add(new Div().SetWidth(100).SetHeight(150)
                .SetBackgroundColor(new CT_Color { Value = "150,150,150" }));
            doc.Close();
            Assert.Equal(2, doc.PageCount);
        }
    }

    /// <summary>
    /// 无文本内容的段落仍按一行占位推进，后续元素不与之叠画。
    /// </summary>
    [Fact]
    public void Add_EmptyParagraph_ShouldOccupyOneLineSlot()
    {
        var path = Path.Combine(OutDir(), "empty_paragraph.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph());                        // 空段落（仅占位）
            doc.Add(new Paragraph().AddText("空段落之后的内容").SetFontSize(5d));
            doc.Close();
        }

        var contents = AllPageContents(path);
        var ys = TextObjects(contents[0]).Select(t => Boundary(t).Y).ToList();
        Assert.Single(ys);
        // 空段占位后，本段应从 MarginTop 下移至少一行以上
        Assert.True(ys[0] > 40, $"空段落应按一行占位推进光标，实际 Y={ys[0]}");
    }

    /// <summary>
    /// 可拆纯盒跨页切分（对齐 Java <c>Div.split</c> 语义）：带边框/背景的高块在页尾剩余空间不足时
    /// 于切缝处断开——上半段（div1）恰好填满本页剩余，下半段（div2）从下一页顶部续排，
    /// 两段视觉高度相加等于整块高度（不再整块顶到下页、本页留白）。
    /// </summary>
    [Fact]
    public void Add_TallBorderDiv_ShouldSplitAcrossPages()
    {
        var path = Path.Combine(OutDir(), "split_bordered_div.ofd");
        using (var doc = new OFDDoc(path))
        {
            // 占位灰块把光标推到页中（A4：内容顶 25.4，30mm 块底到 55.4）
            doc.Add(new Div().SetWidth(100).SetHeight(30)
                .SetBackgroundColor(new CT_Color { Value = "200,200,200" }));
            // 220mm 带 1mm 边框的高块：剩余 271.6-55.4=216.2 < 222(1+220+1) → 应切断
            doc.Add(new Div().SetWidth(120).SetHeight(220).SetBorder(1)
                .SetBackgroundColor(new CT_Color { Value = "180,180,220" }));
            doc.Close();
            Assert.Equal(2, doc.PageCount);
        }

        var contents = AllPageContents(path);
        var page0Rects = RectShapes(contents[0]);
        var page1Rects = RectShapes(contents[1]);

        // 页 1：灰占位块(25.4..55.4) + 上半段——上半段底应恰为内容底 271.6（填满本页剩余）
        Assert.True(page0Rects.Count >= 2, $"页 1 应有占位块与上半段两个矩形，实际 {page0Rects.Count}");
        double page0MaxBottom = page0Rects.Max(r => r.Y + r.H);
        Assert.InRange(page0MaxBottom, 271.1, 272.1);
        // 上半段起点 = 光标 55.4 + 上边框 1 = 56.4
        Assert.True(page0Rects.Any(r => Math.Abs(r.Y - 56.4) < 0.5),
            $"页 1 应有从 56.4 开始的切断上半段，实际 Y=[{string.Join(", ", page0Rects.Select(r => $"{r.Y:0.0}+{r.H:0.0}"))}]");

        // 页 2：仅下半段（fill + stroke 两个 PathObject 同边界），从内容顶 25.4 起、
        // 高度 ≈ 220-215.2 = 4.8（div2 上装饰清零）
        Assert.True(page1Rects.Count >= 1, "页 2 应有切断下半段矩形");
        var tail = page1Rects[0];
        Assert.InRange(tail.Y, 25.0, 25.8);
        Assert.InRange(tail.H, 4.2, 5.4);

        // 两段视觉高相加 ≈ 整块 220mm（fill/stroke 双 PathObject 同边界，先按 (Y,H) 去重）
        var distinct0 = page0Rects.Select(r => (Y: Math.Round(r.Y, 1), H: Math.Round(r.H, 1))).Distinct();
        double splitSum = distinct0.Where(t => t.Y > 50).Sum(t => t.H) + Math.Round(tail.H, 1);
        Assert.InRange(splitSum, 219, 221);
    }

    /// <summary>
    /// 可拆纯盒浮动元素跨页切分（对齐 Java segmentBlocking）：带固定高的左浮动块在页尾
    /// 剩余空间不足时于切缝处断开——上半段填满本页剩余，下半段续排下一页，而非整块移页。
    /// </summary>
    [Fact]
    public void Add_TallLeftFloat_ShouldSplitAcrossPages()
    {
        var path = Path.Combine(OutDir(), "split_left_float.ofd");
        using (var doc = new OFDDoc(path))
        {
            // 占位灰块把光标推到页中（A4：内容顶 25.4，30mm 块底到 55.4）
            doc.Add(new Div().SetWidth(100).SetHeight(30)
                .SetBackgroundColor(new CT_Color { Value = "200,200,200" }));
            // 220mm 左浮动高块：剩余 271.6-55.4=216.2 < 220 → 应切断跨页
            doc.Add((Div)FloatBox(80, 220, AFloat.Left, Clear.None, "180,180,220"));
            doc.Close();
            Assert.Equal(2, doc.PageCount);
        }

        var contents = AllPageContents(path);
        var page0Rects = RectShapes(contents[0]);
        var page1Rects = RectShapes(contents[1]);

        // 页 1：灰占位块(25.4..55.4) + 左浮动上半段(55.4..271.6)，贴内容区左缘 31.7
        Assert.True(page0Rects.Any(r => Math.Abs(r.X - 31.7) < 0.5 && Math.Abs(r.Y - 55.4) < 0.5),
            $"页 1 应含从左缘 31.7 开始的浮动上半段，Y=[{string.Join(", ", page0Rects.Select(r => $"{r.Y:0.0}+{r.H:0.0}"))}]");
        Assert.True(page0Rects.Any(r => Math.Abs((r.Y + r.H) - 271.6) < 0.7),
            $"页 1 浮动上半段应填满至内容底 271.6，实际=[{string.Join(", ", page0Rects.Select(r => $"{r.Y:0.0}+{r.H:0.0}"))}]");

        // 页 2：仅左浮动下半段，从内容顶 25.4 起、贴左缘、高 ≈ 220-216.2 = 3.8
        var tail = page1Rects.Count > 0 ? page1Rects[0] : (X: 0d, Y: 0d, W: 0d, H: 0d);
        Assert.InRange(tail.Y, 25.0, 25.8);
        Assert.InRange(tail.X, 31.3, 32.1);
        Assert.InRange(tail.H, 3.0, 4.6);
    }

    /// <summary>
    /// Integrity（不可拆）块在剩余空间不足时整块移入新页，绝不切断：
    /// 两页各保留一个完整 150mm 高块。
    /// </summary>
    [Fact]
    public void Add_IntegrityTallDiv_ShouldMoveWholeToNextPage()
    {
        var path = Path.Combine(OutDir(), "integrity_no_split.ofd");
        using (var doc = new OFDDoc(path))
        {
            for (int i = 0; i < 2; i++)
                doc.Add(new Div().SetWidth(100).SetHeight(150).SetIntegrity(true)
                    .SetBackgroundColor(new CT_Color { Value = "150,150,150" }));
            doc.Close();
            Assert.Equal(2, doc.PageCount);
        }

        var contents = AllPageContents(path);
        // 每页恰一个完整矩形（若被切断则页 1 会出现第二个残段矩形）
        var r0 = RectShapes(contents[0]);
        var r1 = RectShapes(contents[1]);
        Assert.Single(r0);
        Assert.Single(r1);
        Assert.InRange(r0[0].Y, 25.0, 25.8);   // 页 1 块从内容顶开始
        Assert.InRange(r0[0].H, 149, 151);     // 完整高度，未被切断
        Assert.InRange(r1[0].Y, 25.0, 25.8);   // 页 2 块也从内容顶开始（整块移页）
        Assert.InRange(r1[0].H, 149, 151);
    }

    /// <summary>
    /// 段落/表格混排（修复「表格与段落各自独立队列、一律最后统一追加」的乱序缺陷）：
    /// 前言段落 → 表格 → 结语段落 按 Add 书写顺序就地排版，Y 严格递增、不叠画。
    /// </summary>
    [Fact]
    public void AddTableBetweenParagraphs_ShouldPreserveWritingOrder()
    {
        var path = Path.Combine(OutDir(), "table_mixed.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("前言段落").SetFontSize(5d));

            var table = new Table { ColumnCount = 3, RowCount = 2 };
            table.AddCell(new TableCell { Content = "姓名" }, 0, 0);
            table.AddCell(new TableCell { Content = "年龄" }, 0, 1);
            table.AddCell(new TableCell { Content = "城市" }, 0, 2);
            table.AddCell(new TableCell { Content = "张三" }, 1, 0);
            table.AddCell(new TableCell { Content = "30" }, 1, 1);
            table.AddCell(new TableCell { Content = "青岛" }, 1, 2);
            doc.AddTable(table);

            doc.Add(new Paragraph().AddText("结语段落").SetFontSize(5d));
            doc.Close();
            Assert.Equal(1, doc.PageCount);
        }

        var contents = AllPageContents(path);
        var objs = TextObjects(contents[0]).Select(t => (T: TextOf(t), B: Boundary(t))).ToList();
        var pre = objs.First(o => o.T.Contains("前言"));
        var tbl = objs.Where(o => o.T is "姓名" or "年龄" or "城市" or "张三" or "30" or "青岛").ToList();
        var post = objs.First(o => o.T.Contains("结语"));
        Assert.Equal(6, tbl.Count);

        double tableTop = tbl.Min(o => o.B.Y);
        double tableBottom = tbl.Max(o => o.B.Y + o.B.H);

        // 书写顺序保持：前言(上) < 表格 < 结语(下)——表格不再被推迟到所有段落之后
        Assert.True(pre.B.Y < tableTop,
            $"前言段落应位于表格上方，实际 前言Y={pre.B.Y:0.0} 表顶Y={tableTop:0.0}");
        Assert.True(tableBottom < post.B.Y,
            $"结语段落应位于表格下方（表格插在段间而非文末），实际 表底Y={tableBottom:0.0} 结语Y={post.B.Y:0.0}");

        // 表格融入页边距体系：文本从内容左 25.4+cellPadding 起、Y 从内容顶之后起，而非贴页边 (0,0)
        Assert.True(tbl.Min(o => o.B.X) >= 25.0,
            $"表格应左对齐页内容区（X≥25.4），实际 X={tbl.Min(o => o.B.X):0.0}");
        Assert.True(tableTop >= 25.0, $"表格不应越出上页边距，实际表顶 Y={tableTop:0.0}");
    }

    /// <summary>
    /// 单表（无段落）也融入页边距体系：外框左上从内容区 (MarginLeft, MarginTop) 起、
    /// 宽占满内容区，修复旧 AddTable 一律从页面原点 (0,0) 排布、无视页边距的越界缺陷。
    /// A4 默认边距 {top=25.4, right=31.7, bottom=25.4, left=31.7} → 内容区 X∈[31.7, 178.3]。
    /// </summary>
    [Fact]
    public void AddTableOnly_ShouldRespectPageMargins()
    {
        var path = Path.Combine(OutDir(), "table_margins.ofd");
        using (var doc = new OFDDoc(path))
        {
            var table = new Table { ColumnCount = 2, RowCount = 2 };
            table.AddCell(new TableCell { Content = "A1" }, 0, 0);
            table.AddCell(new TableCell { Content = "B1" }, 0, 1);
            table.AddCell(new TableCell { Content = "A2" }, 1, 0);
            table.AddCell(new TableCell { Content = "B2" }, 1, 1);
            doc.AddTable(table);
            doc.Close();
        }

        var contents = AllPageContents(path);
        var rects = RectShapes(contents[0]);
        Assert.NotEmpty(rects);

        // 表外框左上 = (MarginLeft 31.7, MarginTop 25.4)
        double minX = rects.Min(r => r.X);
        double minY = rects.Min(r => r.Y);
        Assert.InRange(minX, 31.0, 32.5);
        Assert.InRange(minY, 25.0, 26.0);

        // 表宽占满内容区：右缘 ≈ 31.7 + (210 - 31.7*2) = 178.3，不越出右页边距
        double maxRight = rects.Max(r => r.X + r.W);
        Assert.InRange(maxRight, 177.5, 179.5);
    }

    /// <summary>
    /// 表格自身跨多页后，后续段落从表格「末页」接续（而非回第 1 页叠画）：
    /// 70 行表格分 3 页，表尾文字与表后结语同处最后一页。
    /// </summary>
    [Fact]
    public void AddTableAcrossPages_ThenParagraph_ShouldContinueOnLastPage()
    {
        var path = Path.Combine(OutDir(), "table_long_mixed.ofd");
        const int totalRows = 70;
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("表前说明").SetFontSize(5d));

            var table = new Table
            {
                ColumnCount = 2,
                RowCount = totalRows,
                RepeatHeader = true,
                HeaderRows = 1
            };
            table.AddCell(new TableCell { Content = "列A" }, 0, 0);
            table.AddCell(new TableCell { Content = "列B" }, 0, 1);
            for (int r = 1; r < totalRows; r++)
            {
                table.AddCell(new TableCell { Content = $"行{r}列0" }, r, 0);
                table.AddCell(new TableCell { Content = $"行{r}列1" }, r, 1);
            }
            doc.AddTable(table);

            doc.Add(new Paragraph().AddText("表格跨页后的结语").SetFontSize(5d));
            doc.Close();
        }

        var contents = AllPageContents(path);
        Assert.Equal(3, contents.Count);

        // 第 1 页不应含表后结语（表格不再被推迟到文末，结语紧跟表格末页）
        Assert.DoesNotContain("表格跨页后的结语", PageText(contents[0]));
        // 末页既有表格残余行，又有表后结语——结语接在表格末页下方而非叠画回第 1 页
        var lastText = PageText(contents[^1]);
        Assert.Contains("行69列", lastText);
        Assert.Contains("表格跨页后的结语", lastText);
    }

    /// <summary>
    /// 段首行缩进（FirstLineIndent，对齐 Java Paragraph.setFirstLineIndent）：第一行 X 起点
    /// 应比第二行多 indent*fontSize，第二行回到 startX。仅在 TextAlign=Left 时生效。
    /// </summary>
    [Fact]
    public void Paragraph_FirstLineIndent_ShouldShiftFirstLineX()
    {
        var path = Path.Combine(OutDir(), "paragraph_indent.ofd");
        using (var doc = new OFDDoc(path))
        {
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("首行缩进测试").SetFontSize(5d))
                .Add(NetOfd.Layout.Element.Span.LineBreak())
                .Add(new NetOfd.Layout.Element.Span("第二行无缩进").SetFontSize(5d));
            p.SetFirstLineIndent(2).SetTextAlign(TextAlign.Left);
            doc.Add(p);
            doc.Close();
        }

        var contents = AllPageContents(path);
        Assert.Single(contents);
        // 应有两个 TextObject（每行一个 Run）；首行 X = startX + 2*5 = startX + 10
        var textObjs = TextObjects(contents[0]).Select(t => Boundary(t)).ToList();
        Assert.Equal(2, textObjs.Count);
        // 第一行缩进 2 字符 * fontSize 5 = 10mm → 第一行 X 比第二行大 10mm（缩进起点靠右）
        double indentShift = textObjs[0].X - textObjs[1].X;
        Assert.InRange(indentShift, 9.8, 10.2);
    }

    /// <summary>
    /// Span 字间距（对齐 Java Span.setLetterSpacing）：ASCII 半角字宽 = 0.5em（增量 H 对齐 Java
    /// Font.getCharWidthScale 无映射默认）。"ABCD" 4×2.5mm + LetterSpacing 0.5mm×3 间距 = 11.5mm；
    /// 半角 run 写 DeltaX（推进量 2.5+0.5=3 三个）。
    /// </summary>
    [Fact]
    public void Span_LetterSpacing_ShouldWidenRunWidth()
    {
        var path = Path.Combine(OutDir(), "span_letterspacing.ofd");
        using (var doc = new OFDDoc(path))
        {
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("ABCD").SetFontSize(5d).SetLetterSpacing(0.5d));
            doc.Add(p);
            doc.Close();
        }

        var contents = AllPageContents(path);
        Assert.Single(contents);
        var textObj = TextObjects(contents[0]).First();
        var b = Boundary(textObj);
        // 半角 4×2.5=10mm + 3 个 0.5mm 间距 = 11.5mm
        Assert.InRange(b.W, 11.3, 11.7);
        // 半角 run 逐字推进：DeltaX = (2.5+0.5) ×3
        var dx = textObj.Descendants().First(e => e.Name.LocalName == "TextCode").Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);
        Assert.Equal("3 3 3", dx);
    }

    /// <summary>
    /// Span 零字间距回归：ASCII 半角 "ABCD" Boundary 宽度 = 4 × (5×0.5) = 10mm；半角 run 写 DeltaX。
    /// </summary>
    [Fact]
    public void Span_ZeroLetterSpacing_ShouldEqualBaselineWidth()
    {
        var path = Path.Combine(OutDir(), "span_letterspacing_zero.ofd");
        using (var doc = new OFDDoc(path))
        {
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("ABCD").SetFontSize(5d));
            doc.Add(p);
            doc.Close();
        }

        var contents = AllPageContents(path);
        var textObj = TextObjects(contents[0]).First();
        var b = Boundary(textObj);
        Assert.InRange(b.W, 9.8, 10.2); // 4×2.5=10mm（半角 0.5em）
        var dx = textObj.Descendants().First(e => e.Name.LocalName == "TextCode").Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);
        Assert.Equal("2.5 2.5 2.5", dx);
    }

    /// <summary>
    /// Span 字重（Weight，对齐 Java Span.setWeight(Weight)）：OFD TextObject 直接写 Weight 属性，
    /// 写入值应与 SetWeight 一致（100/200/.../900）。非 Bold=true 也能写任意 Weight 档。
    /// </summary>
    [Fact]
    public void Span_Weight_ShouldWriteTextObjectAttribute()
    {
        var path = Path.Combine(OutDir(), "span_weight.ofd");
        using (var doc = new OFDDoc(path))
        {
            // Weight=700（粗体细分档，OFD 表 45）：非默认 Bold=true，但显式写 Weight 属性
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("粗体700").SetFontSize(5d).SetWeight(700));
            doc.Add(p);
            doc.Close();
        }

        var contents = AllPageContents(path);
        Assert.Single(contents);
        var textObj = TextObjects(contents[0]).First();
        Assert.Equal("700", (string?)textObj.Attribute("Weight"));
    }

    /// <summary>
    /// Span 下划线精调（对齐 Java Span.setUnderline(boolean, double, double)）：
    /// UnderlineOffset 影响下划线 Y 位置、UnderlineWidth 影响下划线线宽。
    /// </summary>
    [Fact]
    public void Span_UnderlineOffsetWidth_ShouldApplyOffsetAndWidth()
    {
        var path = Path.Combine(OutDir(), "span_underline_offset.ofd");
        using (var doc = new OFDDoc(path))
        {
            // fontSize=5mm + UnderlineOffset=2.5 + UnderlineWidth=0.4（精确控位 + 粗下划线）
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("下划线精调").SetFontSize(5d)
                    .SetUnderline(true, 2.5d, 0.4d));
            doc.Add(p);
            doc.Close();
        }

        var contents = AllPageContents(path);
        // 下划线走 CTPath 描边实现，页面内应有 PathObject 元素
        var underlinePath = contents[0].Descendants()
            .FirstOrDefault(el => el.Name.LocalName == "PathObject");
        Assert.NotNull(underlinePath);

        // 线宽 = 0.4（精调值，不再走 fontSize*0.05=0.25 默认）
        var lineWidthAttr = (string?)underlinePath!.Attribute("LineWidth");
        Assert.NotNull(lineWidthAttr);
        var lineWidth = double.Parse(lineWidthAttr!,
            System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(0.4, lineWidth, 2);

        // 下划线 Y 位置 = 段落首行 Y（页面绝对）+ fontSize(=baselineRelativeY) + UnderlineOffset
        // 实测：第一段 Y=35.4 + fontSize=5 + offset=2.5 = 42.9；通过 AbbreviatedData 子元素读 Y。
        var abbr = underlinePath.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "AbbreviatedData");
        Assert.NotNull(abbr);
        var tokens = abbr!.Value.Split(' ');
        var underlineY = double.Parse(tokens[2], System.Globalization.CultureInfo.InvariantCulture);
        Assert.InRange(underlineY, 42.5, 43.5);
    }

    /// <summary>
    /// 段落首行绝对宽度缩进（FirstLineIndentWidth，对齐 Java Paragraph.setFirstLineIndentWidth）：
    /// 当 FirstLineIndentWidth &gt; 0 时优先于 FirstLineIndent 字符数（绝对 mm 覆盖）。
    /// </summary>
    [Fact]
    public void Paragraph_FirstLineIndentWidth_ShouldOverrideCharCount()
    {
        var path = Path.Combine(OutDir(), "paragraph_indent_width.ofd");
        using (var doc = new OFDDoc(path))
        {
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("绝对宽度首行缩进").SetFontSize(5d))
                .Add(NetOfd.Layout.Element.Span.LineBreak())
                .Add(new NetOfd.Layout.Element.Span("第二行无缩进").SetFontSize(5d));
            // 同时设字符数（2*5=10mm）与绝对宽度（15mm）：绝对宽度应优先
            p.SetFirstLineIndent(2).SetFirstLineIndentWidth(15).SetTextAlign(TextAlign.Left);
            doc.Add(p);
            doc.Close();
        }

        var contents = AllPageContents(path);
        var textObjs = TextObjects(contents[0]).Select(t => Boundary(t)).ToList();
        Assert.Equal(2, textObjs.Count);
        // 首行 X 比第二行 X 大 15mm（绝对宽度覆盖字符数）
        double indentShift = textObjs[0].X - textObjs[1].X;
        Assert.InRange(indentShift, 14.8, 15.2);
    }

    // ─────────────────────────────────────────────────────────────
    // 浮动布局引擎（大缺口 #1 最小可观测子集，语义对齐 Java Segment 行模型）
    // A4：contentLeft=31.7，contentWidth=146.6，contentRight=178.3，contentTop=25.4
    // ─────────────────────────────────────────────────────────────

    private static void AssertRectNear(List<(double X, double Y, double W, double H)> rects,
        double x, double y, double w, double h)
    {
        Assert.True(rects.Any(r => Math.Abs(r.X - x) < 0.5 && Math.Abs(r.Y - y) < 0.5
                && Math.Abs(r.W - w) < 0.5 && Math.Abs(r.H - h) < 0.5),
            $"页面应含 ({x:0.0},{y:0.0}) {w:0.0}x{h:0.0} 矩形，实际=["
            + string.Join("; ", rects.Select(r => $"({r.X:0.0},{r.Y:0.0}) {r.W:0.0}x{r.H:0.0}")) + "]");
    }

    private static Div FloatBox(double w, double h, AFloat f, Clear c, string color)
        => new Div().SetWidth(w).SetHeight(h).SetFloat(f).SetClear(c)
            .SetBackgroundColor(new CT_Color { Value = color });

    /// <summary>
    /// 两个 left 浮动块共享同一浮动行（Java Segment 行模型：left 元素贴左、startX 依次右排）：
    /// 第二个块紧邻第一个右侧，二者 Y 顶对齐（同 25.4）。
    /// </summary>
    [Fact]
    public void Float_LeftTwo_ShouldShareRowSideBySide()
    {
        var path = Path.Combine(OutDir(), "float_left_two.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(60, 20, AFloat.Left, Clear.None, "220,60,60"));
            doc.Add(FloatBox(60, 20, AFloat.Left, Clear.None, "60,120,220"));
            doc.Close();
            Assert.Equal(1, doc.PageCount);
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 31.7, 25.4, 60, 20); // 第一个贴内容区左缘
        AssertRectNear(rects, 91.7, 25.4, 60, 20); // 第二个同行共享，紧邻其右（31.7+60）
    }

    /// <summary>
    /// left 与 right 浮动块同行共存（Java elementPositioning：left 贴左、right 贴右）：
    /// 右块贴内容区右缘（178.3），与左块 Y 顶对齐。
    /// </summary>
    [Fact]
    public void Float_LeftAndRight_ShouldShareRowAtBothEdges()
    {
        var path = Path.Combine(OutDir(), "float_left_right.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(50, 15, AFloat.Left, Clear.None, "220,120,60"));
            doc.Add(FloatBox(30, 15, AFloat.Right, Clear.None, "60,60,220"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 31.7, 25.4, 50, 15);   // left 贴左
        AssertRectNear(rects, 148.3, 25.4, 30, 15);  // right 贴右：178.3-30
    }

    /// <summary>
    /// 两个 right 浮动块同行共享：Java endX 依次左移（第一个贴右缘 138.3，第二个在其左 98.3）。
    /// </summary>
    [Fact]
    public void Float_RightTwo_ShouldStackRightToLeft()
    {
        var path = Path.Combine(OutDir(), "float_right_two.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(40, 10, AFloat.Right, Clear.None, "60,180,60"));
            doc.Add(FloatBox(40, 10, AFloat.Right, Clear.None, "180,60,180"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 138.3, 25.4, 40, 10); // 178.3-40
        AssertRectNear(rects, 98.3, 25.4, 40, 10);  // 138.3-40
    }

    /// <summary>
    /// center 浮动独占居中行（Java isCenterFloat）：水平居中于内容区（75.0 = 31.7+(146.6-60)/2）；
    /// 行结束后光标推进行底（25.4+20=45.4），后续 left 浮动从新行起排。
    /// </summary>
    [Fact]
    public void Float_Center_ShouldOccupyCenteredRow_ThenNextStartsBelow()
    {
        var path = Path.Combine(OutDir(), "float_center.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(60, 20, AFloat.Center, Clear.None, "220,180,60"));
            doc.Add(FloatBox(40, 10, AFloat.Left, Clear.None, "60,120,220"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 75.0, 25.4, 60, 20);  // center 独占居中行
        AssertRectNear(rects, 31.7, 45.4, 40, 10);  // 后续 left 从 center 行底下排
    }

    /// <summary>
    /// 浮动行结束后独占段落从行底下续排（Java Paragraph 默认 clear=both 独占段，不与浮动行共享）：
    /// 左浮动块 30mm 高占第一行（底 55.4），段落文字首行 Y 从 55.4 之下起（不叠画、无环绕）。
    /// </summary>
    [Fact]
    public void Float_LeftThenParagraph_ShouldEndRowAndStackBelow()
    {
        var path = Path.Combine(OutDir(), "float_then_paragraph.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(40, 30, AFloat.Left, Clear.None, "220,60,60"));
            doc.Add(new Paragraph().AddText("浮动行下方的段落文本").SetFontSize(5d));
            doc.Close();
        }
        var contents = AllPageContents(path);
        var rects = RectShapes(contents[0]);
        AssertRectNear(rects, 31.7, 25.4, 40, 30); // 浮动块占第一行（25.4..55.4）
        var textY = Boundary(TextObjects(contents[0]).First()).Y;
        // 行底 55.4 + 基线偏移（首段文本 Y≈光标+10）：应从浮动块之下排布
        Assert.InRange(textY, 60, 70);
    }

    /// <summary>
    /// clear 冲突换新行（Java tryAdd：left+clear:left 且行内已有 left 浮动 → 拒绝加入共享段）：
    /// 第一个 left 块占行 25.4..45.4；第二个 left+clear:left 触发冲突 → 结束行、光标推进到行底 45.4，
    /// 新行起排（Java 开新 Segment）。
    /// </summary>
    [Fact]
    public void Float_ClearLeftConflict_ShouldBreakToNewRow()
    {
        var path = Path.Combine(OutDir(), "float_clear_left.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.None, "220,60,60"));
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.Left, "60,120,220"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 31.7, 25.4, 40, 20); // 第一行
        AssertRectNear(rects, 31.7, 45.4, 40, 20); // 冲突 → 新行（Java 独占段语义边界）
    }

    /// <summary>
    /// 浮动块默认 clear=both（Div 默认值）即独占（Java isBlockElement）：
    /// 第二个 left 浮动不与第一个共享行，从行底 45.4 独占成行、仍贴左排。
    /// </summary>
    [Fact]
    public void Float_DefaultClearBoth_ShouldOwnExclusiveRow()
    {
        var path = Path.Combine(OutDir(), "float_default_both.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.None, "220,60,60"));
            // 未显式 SetClear → 默认 Clear.Both → 独占（Java isBlockElement）
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.Both, "60,120,220"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 31.7, 25.4, 40, 20);
        AssertRectNear(rects, 31.7, 45.4, 40, 20);
    }

    // ── 增量 E：Span 文字填充 / 描边（Java setFill + CT_Text Stroke/StrokeColor）──────────

    /// <summary>
    /// 空心轮廓字：Fill=false + Stroke=true → TextObject 同时写 Fill="false" 与 Stroke="true"。
    /// Java ParagraphRender 中 `if (!s.isFill()) txtObj.setFill(false)` 直写 CT_Text。
    /// </summary>
    [Fact]
    public void Span_FillFalseStrokeTrue_ShouldWriteBothTextObjectAttrs()
    {
        var path = Path.Combine(OutDir(), "span_hollow.ofd");
        using (var doc = new OFDDoc(path))
        {
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("空心字").SetFontSize(5d)
                    .SetFill(false).SetStroke(true));
            doc.Add(p);
            doc.Close();
        }

        var textObj = TextObjects(AllPageContents(path)[0]).First();
        Assert.Equal("false", (string?)textObj.Attribute("Fill"));
        Assert.Equal("true", (string?)textObj.Attribute("Stroke"));
    }

    /// <summary>默认 Fill=true / Stroke=false 时，TextObject 不写多余属性（保持干净 XML）。</summary>
    [Fact]
    public void Span_DefaultFillStroke_ShouldNotWriteAttrs()
    {
        var path = Path.Combine(OutDir(), "span_fill_default.ofd");
        using (var doc = new OFDDoc(path))
        {
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("普通文字").SetFontSize(5d));
            doc.Add(p);
            doc.Close();
        }

        var textObj = TextObjects(AllPageContents(path)[0]).First();
        Assert.Null(textObj.Attribute("Fill"));
        Assert.Null(textObj.Attribute("Stroke"));
    }

    /// <summary>描边色 StrokeColor 写为 TextObject 子元素（NetOfd 扩展，对齐 CT_Text.setStrokeColor）。</summary>
    [Fact]
    public void Span_StrokeColor_ShouldWriteChildElement()
    {
        var path = Path.Combine(OutDir(), "span_stroke_color.ofd");
        using (var doc = new OFDDoc(path))
        {
            var red = new NetOfd.Core.PageDescription.Color.CT_Color { Value = "255 0 0" };
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span("红描边").SetFontSize(5d)
                    .SetFill(false).SetStroke(true).SetStrokeColor(red));
            doc.Add(p);
            doc.Close();
        }

        var textObj = TextObjects(AllPageContents(path)[0]).First();
        var strokeColor = textObj.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "StrokeColor");
        Assert.NotNull(strokeColor);
        Assert.Equal("255 0 0", (string?)strokeColor!.Attribute("Value"));
    }

    /// <summary>
    /// 节点漂移回归：同一 Span.Color 实例被文字 FillColor 与下划线 PathObject.StrokeColor
    /// 共用、且折行多 run 共享时——XML 节点不得从旧父（前一 run）挪走导致 FillColor 丢失。
    /// 修复后每个 TextObject 必须保留独立 FillColor。
    /// </summary>
    [Fact]
    public void Span_SharedRedUnderlineWrapped_ShouldKeepFillColorOnEveryRun()
    {
        var path = Path.Combine(OutDir(), "span_color_drift.ofd");
        using (var doc = new OFDDoc(path))
        {
            var red = new NetOfd.Core.PageDescription.Color.CT_Color { Value = "255 0 0" };
            // 40 汉字 × fontSize 5mm = 200mm &gt; 146.6mm 内容宽 → 折 2 行（2 个 TextObject run）
            var p = new Paragraph()
                .Add(new NetOfd.Layout.Element.Span(
                        "红色下划线折行颜色保持红色下划线折行颜色保持红色下划线折行颜色保持")
                    .SetFontSize(5d).SetColor(red).SetUnderline(true));
            doc.Add(p);
            doc.Close();
        }

        var contents = AllPageContents(path)[0];
        var textObjs = TextObjects(contents).ToList();
        Assert.True(textObjs.Count >= 2, $"期望折行为 ≥2 个 TextObject，实际 {textObjs.Count}");

        // 漂移回归核心：每个 run 的 TextObject 都必须保留自己的 FillColor
        foreach (var t in textObjs)
        {
            var fillColor = t.Elements().FirstOrDefault(e => e.Name.LocalName == "FillColor");
            Assert.NotNull(fillColor);
            Assert.Equal("255 0 0", (string?)fillColor!.Attribute("Value"));
        }
        // 下划线 PathObject 使用独立的 StrokeColor 拷贝，不侵占文字 FillColor
        var strokePaths = contents.Descendants()
            .Where(el => el.Name.LocalName == "PathObject").ToList();
        Assert.NotEmpty(strokePaths);
        Assert.All(strokePaths, pathEl =>
        {
            Assert.NotNull(pathEl.Elements().FirstOrDefault(e => e.Name.LocalName == "StrokeColor"));
        });
    }

    // ── 增量 F：inline-block 行内共享 + BR 换行（Java SegmentationEngine 完整语义）────────

    /// <summary>
    /// InlineBlock 元素（SetDisplay(InlineBlock) → Clear=None + Float=None）与 left 浮动同行共享：
    /// Java Div.aFloat 默认 left、clear=none 即行内共享贴左——蓝块紧邻红块右侧、Y 顶对齐。
    /// </summary>
    [Fact]
    public void Inline_AfterLeftFloat_ShouldPlaceRightBesideIt()
    {
        var path = Path.Combine(OutDir(), "inline_after_left.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(60, 20, AFloat.Left, Clear.None, "220,60,60"));
            // InlineBlock（Float=None + Clear=None → 引擎归一 Left 贴左共享）
            doc.Add(new Div().SetWidth(50).SetHeight(20).SetDisplay(Display.InlineBlock)
                .SetBackgroundColor(new CT_Color { Value = "60,120,220" }));
            doc.Close();
        }

        var rects = RectShapes(AllPageContents(path)[0]);
        Assert.Equal(2, rects.Count);
        AssertRectNear(rects, 31.7, 25.4, 60, 20); // left 红
        AssertRectNear(rects, 91.7, 25.4, 50, 20); // inline 蓝紧邻（31.7+60）
    }

    /// <summary>left + inline + right 三元素同一浮动行水平拼接（Java Segment 行内共享）。</summary>
    [Fact]
    public void Inline_WithLeftAndRight_ShouldShareOneRow()
    {
        var path = Path.Combine(OutDir(), "inline_lr.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(30, 20, AFloat.Left, Clear.None, "220,60,60"));
            doc.Add(new Div().SetWidth(40).SetHeight(20).SetDisplay(Display.InlineBlock)
                .SetBackgroundColor(new CT_Color { Value = "60,120,220" }));
            doc.Add(FloatBox(30, 20, AFloat.Right, Clear.None, "60,180,60"));
            doc.Close();
        }

        var rects = RectShapes(AllPageContents(path)[0]);
        Assert.Equal(3, rects.Count);
        AssertRectNear(rects, 31.7, 25.4, 30, 20);  // left 贴左
        AssertRectNear(rects, 61.7, 25.4, 40, 20);  // inline 紧邻 left（Java 默认贴左累加）
        AssertRectNear(rects, 148.3, 25.4, 30, 20); // right 贴右（contentRight-30）
    }

    /// <summary>inline 元素行内放不下 → 封段开新行（Java Segment.tryAdd remainWidth 不足返回 false）。</summary>
    [Fact]
    public void Inline_OverflowWidth_ShouldBreakToNewRow()
    {
        var path = Path.Combine(OutDir(), "inline_overflow.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(90, 20, AFloat.Left, Clear.None, "220,60,60"));
            // 90 + 70 = 160 &gt; 146.6 内容宽 → inline 放不下断行
            doc.Add(new Div().SetWidth(70).SetHeight(20).SetDisplay(Display.InlineBlock)
                .SetBackgroundColor(new CT_Color { Value = "60,120,220" }));
            doc.Close();
        }

        var rects = RectShapes(AllPageContents(path)[0]);
        Assert.Equal(2, rects.Count);
        AssertRectNear(rects, 31.7, 25.4, 90, 20);  // left 首行
        AssertRectNear(rects, 31.7, 45.4, 70, 20);  // inline 断行到新行（Java 开新 Segment）
    }

    /// <summary>BR 换行符强制结束浮动行（Java SegmentationEngine：遇 BR 封段开新段）。</summary>
    [Fact]
    public void Br_ShouldEndFloatRow_NextStartsNewRow()
    {
        var path = Path.Combine(OutDir(), "br_break.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.None, "220,60,60"));
            doc.Add(new BR());
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.None, "60,120,220"));
            doc.Close();
        }

        var rects = RectShapes(AllPageContents(path)[0]);
        Assert.Equal(2, rects.Count);
        AssertRectNear(rects, 31.7, 25.4, 40, 20); // BR 前首行
        AssertRectNear(rects, 31.7, 45.4, 40, 20); // BR 后新行（Java 新 Segment 从行底下）
    }

    /// <summary>
    /// Java isBlockElement = Both || (Right &amp;&amp; Clear:Left)——left+clear:right <b>非独占</b>，
    /// 与段内既有 left 元素同行贴左共享（修正此前 .NET「三项独占」过度判定）。
    /// </summary>
    [Fact]
    public void LeftClearRight_ShouldShareRowLikePlainLeftFloat()
    {
        var path = Path.Combine(OutDir(), "left_clear_right.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.None, "220,60,60"));
            // left + clear:right：Java 无拦截规则 → 与既有 left 同行贴左（非独占行）
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.Right, "60,120,220"));
            doc.Close();
        }

        var rects = RectShapes(AllPageContents(path)[0]);
        Assert.Equal(2, rects.Count);
        AssertRectNear(rects, 31.7, 25.4, 40, 20);  // 第一个 left
        AssertRectNear(rects, 71.7, 25.4, 40, 20);  // left+clear:right 同行贴左（31.7+40）
    }

    // ─────────────────────────────────────────────────────────────
    // 增量 G：段落行级对齐 + 统一基线 + PlaceholderSpan（Java TxtLineBlock）
    // ─────────────────────────────────────────────────────────────
    // 流式段落默认 X = contentLeft = 31.7（A4 左页边距）；段宽 SetWidth 后行内容
    // 在 [startX, startX+width] 内排布。多 Span 同行 Center/Right 必须「整行一次平移」
    // （Java ParagraphRender switch textAlign）——旧实现逐 run 各自平移导致错位。

    private static double TextCodeY(XElement textObj)
    {
        var tc = textObj.Descendants().First(e => e.Name.LocalName == "TextCode");
        var yAttr = tc.Attribute("Y");
        Assert.NotNull(yAttr);
        return double.Parse(yAttr.Value);
    }

    [Fact]
    public void MultiSpan_AlignCenter_ShouldShiftWholeLineOnce()
    {
        var path = Path.Combine(OutDir(), "g_center_multispan.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(60).SetTextAlign(TextAlign.Center)
                .Add(new Span("甲").SetFontSize(5))
                .Add(new Span("乙丙").SetFontSize(5)));
        }
        var objs = TextObjects(AllPageContents(path)[0]).ToList();
        Assert.Equal(2, objs.Count);               // 两 run 各一 TextObject
        var x1 = Boundary(objs[0]).X;
        var x2 = Boundary(objs[1]).X;
        // 行宽 15（5+10），段宽 60 → slack=45、delta=22.5：整行一次平移
        Assert.InRange(x1, 31.7 + 22.4, 31.7 + 22.6);   // 54.1 ≈ 54.2
        Assert.InRange(x2 - x1, 4.9, 5.1);              // run 间距保持 5mm（span1 宽）
    }

    [Fact]
    public void MultiSpan_AlignRight_ShouldShiftWholeLineOnce()
    {
        var path = Path.Combine(OutDir(), "g_right_multispan.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(60).SetTextAlign(TextAlign.Right)
                .Add(new Span("甲").SetFontSize(5))
                .Add(new Span("乙丙").SetFontSize(5)));
        }
        var objs = TextObjects(AllPageContents(path)[0]).ToList();
        var x1 = Boundary(objs[0]).X;
        var x2 = Boundary(objs[1]).X;
        // delta=45：末 run 右缘 = 31.7+60 = 91.7
        Assert.InRange(x2 + Boundary(objs[1]).W, 91.6, 91.8);
        Assert.InRange(x2 - x1, 4.9, 5.1);              // 间距保持
    }

    [Fact]
    public void MixedFontSize_ShouldShareUniformBaseline()
    {
        var path = Path.Combine(OutDir(), "g_mixedfont.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(60)
                .Add(new Span("大字").SetFontSize(10))
                .Add(new Span("小").SetFontSize(5)));
        }
        var objs = TextObjects(AllPageContents(path)[0]).ToList();
        Assert.Equal(2, objs.Count);
        // 统一基线 = 行内最高字身 10（Java TextCode y = maxSpanHeight）
        Assert.Equal(10, TextCodeY(objs[0]));
        Assert.Equal(10, TextCodeY(objs[1]));
        // Boundary 高统一 = 1.2×10 = 12（行高）
        Assert.InRange(Boundary(objs[0]).H, 11.9, 12.1);
        Assert.InRange(Boundary(objs[1]).H, 11.9, 12.1);
    }

    [Fact]
    public void PlaceholderSpan_ShouldOccupyWidthWithoutText()
    {
        var path = Path.Combine(OutDir(), "g_placeholder.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(60)
                .Add(new PlaceholderSpan(20d, 5d))
                .Add(new Span("字").SetFontSize(5)));
        }
        var content = AllPageContents(path)[0];
        var objs = TextObjects(content).ToList();
        Assert.Single(objs);                            // 占位符不产生 TextObject
        Assert.Equal("字", TextOf(objs[0]));
        Assert.InRange(Boundary(objs[0]).X, 31.7 + 19.9, 31.7 + 20.1);  // 占位推 X 20mm
    }

    [Fact]
    public void PlaceholderSpan_CenterRow_ShouldIncludeHoldWidthInSlack()
    {
        var path = Path.Combine(OutDir(), "g_placeholder_center.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(60).SetTextAlign(TextAlign.Center)
                .Add(new PlaceholderSpan(20d, 5d))
                .Add(new Span("字").SetFontSize(5)));
        }
        var objs = TextObjects(AllPageContents(path)[0]).ToList();
        // 行宽 = 20+5=25，slack=35，delta=17.5 → 字 X = 31.7+20+17.5 = 69.2
        Assert.InRange(Boundary(objs[0]).X, 69.1, 69.3);
    }

    [Fact]
    public void PlaceholderSpan_OverflowWidth_ShouldBeDropped()
    {
        var path = Path.Combine(OutDir(), "g_placeholder_overflow.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(60)
                .Add(new PlaceholderSpan(120d, 5d))
                .Add(new Span("字").SetFontSize(5)));
        }
        var objs = TextObjects(AllPageContents(path)[0]).ToList();
        // 占位 120 > 段宽 60 → 丢弃（Java integrity 超宽忽略）；后续字仍在行首
        Assert.Single(objs);
        Assert.InRange(Boundary(objs[0]).X, 31.6, 31.8);
    }

    // ── 增量 H：逐字符宽度度量 + DeltaX 写路径（对齐 Java TxtGlyph.getW / ParagraphRender deltaX）──

    /// <summary>混合全角/半角 run 写 DeltaX：推进量 = 字宽（中 1.0em / A·B 0.5em）×字号。</summary>
    [Fact]
    public void MixedRun_DeltaX_ShouldReflectHalfWidthScale()
    {
        var path = Path.Combine(OutDir(), "incrH_mixed_dx.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().Add(new NetOfd.Layout.Element.Span("A中B").SetFontSize(5d)));
            doc.Close();
        }
        var textObj = TextObjects(AllPageContents(path)[0]).First();
        // 总宽 = 2.5 + 5 + 2.5 = 10mm
        Assert.InRange(Boundary(textObj).W, 9.8, 10.2);
        var dx = textObj.Descendants().First(e => e.Name.LocalName == "TextCode").Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);
        Assert.Equal("2.5 5", dx);   // A 宽 2.5 → B 起点；中宽 5 → 末字 B 起点
    }

    /// <summary>
    /// 纯中文 run 也写 DeltaX（每字符 1.0em 推进）。
    /// <para>
    /// 【2026-10-24 语义变更】原断言为「纯中文 run 不写 DeltaX（保持 XML 稳定）」。
    /// 该行为与 Java ofdrw 相反，且实测有害：不度量字宽的阅读器（ofdgo 等）会把整行
    /// 字符叠绘在起点——病案首页表格每格只显示一个字。改为对齐 ofdrw 的「≥2 字符即写」。
    /// </para>
    /// </summary>
    [Fact]
    public void PureChinese_DeltaX_ShouldBeWritten()
    {
        var path = Path.Combine(OutDir(), "incrH_chs_nodx.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().Add(new NetOfd.Layout.Element.Span("电子病历系统").SetFontSize(5d)));
            doc.Close();
        }
        var textObj = TextObjects(AllPageContents(path)[0]).First();
        Assert.InRange(Boundary(textObj).W, 29.8, 30.2);  // 6×5=30
        var code = textObj.Descendants().First(e => e.Name.LocalName == "TextCode");
        var dx = code.Attribute("DeltaX")?.Value;
        Assert.NotNull(dx);
        Assert.Equal("5 5 5 5 5", dx);   // 6 字 → 5 个推进量，每值 = 1.0em × 5mm
    }

    /// <summary>半角 run 下划线 PathObject 宽度 = 半角总宽（5×2.5=12.5mm，非旧全等宽 25）。</summary>
    [Fact]
    public void HalfWidthUnderline_ShouldMatchScaledRunWidth()
    {
        var path = Path.Combine(OutDir(), "incrH_ascii_underline.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().Add(new NetOfd.Layout.Element.Span("T36.5").SetFontSize(5d).SetUnderline()));
            doc.Close();
        }
        var contents = AllPageContents(path)[0];
        var textObj = TextObjects(contents).First();
        Assert.InRange(Boundary(textObj).W, 12.3, 12.7);  // 5 半角 × 2.5 = 12.5
        var under = contents.Descendants().First(e => e.Name.LocalName == "PathObject");
        Assert.InRange(Boundary(under).W, 12.3, 12.7);
    }

    /// <summary>右对齐半角行：行 X = startX + (maxWidth − 半角总宽)，不再按全等宽偏移。</summary>
    [Fact]
    public void RightAlignHalfWidth_ShouldShiftByScaledWidth()
    {
        var path = Path.Combine(OutDir(), "incrH_right_ascii.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(100).SetTextAlign(TextAlign.Right)
                .Add(new NetOfd.Layout.Element.Span("ABC").SetFontSize(5d)));
            doc.Close();
        }
        var textObj = TextObjects(AllPageContents(path)[0]).First();
        // 半角 3×2.5=7.5 → X = 31.7 + (100−7.5) = 124.2；旧全等宽 15 → 116.7
        Assert.InRange(Boundary(textObj).X, 124.0, 124.4);
    }

    /// <summary>半角折行：11 个 ASCII 宽 27.5mm 放得进 30mm 段宽单行（旧全等宽 55 折 2 行）。</summary>
    [Fact]
    public void HalfWidthWrap_ShouldFitMoreCharsPerLine()
    {
        var path = Path.Combine(OutDir(), "incrH_wrap_ascii.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().SetWidth(30)
                .Add(new NetOfd.Layout.Element.Span("ABCDEFGHIJK").SetFontSize(5d)));
            doc.Close();
        }
        var objs = TextObjects(AllPageContents(path)[0]).ToList();
        Assert.Single(objs);                              // 单行不折
        Assert.InRange(Boundary(objs[0]).W, 27.3, 27.7);  // 11×2.5=27.5
    }

    /// <summary>
    /// 浮动行跨度占用「完整盒宽」（contentWidth + widthPlus 水平装饰，对齐 Java doPrepare）：
    /// 带 left margin 的 left 浮动块需为其 margin 预留横向空间，后续兄弟不得重叠入侵。
    /// </summary>
    [Fact]
    public void Float_WithLeftMargin_ShouldReserveFullBoxWidth()
    {
        var path = Path.Combine(OutDir(), "float_widthplus.ofd");
        using (var doc = new OFDDoc(path))
        {
            // 第一块：宽 40 + left margin 10 = 占位 50mm
            doc.Add(new Div().SetWidth(40).SetHeight(20).SetMarginLeft(10)
                    .SetFloat(AFloat.Left).SetClear(Clear.None)
                    .SetBackgroundColor(new CT_Color { Value = "220,60,60" }));
            // 第二块：宽 40，应紧邻第一块完整占位右侧（contentLeft 31.7 + 50 = 81.7）
            doc.Add(FloatBox(40, 20, AFloat.Left, Clear.None, "60,120,220"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        // 第一块视觉：X=内容左缘(31.7)、Y=25.4（X 侧 margin 不影响背景位置，仅计入浮动占位）
        AssertRectNear(rects, 31.7, 25.4, 40, 20);
        // 第二块紧邻完整占位（含 margin 50mm）右侧：31.7+50=81.7
        AssertRectNear(rects, 81.7, 25.4, 40, 20);
    }

    /// <summary>
    /// 多个连续 center 浮动块共享同一居中行（Java isCenterFloat + tryAdd：段内全为 center 时可续加，
    /// elementPositioning 按整组总宽整体水平居中）：
    /// 三个 center 宽 40/40/60，总宽 140，offset = 31.7 + (146.6-140)/2 = 35.0，
    /// 依次排：35.0、75.0、115.0。
    /// </summary>
    [Fact]
    public void Float_MultipleCenter_ShouldShareOneCenteredRow()
    {
        var path = Path.Combine(OutDir(), "float_multi_center.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(40, 20, AFloat.Center, Clear.None, "220,180,60"));
            doc.Add(FloatBox(40, 20, AFloat.Center, Clear.None, "60,60,220"));
            // 后续独占元素触发居中行结束（同时验证居中行在流式末尾也能正确渲染）
            doc.Add(FloatBox(60, 20, AFloat.Center, Clear.None, "60,120,220"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 35.0, 25.4, 40, 20);  // 第 1 个 center
        AssertRectNear(rects, 75.0, 25.4, 40, 20);  // 第 2 个 center
        AssertRectNear(rects, 115.0, 25.4, 60, 20); // 第 3 个 center
    }

    /// <summary>流式末尾仅剩 center 元素时，居中行也应在收尾时正确渲染（EndFloatRow 兜底）。</summary>
    [Fact]
    public void Float_TrailingCenterOnly_ShouldRenderCenteredAtEnd()
    {
        var path = Path.Combine(OutDir(), "float_trailing_center.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.Add(FloatBox(60, 20, AFloat.Center, Clear.None, "220,180,60"));
            doc.Close();
        }
        var rects = RectShapes(AllPageContents(path)[0]);
        AssertRectNear(rects, 75.0, 25.4, 60, 20);  // 单 center 居中：31.7+43.3
    }
}
