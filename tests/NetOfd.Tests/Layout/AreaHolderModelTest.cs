using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout.Areaholder;
using NetOfd.Layout.Element;
using NetOfd.Pkg.Container;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// areaholder 区域占位子系统模型层测试——对齐 Java ofdrw-layout areaholder 包：
/// <c>element.AreaHolderBlock</c> / <c>areaholder.CT_AreaHolderBlock</c> /
/// <c>areaholder.AreaHolderBlocks</c> / <c>areaholder.AreaHolderBlocksProcess</c>。
/// <para>Java 锚点：CT_AreaHolderBlockTest.testMarshall（属性 AreaName/PageBlockID/Boundary
/// + PageFile 子元素）；AreaHolderBlockTest.testStreamDocAddArea（(areaName,w,h) 构造流式加占位）；
/// 官方样本 areaholder_fields.ofd（Doc_0/AreaHolderBlocks.xml，2 条 name/birthday 记录，
/// ofd 命名空间与 NetOfd OfdNs 一致）做读取侧锚定。</para>
/// <para>边界：AreaHolderContext.get（依赖 ResourceLocator/CT_PageBlock/AdditionVPage 编辑引擎）
/// 因 NetOfd Layout 不依赖 Reader 且无编辑引擎，判为依赖方向阻碍项未移植（见 skill 大缺口登记）。</para>
/// </summary>
public class AreaHolderModelTest
{
    private const string JavaLayoutRes = "E:/电子病历/ofdrw-master/ofdrw-layout/src/test/resources";
    private const string AreaFieldsOfd = "areaholder_fields.ofd";

    private static bool Has(string name) => File.Exists(Path.Combine(JavaLayoutRes, name));

    private static void SkipIfMissing(string name)
    {
        if (!Has(name)) Assert.True(true, $"Java 测试资源 {name} 不存在，跳过。");
    }

    // ── CT_AreaHolderBlock：setter/属性/子元素 ──

    [Fact]
    public void CtBlock_ChainedSetters_ShouldWriteAttrsAndChild()
    {
        var block = new CT_AreaHolderBlock("UserName")
            .SetBoundary(new STBox(10, 10, 100, 100))
            .SetPageBlockID(new STRefID(233))
            .SetPageFile(new STLoc("/Doc_0/Pages/Page_0/Content.xml"));

        Assert.Equal("UserName", block.GetAreaName());
        Assert.Equal(new STBox(10, 10, 100, 100), block.GetBoundary());
        Assert.Equal(233L, block.GetPageBlockID()?.Value);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", block.GetPageFile()?.ToString());
        // Java 别名读法（getFontFile 实读 PageFile）
        Assert.Equal(block.GetPageFile(), block.GetFontFile());

        // XML 形态：3 属性 + 1 子元素（Java testMarshall 结构）
        Assert.Equal("AreaHolderBlock", block.Element.Name.LocalName);
        Assert.Equal("UserName", block.Element.Attribute("AreaName")?.Value);
        Assert.Equal("233", block.Element.Attribute("PageBlockID")?.Value);
        Assert.Equal("10 10 100 100", block.Element.Attribute("Boundary")?.Value);
        var pageFile = block.Element.Elements().Single();
        Assert.Equal("PageFile", pageFile.Name.LocalName);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", pageFile.Value);
    }

    [Fact]
    public void CtBlock_AreaNameCtor_ShouldPreset()
    {
        var block = new CT_AreaHolderBlock("area-1");
        Assert.Equal("area-1", block.GetAreaName());
    }

    [Fact]
    public void CtBlock_NullAreaName_ShouldThrow()
    {
        // Java IllegalArgumentException「区域名称（AreaName）不能为空」——仅 null 抛，空串 Java 允许
        _ = Assert.Throws<ArgumentException>(() => new CT_AreaHolderBlock("x").SetAreaName(null!));
        Assert.Equal("", new CT_AreaHolderBlock("x").SetAreaName("").GetAreaName());
    }

    [Fact]
    public void CtBlock_NullBoundary_ShouldThrow()
    {
        // Java IllegalArgumentException「区域外接矩形（boundary）为空」
        _ = Assert.Throws<ArgumentException>(() => new CT_AreaHolderBlock("x").SetBoundary(null));
    }

    [Fact]
    public void CtBlock_NullPageBlockId_ShouldThrow()
    {
        // Java IllegalArgumentException「页块引用（pageBlockID）不能为空」
        _ = Assert.Throws<ArgumentException>(() => new CT_AreaHolderBlock("x").SetPageBlockID(null));
    }

