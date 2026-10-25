using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// AreaHolderBlock 写侧消费测试（增量 I，对齐 Java AreaHolderBlockRender / VPageParseEngine.convert2Layer）：
/// 向文档 Add 区域占位区块后，产物须与 Java 同构——页面 Body 图层插入空 <c>PageBlock</c>（仅 ID 占位）
/// + Doc_0/AreaHolderBlocks.xml 登记 { AreaName / Boundary / PageFile / PageBlockID / ID }。
/// 编辑期消费（AreaHolderContext.get 往区域填充内容）仍为后续大缺口，本测试只覆盖写侧。
/// </summary>
public class AreaHolderWriteTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_areaholder_write_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string ReadEntry(string ofd, string name)
    {
        using var za = ZipFile.OpenRead(ofd);
        var entry = za.GetEntry(name)
            ?? throw new InvalidOperationException($"zip 中缺少条目: {name}");
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private static string ReadXmlFile(string ofd, string name)
    {
        using var za = ZipFile.OpenRead(ofd);
        var entry = za.GetEntry(name)
            ?? throw new InvalidOperationException($"zip 中缺少条目: {name}");
        using var s = entry.Open();
        return new StreamReader(s, Encoding.UTF8).ReadToEnd();
    }

    /// <summary>读取 AreaHolderBlocks.xml 全部登记记录（AreaName/Boundary/PageFile/PageBlockID）。</summary>
    private static (string Name, STBox Boundary, string PageFile, long PageBlockId)[] ReadRecords(string ofd)
    {
        var xml = ReadXmlFile(ofd, "Doc_0/AreaHolderBlocks.xml");
        var root = XDocument.Parse(xml).Root!;
        return root.Descendants().Where(e => e.Name.LocalName == "AreaHolderBlock")
            .Select(e => (
                Name: e.Attribute("AreaName")!.Value,
                Boundary: STBox.Parse(e.Attribute("Boundary")!.Value),
                PageFile: e.Descendants().First(c => c.Name.LocalName == "PageFile").Value,
                PageBlockId: long.Parse(e.Attribute("PageBlockID")!.Value)))
            .ToArray();
    }

    /// <summary>读取指定页面 Content.xml 中 Body 图层直接子元素序列。</summary>
    private static XElement[] BodyLayerChildren(string ofd, string page = "Page_0")
    {
        var xml = ReadXmlFile(ofd, $"Doc_0/Pages/{page}/Content.xml");
        var root = XDocument.Parse(xml).Root!;
        var body = root.Descendants().First(e => e.Name.LocalName == "Layer"
                                                 && e.Attribute("Type")?.Value == "Body");
        return body.Elements().ToArray();
    }

    [Fact]
    public void Absolute_RegistersEmptyPageBlockAndXmlRecord()
    {
        var path = Path.Combine(OutDir(), "ah_absolute.ofd");
        using (var doc = new OFDDoc(path))
        {
            // 5 参构造：X/Y 绝对定位（默认 Clear=Both 独占块，X/Y 被流式引擎保留）
            doc.Add(new AreaHolderBlock("name", 52.406, 25.4, 100, 10));
            doc.Close();
        }

        var records = ReadRecords(path);
        Assert.Single(records);
        Assert.Equal("name", records[0].Name);
        Assert.Equal(52.406, records[0].Boundary.TopLeftX, 3);
        Assert.Equal(25.4, records[0].Boundary.TopLeftY, 3);
        Assert.Equal(100, records[0].Boundary.Width, 3);
        Assert.Equal(10, records[0].Boundary.Height, 3);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", records[0].PageFile);

        // 页面 Body 图层：唯一子元素为空的 PageBlock，ID 与登记引用一致
        var children = BodyLayerChildren(path);
        Assert.Single(children);
        Assert.Equal("PageBlock", children[0].Name.LocalName);
        Assert.Equal(records[0].PageBlockId.ToString(), children[0].Attribute("ID")!.Value);
        Assert.Empty(children[0].Elements());
    }

    [Fact]
    public void Flow_TwoBlocksStackAndRegisterSequentially()
    {
        var path = Path.Combine(OutDir(), "ah_flow.ofd");
        double contentLeft, contentTop;
        using (var doc = new OFDDoc(path))
        {
            contentLeft = doc.MarginLeft;
            contentTop = doc.MarginTop;
            doc.Add(new AreaHolderBlock("a", 100, 10));
            doc.Add(new AreaHolderBlock("b", 100, 10));
            doc.Close();
        }

        var records = ReadRecords(path);
        Assert.Equal(2, records.Length);
        // 独占块沿内容区顶逐块堆叠：无装饰时 cursorY 每块推进自身高度
        Assert.Equal("a", records[0].Name);
        Assert.Equal(contentLeft, records[0].Boundary.TopLeftX, 6);
        Assert.Equal(contentTop, records[0].Boundary.TopLeftY, 6);
        Assert.Equal("b", records[1].Name);
        Assert.Equal(contentLeft, records[1].Boundary.TopLeftX, 6);
        Assert.Equal(contentTop + 10, records[1].Boundary.TopLeftY, 6);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", records[0].PageFile);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", records[1].PageFile);
        Assert.NotEqual(records[0].PageBlockId, records[1].PageBlockId);
    }

    [Fact]
    public void BorderAndPadding_OffsetBoundaryAndStillRenderBox()
    {
        var path = Path.Combine(OutDir(), "ah_border_padding.ofd");
        using (var doc = new OFDDoc(path))
        {
            var ah = new AreaHolderBlock("c", 52.406, 25.4, 100, 10);
            ah.SetBorder(1).SetPadding(2);
            doc.Add(ah);
            doc.Close();
        }

        // NetOfd 坐标约定：div.X/Y 即内容绘制区起点（无 Java border-box 概念；流式引擎把 Y 推进到
        // 含上装饰的内容顶）。登记 Boundary 直接用元素声明坐标——与 Java 的「无装饰场景数值一致」
        // 对齐目标一致（Java 测试/样本均无 padding、border 仅视觉线），装饰不双加。
        var records = ReadRecords(path);
        Assert.Single(records);
        Assert.Equal(52.406, records[0].Boundary.TopLeftX, 3);
        Assert.Equal(25.4 + 1 + 2, records[0].Boundary.TopLeftY, 3); // 流式 Y 已含上装饰推进
        Assert.Equal(100, records[0].Boundary.Width, 3);
        Assert.Equal(10, records[0].Boundary.Height, 3);

        // 盒本体照常渲染（DivRender 画边框 PathObject），空 PageBlock 位于盒图元之后（Java 顺序）
        var children = BodyLayerChildren(path);
        Assert.Contains(children, e => e.Name.LocalName == "PathObject");
        Assert.Equal("PageBlock", children[^1].Name.LocalName);
        Assert.Equal(records[0].PageBlockId.ToString(), children[^1].Attribute("ID")!.Value);
    }

    [Fact]
    public void VirtualPage_FixedLayoutRegistersWithPageLoc()
    {
        var path = Path.Combine(OutDir(), "ah_vpage.ofd");
        using (var doc = new OFDDoc(path))
        {
            var ah = new AreaHolderBlock("n", 10, 20, 50, 8);
            ah.SetPosition(Position.Absolute);
            var vp = new VirtualPage();
            vp.Add(ah);
            doc.AddVPage(vp);
            doc.Close();
        }

        var records = ReadRecords(path);
        Assert.Single(records);
        Assert.Equal("n", records[0].Name);
        Assert.Equal(10, records[0].Boundary.TopLeftX, 3);
        Assert.Equal(20, records[0].Boundary.TopLeftY, 3);
        Assert.Equal(50, records[0].Boundary.Width, 3);
        Assert.Equal(8, records[0].Boundary.Height, 3);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", records[0].PageFile);

        var children = BodyLayerChildren(path);
        Assert.Equal("PageBlock", children[0].Name.LocalName);
        Assert.Equal(records[0].PageBlockId.ToString(), children[0].Attribute("ID")!.Value);
    }

    [Fact]
    public void AutoPagination_EachBlockRegisteredOnItsOwnPage()
    {
        var path = Path.Combine(OutDir(), "ah_paginate.ofd");
        using (var doc = new OFDDoc(path))
        {
            // 200mm 高块：第一块占 Page_0 首部；第二块放不下自动换页到 Page_1 顶部
            doc.Add(new AreaHolderBlock("p1", 180, 200));
            doc.Add(new AreaHolderBlock("p2", 180, 200));
            doc.Close();
        }

        var records = ReadRecords(path);
        Assert.Equal(2, records.Length);
        Assert.Equal("p1", records[0].Name);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", records[0].PageFile);
        Assert.Equal("p2", records[1].Name);
        Assert.Equal("/Doc_0/Pages/Page_1/Content.xml", records[1].PageFile);

        // 每页 Body 图层各含一个空 PageBlock，ID 与各自登记一致
        var page0 = BodyLayerChildren(path, "Page_0");
        var page1 = BodyLayerChildren(path, "Page_1");
        Assert.Equal(records[0].PageBlockId.ToString(), page0.Single(e => e.Name.LocalName == "PageBlock").Attribute("ID")!.Value);
        Assert.Equal(records[1].PageBlockId.ToString(), page1.Single(e => e.Name.LocalName == "PageBlock").Attribute("ID")!.Value);
    }
}
