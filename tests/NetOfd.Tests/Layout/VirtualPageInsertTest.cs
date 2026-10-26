using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// VirtualPage 插入到指定页码（对应 Java <c>VPageParseEngine.addNewPage</c>）。
/// <para>
/// Java 语义：<c>pageNum</c> 为空 → 追加到末尾并回填实际页码；
/// <c>pageNum</c> 有值 → 把该页插入到第 <c>pageNum</c> 页（1-based）的位置，越界自动钳制。
/// 物理目录 <c>Page_N</c> 仍按追加顺序分配，文档页序由 Document.xml 的 &lt;Page&gt; 列表决定。
/// </para>
/// </summary>
public class VirtualPageInsertTest
{
    /// <summary>
    /// 输出目录。必须自行创建 —— 不能依赖其它测试残留的目录
    /// （Linux CI 上从零开始，该目录不存在会直接抛 DirectoryNotFoundException）。
    /// 用独立目录名，避免与其它测试类并行写同一目录。
    /// </summary>
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_vpage_insert_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>读取生成文档 Document.xml 中页面（BaseLoc）的先后顺序</summary>
    private static List<string> ReadPageOrder(string ofdPath)
    {
        using var zip = ZipFile.OpenRead(ofdPath);
        var entry = zip.GetEntry("Doc_0/Document.xml");
        Assert.NotNull(entry);
        using var s = entry!.Open();
        var xdoc = XDocument.Load(s);
        return xdoc.Descendants()
            .Where(e => e.Name.LocalName == "Page")
            .Select(p => p.Attribute("BaseLoc")?.Value ?? string.Empty)
            .ToList();
    }

    /// <summary>构造一个带文字的固定布局虚拟页</summary>
    private static VirtualPage MakeVPage(string text, string color = "0,0,255")
    {
        var vPage = new VirtualPage(PageLayout.A4());
        vPage.Add(new Div()
            .SetPosition(Position.Absolute)
            .SetXY(10, 10)
            .SetWidth(120)
            .SetHeight(20)
            .SetBackgroundColor(new CT_Color { Value = color }));
        vPage.Add(new Paragraph()
            .AddText(text)
            .SetFontSize(8d)
            .SetPosition(Position.Absolute)
            .SetXY(10, 40)
            .SetWidth(150));
        return vPage;
    }

    /// <summary>
    /// 指定页码插入：4 个虚拟页，第 4 个插入到第 2 位
    /// </summary>
    [Fact]
    public void VirtualPage_WithPageNum_ShouldInsertAtSpecifiedPosition()
    {
        var outPath = Out("VPage_InsertAt.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            doc.AddVPage(MakeVPage("A"));
            doc.AddVPage(MakeVPage("B"));
            doc.AddVPage(MakeVPage("C"));

            var vPage = MakeVPage("插入页");
            vPage.SetPageNum(2);
            doc.AddVPage(vPage);

            doc.Close();
        }

        var order = ReadPageOrder(outPath);
        Assert.Equal(4, order.Count);
        // 物理目录按追加分配：三个在先、插入页拿到最后一个 Page_3
        Assert.EndsWith("Page_0/Content.xml", order[0]);
        Assert.EndsWith("Page_3/Content.xml", order[1]);   // 插入到第 2 位
        Assert.EndsWith("Page_1/Content.xml", order[2]);
        Assert.EndsWith("Page_2/Content.xml", order[3]);
    }

    /// <summary>
    /// 插入到最前（pageNum=1），且与流式页混合
    /// </summary>
    [Fact]
    public void VirtualPage_WithPageNum_One_ShouldInsertAtFirstPosition()
    {
        var outPath = Out("VPage_InsertFirst.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            doc.Add(new Paragraph().AddText("流式页").SetFontSize(8d));

            var vPage = MakeVPage("插到最前");
            vPage.SetPageNum(1);
            doc.AddVPage(vPage);

            doc.Close();
        }

        var order = ReadPageOrder(outPath);
        Assert.Equal(2, order.Count);
        Assert.EndsWith("Page_1/Content.xml", order[0]);   // 虚拟页插到最前
        Assert.EndsWith("Page_0/Content.xml", order[1]);   // 原流式页后移
    }

    /// <summary>
    /// 页码越界：大于总页数时钳制为追加到末尾
    /// </summary>
    [Fact]
    public void VirtualPage_WithPageNum_OutOfRange_ShouldClampToEnd()
    {
        var outPath = Out("VPage_InsertClamp.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            doc.AddVPage(MakeVPage("A"));
            doc.AddVPage(MakeVPage("B"));

            var vPage = MakeVPage("越界页");
            vPage.SetPageNum(99);
            doc.AddVPage(vPage);

            doc.Close();
        }

        var order = ReadPageOrder(outPath);
        Assert.Equal(3, order.Count);
        Assert.EndsWith("Page_2/Content.xml", order[2]);   // 追加到末尾
    }

    /// <summary>
    /// 未指定页码：追加到末尾，并回填实际页码（对齐 Java newPage() 后 setPageNum(size)）
    /// </summary>
    [Fact]
    public void VirtualPage_WithoutPageNum_ShouldAppendAndBackfillPageNum()
    {
        var outPath = Out("VPage_AppendBackfill.ofd");
        var vPage = MakeVPage("追加页");
        using (var doc = new OFDDoc(outPath))
        {
            doc.AddVPage(MakeVPage("A"));
            doc.AddVPage(vPage);
            doc.Close();
        }

        Assert.Equal(2, vPage.PageNum);   // 渲染后回填为最终页码
        var order = ReadPageOrder(outPath);
        Assert.Equal(2, order.Count);
        Assert.EndsWith("Page_1/Content.xml", order[1]);
    }

    /// <summary>
    /// 回读验证：插入页在文档页序中位于第 2 位，Reader 按页序读取到正确内容
    /// </summary>
    [Fact]
    public void VirtualPage_InsertedPage_ShouldBeReadBackInDocumentOrder()
    {
        var outPath = Out("VPage_InsertReadBack.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            doc.AddVPage(MakeVPage("第一页"));
            doc.AddVPage(MakeVPage("第三页"));

            var vPage = MakeVPage("插入页");
            vPage.SetPageNum(2);
            doc.AddVPage(vPage);

            doc.Close();
        }

        using var reader = new OFDReader(outPath);
        Assert.Equal(3, reader.GetPageCount());
        Assert.Contains("第一页", reader.GetPageText(0));
        Assert.Contains("插入页", reader.GetPageText(1));
        Assert.Contains("第三页", reader.GetPageText(2));
    }
}