    [Fact]
    public void CtBlock_NullOrEmptyPageFile_ShouldThrow()
    {
        // Java IllegalArgumentException「页面对象路径（pageLoc）为空」
        _ = Assert.Throws<ArgumentException>(() => new CT_AreaHolderBlock("x").SetPageFile(null));
        _ = Assert.Throws<ArgumentException>(() => new CT_AreaHolderBlock("x").SetPageFile(new STLoc("")));
    }

    [Fact]
    public void CtBlock_SetFontFile_AliasWritesPageFile()
    {
        // Java 命名误导忠实移植：setFontFile(String) 实为写 PageFile 子元素
        var block = new CT_AreaHolderBlock("x").SetFontFile("/Doc_0/Pages/Page_1/Content.xml");
        Assert.Equal("/Doc_0/Pages/Page_1/Content.xml", block.GetPageFile()?.ToString());
        Assert.Equal(block.GetPageFile(), block.GetFontFile());
        Assert.Null(block.GetPageBlockID()); // 未设置属性 → null
        Assert.Null(block.GetBoundary());
    }

    [Fact]
    public void CtBlock_WrapFromXml_RoundTrip()
    {
        var original = new CT_AreaHolderBlock("UserName")
            .SetBoundary(new STBox(10.5, 20.25, 100, 10))
            .SetPageBlockID(new STRefID(7))
            .SetPageFile(new STLoc("/Doc_0/Pages/Page_0/Content.xml"));

        var reparsed = new CT_AreaHolderBlock(XElement.Parse(original.ToString()));

        Assert.Equal("UserName", reparsed.GetAreaName());
        Assert.Equal(original.GetBoundary(), reparsed.GetBoundary());
        Assert.Equal(7L, reparsed.GetPageBlockID()?.Value);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", reparsed.GetPageFile()?.ToString());
    }

    // ── AreaHolderBlocks：容器模型 ──

    [Fact]
    public void Blocks_AddGet_MultipleInOrder()
    {
        var blocks = new AreaHolderBlocks();
        blocks.AddAreaHolderBlock(
            new CT_AreaHolderBlock("name"),
            new CT_AreaHolderBlock("birthday"));
        blocks.AddAreaHolderBlock(new CT_AreaHolderBlock("addr"));

        var list = blocks.GetAreaHolderBlocks();
        Assert.Equal(3, list.Count);
        Assert.Equal(new[] { "name", "birthday", "addr" }, list.Select(b => b.GetAreaName()));
        Assert.Equal("AreaHolderBlocks", blocks.Element.Name.LocalName);
        Assert.Equal(AreaHolderBlocks.AreaHolderBlocksFile, "AreaHolderBlocks.xml");
    }

    [Fact]
    public void Blocks_AddNullOrEmpty_ShouldNoop()
    {
        var blocks = new AreaHolderBlocks();
        Assert.Same(blocks, blocks.AddAreaHolderBlock(null));
        Assert.Same(blocks, blocks.AddAreaHolderBlock());
        Assert.Empty(blocks.GetAreaHolderBlocks());
    }

    // ── AreaHolderBlocksProcess：Doc 目录文件生命周期 ──

    [Fact]
    public void Process_CreateGetFindDelete_Lifecycle()
    {
        using var ofdDir = OFDDir.NewOFD();
        DocDir docDir = ofdDir.ObtainDocDir();

        Assert.False(AreaHolderBlocksProcess.Exists(docDir));

        AreaHolderBlocks created = AreaHolderBlocksProcess.Create(docDir);
        Assert.True(AreaHolderBlocksProcess.Exists(docDir));

        // 装配：向容器模型添加记录后回写（Java 引擎装配同型：改对象后 putObj 持久化）
        created.AddAreaHolderBlock(new CT_AreaHolderBlock("name")
            .SetBoundary(new STBox(52.406, 25.4, 100, 10))
            .SetPageBlockID(new STRefID(10))
            .SetPageFile(new STLoc("/Doc_0/Pages/Page_0/Content.xml")));
        docDir.PutFile(AreaHolderBlocks.AreaHolderBlocksFile, created.Element);

        // Get 回读（缓存同源，包装新对象、底层 XElement 同实例）
        AreaHolderBlocks read = AreaHolderBlocksProcess.Get(docDir);
        Assert.Single(read.GetAreaHolderBlocks());
        Assert.Equal("name", read.GetAreaHolderBlocks()[0].GetAreaName());

        // Find 命中 / 未命中
        Assert.NotNull(AreaHolderBlocksProcess.Find(read, "name"));
        Assert.Null(AreaHolderBlocksProcess.Find(read, "nope"));
        Assert.Null(AreaHolderBlocksProcess.Find(null, "name"));
        Assert.Null(AreaHolderBlocksProcess.Find(read, null));

        // Delete 后消失
        AreaHolderBlocksProcess.Delete(docDir);
        Assert.False(AreaHolderBlocksProcess.Exists(docDir));
        Assert.Null(docDir.GetFile(AreaHolderBlocks.AreaHolderBlocksFile));
    }

