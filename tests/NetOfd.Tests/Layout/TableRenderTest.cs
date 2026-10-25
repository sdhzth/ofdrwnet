using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Converter;
using SkiaSharp;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 表格渲染测试：验证 <see cref="OFDDoc.AddTable"/> 将表格真正渲染为 OFD 页面图元
/// （边框 PathObject + 单元格文字 TextObject + 跨行/跨列合并边框），而非静默丢弃。
/// </summary>
public class TableRenderTest
{
    private static string Out(string name) =>
        Path.Combine(Path.GetTempPath(), "netofd_table_" + name);

    private static int CountTag(string xml, string tag)
    {
        // OFD 元素带命名空间前缀，形如 <ofd:PathObject ...>；按完整开标签子串计数
        int count = 0, idx = 0;
        string needle = "<ofd:" + tag;
        while ((idx = xml.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }
        return count;
    }

    private static (int Path, int Text) CountContentObjects(string ofdPath)
    {
        int path = 0, text = 0;
        using var za = ZipFile.OpenRead(ofdPath);
        foreach (var entry in za.Entries.Where(e =>
                     e.FullName.EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var sr = new StreamReader(entry.Open());
            var xml = sr.ReadToEnd();
            path += CountTag(xml, "PathObject");
            text += CountTag(xml, "TextObject");
        }
        return (path, text);
    }

    [Fact]
    public void AddTable_ShouldRenderBordersAndTextIntoBodyLayer()
    {
        var outPath = Out("table_basic.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var table = new Table { ColumnCount = 3, RowCount = 2, Title = "人员表" };
            table.AddCell(new TableCell { Content = "姓名" }, 0, 0);
            table.AddCell(new TableCell { Content = "年龄" }, 0, 1);
            table.AddCell(new TableCell { Content = "城市" }, 0, 2);
            table.AddCell(new TableCell { Content = "张三" }, 1, 0);
            table.AddCell(new TableCell { Content = "30" }, 1, 1);
            table.AddCell(new TableCell { Content = "青岛" }, 1, 2);
            doc.AddTable(table);
            doc.Close();
        }

        Assert.True(File.Exists(outPath));
        var (path, text) = CountContentObjects(outPath);

        // 外边框 + 内部 2 竖 1 横 = 4 条 PathObject
        Assert.True(path >= 4, $"表格应渲染出边框 PathObject（外框+内部网格），实际 {path}");
        // 6 个单元格 + 1 标题 = 7 个 TextObject
        Assert.True(text >= 7, $"6 单元格 + 标题应渲染出 TextObject，实际 {text}");

        // 标题确已渲染
        using var za = ZipFile.OpenRead(outPath);
        var entry = za.Entries.First(e =>
            e.FullName.EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase));
        using var sr = new StreamReader(entry.Open());
        Assert.Contains("人员表", sr.ReadToEnd());
    }

    [Fact]
    public void AddTable_WithColumnSpan_ShouldMergeBorderAndRenderSingleHeaderText()
    {
        var outPath = Out("table_span.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var table = new Table { ColumnCount = 2, RowCount = 2 };
            // 第 0 行跨两列合并为表头
            table.AddCell(new TableCell { Content = "合并表头", ColumnSpan = 2 }, 0, 0);
            table.AddCell(new TableCell { Content = "左" }, 1, 0);
            table.AddCell(new TableCell { Content = "右" }, 1, 1);
            doc.AddTable(table);
            doc.Close();
        }

        var (path, text) = CountContentObjects(outPath);
        // 跨列合并后中间竖线消失：外框(1) + 竖线仅第1行(1) + 横线(2) = 4
        Assert.Equal(4, path);
        // 仅 3 个非空单元格 → 3 个 TextObject（合并表头只占 1 格）
        Assert.Equal(3, text);
    }

    [Fact]
    public void AddTable_ShouldRenderNonBlankBitmap()
    {
        var outPath = Out("table_bitmap.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var table = new Table { ColumnCount = 2, RowCount = 2 };
            table.AddCell(new TableCell { Content = "A1" }, 0, 0);
            table.AddCell(new TableCell { Content = "B1" }, 0, 1);
            table.AddCell(new TableCell { Content = "A2" }, 1, 0);
            table.AddCell(new TableCell { Content = "B2" }, 1, 1);
            doc.AddTable(table);
            doc.Close();
        }

        using var maker = new OfdImageMaker(outPath);
        using var bmp = maker.MakePage(0, new ConverterOptions { PPM = 4, AntiAlias = false });
        Assert.NotNull(bmp);

        int nonWhite = 0;
        for (int py = 0; py < bmp.Height; py += 7)
            for (int px = 0; px < bmp.Width; px += 7)
            {
                var c = bmp.GetPixel(px, py);
                if (c.Red < 240 || c.Green < 240 || c.Blue < 240) nonWhite++;
            }
        Assert.True(nonWhite > 0, "表格渲染后位图不应全白");
    }

    [Fact]
    public void AddTable_LongTable_ShouldPaginateAcrossMultiplePages()
    {
        var outPath = Out("table_paginate.ofd");
        const int totalRows = 70;
        using (var doc = new OFDDoc(outPath))
        {
            var table = new Table { ColumnCount = 2, RowCount = totalRows };
            for (int r = 0; r < totalRows; r++)
            {
                table.AddCell(new TableCell { Content = $"行{r}列0" }, r, 0);
                table.AddCell(new TableCell { Content = $"行{r}列1" }, r, 1);
            }
            doc.AddTable(table);
            doc.Close();
        }

        Assert.True(File.Exists(outPath));
        using var za = ZipFile.OpenRead(outPath);
        var pages = za.Entries
            .Where(e => e.FullName.EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // A4 默认上下页边距 25.4mm、行高 8mm：单页约放 33 行，70 行应分页为 3 页
        Assert.True(pages.Count >= 2, $"超长表格应自动分页，实际页数 {pages.Count}");
        Assert.True(pages.Count <= 4, $"70 行不应分页过多，实际页数 {pages.Count}");

        // 每一页都应含边框与文字图元，且不应出现整页 0 行的截断
        foreach (var entry in pages)
        {
            using var sr = new StreamReader(entry.Open());
            var xml = sr.ReadToEnd();
            Assert.True(CountTag(xml, "PathObject") >= 1, "每页应有边框");
            Assert.True(CountTag(xml, "TextObject") >= 1, "每页应有文字");
        }
    }

    [Fact]
    public void AddTable_WithRepeatHeader_ShouldRepeatHeaderOnNewPages()
    {
        var outPath = Out("table_repeat_header.ofd");
        const int totalRows = 70;
        using (var doc = new OFDDoc(outPath))
        {
            var table = new Table
            {
                ColumnCount = 2,
                RowCount = totalRows,
                RepeatHeader = true,
                HeaderRows = 1
            };
            table.AddCell(new TableCell { Content = "表头0" }, 0, 0);
            table.AddCell(new TableCell { Content = "表头1" }, 0, 1);
            for (int r = 1; r < totalRows; r++)
            {
                table.AddCell(new TableCell { Content = $"行{r}列0" }, r, 0);
                table.AddCell(new TableCell { Content = $"行{r}列1" }, r, 1);
            }
            doc.AddTable(table);
            doc.Close();
        }

        Assert.True(File.Exists(outPath));
        using var za = ZipFile.OpenRead(outPath);
        var pages = za.Entries
            .Where(e => e.FullName.EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(pages.Count >= 2, $"超长表格应分页，实际页数 {pages.Count}");

        // 首页随表格自然出现表头，后续页由 RepeatHeader 重复，故每页都应含表头文字
        foreach (var entry in pages)
        {
            using var sr = new StreamReader(entry.Open());
            var xml = sr.ReadToEnd();
            Assert.Contains("表头0", xml);
            Assert.Contains("表头1", xml);
        }
    }
}