    [Fact]
    public void Process_Obtain_FirstCreateThenReuse()
    {
        using var ofdDir = OFDDir.NewOFD();
        DocDir docDir = ofdDir.ObtainDocDir();

        AreaHolderBlocks first = AreaHolderBlocksProcess.Obtain(docDir);
        Assert.True(AreaHolderBlocksProcess.Exists(docDir));

        first.AddAreaHolderBlock(new CT_AreaHolderBlock("a1"));
        docDir.PutFile(AreaHolderBlocks.AreaHolderBlocksFile, first.Element);

        // 已存在 → 第二次 Obtain 走 Get 读回而非重建
        AreaHolderBlocks second = AreaHolderBlocksProcess.Obtain(docDir);
        Assert.Single(second.GetAreaHolderBlocks());
        Assert.Equal("a1", second.GetAreaHolderBlocks()[0].GetAreaName());

        // Java 静默语义：null docDir
        Assert.False(AreaHolderBlocksProcess.Exists(null));
        Assert.False(AreaHolderBlocksProcess.Exists(null!));
        AreaHolderBlocksProcess.Delete(null);
    }

    [Fact]
    public void Process_NullDocDir_ShouldThrow()
    {
        _ = Assert.Throws<ArgumentException>(() => AreaHolderBlocksProcess.Create(null));
        _ = Assert.Throws<ArgumentException>(() => AreaHolderBlocksProcess.Obtain(null));
    }

    // ── element.AreaHolderBlock：Div 占位元素 ──

    [Fact]
    public void Element_WidthHeightCtor_ShouldSetSize()
    {
        var field = new AreaHolderBlock("name", 100d, 10d);
        Assert.Equal("name", field.GetAreaName());
        Assert.Equal(100d, field.Width);
        Assert.Equal(10d, field.Height);
        Assert.Null(field.X);
        Assert.Null(field.Y);
    }

    [Fact]
    public void Element_XywhCtor_ShouldSetPositionAndSize()
    {
        var field = new AreaHolderBlock("addr", 5d, 6d, 100d, 10d);
        Assert.Equal("addr", field.GetAreaName());
        Assert.Equal(5d, field.X);
        Assert.Equal(6d, field.Y);
        Assert.Equal(100d, field.Width);
        Assert.Equal(10d, field.Height);
    }

    [Fact]
    public void Element_ElementTypeAndChain_ShouldMatchJava()
    {
        var field = new AreaHolderBlock("name", 100d, 10d);

        // Java elementType() = "AreaHolderBlock"
        Assert.Equal("AreaHolderBlock", field.ElementType);

        // SetAreaName 链式返回自身（Java 泛型 this）
        AreaHolderBlock chained = field.SetAreaName("renamed");
        Assert.Same(field, chained);
        Assert.Equal("renamed", field.GetAreaName());

        // 继承 Div 默认值/链式能力
        Assert.Equal(Clear.Both, field.Clear);
        field.SetBorder(0.353).SetBorderColor(CT_Color.Black);
        Assert.Equal(0.353, field.BorderTop);
        Assert.NotNull(field.BorderColor);
    }

    // ── Java 官方样本读取侧锚定 ──

    [Fact]
    public void JavaSample_AreaHolderFields_ShouldParseToModel()
    {
        if (!Has(AreaFieldsOfd)) { SkipIfMissing(AreaFieldsOfd); return; }
        using var reader = new OFDReader(Path.Combine(JavaLayoutRes, AreaFieldsOfd));

        // 直接读取 Doc_0/AreaHolderBlocks.xml 并用模型包装（Java 引擎装配产物）
        var xml = reader.GetOfdDir().GetDocByIndex(0).GetFile(AreaHolderBlocks.AreaHolderBlocksFile);
        Assert.NotNull(xml);
        var blocks = new AreaHolderBlocks(xml!);

        var list = blocks.GetAreaHolderBlocks();
        Assert.Equal(2, list.Count);

        var name = list[0];
        Assert.Equal("name", name.GetAreaName());
        Assert.Equal(10L, name.GetPageBlockID()?.Value);
        var boundary = name.GetBoundary();
        Assert.NotNull(boundary);
        Assert.Equal(52.406, boundary!.TopLeftX, 3);
        Assert.Equal(25.4, boundary.TopLeftY, 3);
        Assert.Equal(100d, boundary.Width);
        Assert.Equal(10d, boundary.Height);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", name.GetPageFile()?.ToString());

        Assert.Equal("birthday", list[1].GetAreaName());
        Assert.Equal(21L, list[1].GetPageBlockID()?.Value);
    }
}
