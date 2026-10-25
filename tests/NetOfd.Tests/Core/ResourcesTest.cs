using System.Linq;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicType;
using NetOfd.Core.CompositeObj;
using NetOfd.Core.PageDescription.Color.colorSpace;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.Resources;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 资源（Res）子系统测试：OFDResource 工厂分发 + 五类资源集合 + MediaType +
/// 复合对象（CT_Composite / CT_VectorG / Content / CT_PageBlock）。
/// 对齐 Java: org.ofdrw.core.basicStructure.res、org.ofdrw.core.compositeObj
/// （GB/T 33190 7.9 图 20-21、13 章 图 71-72）。
/// </summary>
public class ResourcesTest
{
    private static XElement RoundTrip(OFDElement e) => new(e.Element);

    // ────────────────────────────────────────────────────────
    //  MediaType
    // ────────────────────────────────────────────────────────

    [Fact]
    public void MediaType_Parse_IsCaseInsensitive()
    {
        Assert.Equal(MediaType.Image, MediaTypeParser.Parse("Image"));
        Assert.Equal(MediaType.Audio, MediaTypeParser.Parse("audio"));
        Assert.Equal(MediaType.Video, MediaTypeParser.Parse("VIDEO"));
        Assert.Throws<System.ArgumentException>(() => MediaTypeParser.Parse("GIF"));
        Assert.Throws<System.ArgumentException>(() => MediaTypeParser.Parse(null));
    }

    [Fact]
    public void MediaType_TryParse_DoesNotThrow()
    {
        Assert.True(MediaTypeParser.TryParse("image", out var mt));
        Assert.Equal(MediaType.Image, mt);
        Assert.False(MediaTypeParser.TryParse("bmp", out _));
    }

    // ────────────────────────────────────────────────────────
    //  OFDResource 工厂
    // ────────────────────────────────────────────────────────

    [Fact]
    public void OFDResource_GetInstance_DispatchesByElementName()
    {
        Assert.IsType<ColorSpaces>(OFDResource.GetInstance(new XElement("ColorSpaces")));
        Assert.IsType<DrawParams>(OFDResource.GetInstance(new XElement("DrawParams")));
        Assert.IsType<Fonts>(OFDResource.GetInstance(new XElement("Fonts")));
        Assert.IsType<MultiMedias>(OFDResource.GetInstance(new XElement("MultiMedias")));
        Assert.IsType<CompositeGraphicUnits>(OFDResource.GetInstance(new XElement("CompositeGraphicUnits")));
    }

    [Fact]
    public void OFDResource_GetInstance_FallsBackByKeyword()
    {
        // 名称含关键字时回退到对应类型（Java 同名逻辑）
        Assert.IsType<DrawParams>(OFDResource.GetInstance(new XElement("MyDrawSet")));
        Assert.IsType<Fonts>(OFDResource.GetInstance(new XElement("MyFontSet")));
        Assert.IsType<ColorSpaces>(OFDResource.GetInstance(new XElement("MyColorSet")));
    }

    [Fact]
    public void OFDResource_GetInstance_UnknownThrows()
    {
        Assert.Throws<System.ArgumentException>(() => OFDResource.GetInstance(new XElement("Whatever")));
        Assert.Null(OFDResource.GetInstance(null!));
    }

    // ────────────────────────────────────────────────────────
    //  资源集合往返
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Fonts_RoundTrip_RequiresID()
    {
        var fonts = new Fonts();
        var font = new CT_Font { ID = new STID(7), FontName = "SimSun" };
        fonts.AddFont(font);

        var rt = new Fonts(RoundTrip(fonts));
        var list = rt.GetFonts();
        Assert.Single(list);
        Assert.Equal(7, list[0].ID.Value);
        Assert.Equal("SimSun", list[0].FontName);

        Assert.Throws<System.ArgumentException>(() => new Fonts().AddFont(new CT_Font()));
        Assert.Empty(new Fonts().AddFont(null!).GetFonts());
    }

    [Fact]
    public void ColorSpaces_RoundTrip_RequiresID()
    {
        var cs = new ColorSpaces();
        var space = new CT_ColorSpace(OFDColorSpaceType.RGB);
        space.SetObjID(new STID(3));
        cs.AddColorSpace(space);

        var rt = new ColorSpaces(RoundTrip(cs));
        Assert.Single(rt.GetColorSpaces());
        Assert.Equal(3, rt.GetColorSpaces()[0].ID!.Value);

        Assert.Throws<System.ArgumentException>(() => new ColorSpaces().AddColorSpace(new CT_ColorSpace()));
    }

    [Fact]
    public void DrawParams_RoundTrip_RequiresID()
    {
        var dps = new DrawParams();
        var dp = new CT_DrawParam { Id = "11" };
        dps.AddDrawParam(dp);

        var rt = new DrawParams(RoundTrip(dps));
        Assert.Single(rt.GetDrawParams());
        Assert.Equal("11", rt.GetDrawParams()[0].Id);

        Assert.Throws<System.ArgumentException>(() => new DrawParams().AddDrawParam(new CT_DrawParam()));
    }

    [Fact]
    public void MultiMedias_RoundTrip_RequiresID()
    {
        var mms = new MultiMedias();
        var mm = new CT_MultiMedia { Type = "Image", Format = "PNG" };
        mm.SetObjID(new STID(21));
        mms.AddMultiMedia(mm);

        var rt = new MultiMedias(RoundTrip(mms));
        var list = rt.GetMultiMedias();
        Assert.Single(list);
        Assert.Equal("Image", list[0].Type);
        Assert.Equal("PNG", list[0].Format);

        Assert.Throws<System.ArgumentException>(() => new MultiMedias().AddMultiMedia(new CT_MultiMedia()));
    }

    [Fact]
    public void CompositeGraphicUnits_RenamesElementToCompositeGraphicUnit()
    {
        var units = new CompositeGraphicUnits();
        var vg = new CT_VectorG { Width = 100, Height = 50 };
        vg.SetObjID(new STID(9));
        units.AddCompositeGraphicUnit(vg);

        // 加入集合后元素名被重写为 CompositeGraphicUnit
        Assert.Equal("CompositeGraphicUnit", units.Element.Elements().Single().Name.LocalName);

        var rt = new CompositeGraphicUnits(RoundTrip(units));
        var list = rt.GetCompositeGraphicUnits();
        Assert.Single(list);
        Assert.Equal(100, list[0].Width);
        Assert.Equal(50, list[0].Height);

        Assert.Throws<System.ArgumentException>(() =>
            new CompositeGraphicUnits().AddCompositeGraphicUnit(new CT_VectorG()));
    }

    // ────────────────────────────────────────────────────────
    //  CT_VectorG / Content / CT_PageBlock
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CT_VectorG_RoundTrip()
    {
        var vg = new CT_VectorG { Width = 210, Height = 297 };
        vg.SetObjID(new STID(5));
        vg.Thumbnail = new STRefID(11);
        vg.Substitution = new STRefID(12);

        var content = new Content();
        content.AddPageBlock(new CT_PageBlock());
        vg.SetContent(content);

        var rt = new CT_VectorG(RoundTrip(vg));
        Assert.Equal(5, rt.ID!.Value);
        Assert.Equal(210, rt.Width);
        Assert.Equal(297, rt.Height);
        Assert.Equal(new STRefID(11), rt.Thumbnail);
        Assert.Equal(new STRefID(12), rt.Substitution);
        Assert.Single(rt.GetContent().GetPageBlocks());
    }

    [Fact]
    public void CT_VectorG_AddContent_CreatesContentWhenMissing()
    {
        var vg = new CT_VectorG();
        vg.AddContent(new CT_PageBlock()).AddContent(new CT_PageBlock());
        Assert.Equal(2, vg.GetContent().GetPageBlocks().Count);
    }

    [Fact]
    public void CT_VectorG_MissingSizeOrContent_Throws()
    {
        var vg = new CT_VectorG();
        Assert.Throws<System.InvalidOperationException>(() => _ = vg.Width);
        Assert.Throws<System.InvalidOperationException>(() => _ = vg.Height);
        Assert.Throws<System.InvalidOperationException>(() => vg.GetContent());
    }

    [Fact]
    public void CT_PageBlock_NestedRoundTrip()
    {
        var root = new CT_PageBlock();
        var inner = new CT_PageBlock();
        inner.AddPageBlock(new Content());
        root.AddPageBlock(inner).AddPageBlock(new Content());

        var rt = new CT_PageBlock(RoundTrip(root));
        var blocks = rt.GetPageBlocks();
        Assert.Equal(2, blocks.Count);
        // 内层保留原标签名
        Assert.Equal("PageBlock", blocks[0].Element.Name.LocalName);
        Assert.Equal("Content", blocks[1].Element.Name.LocalName);
    }

    // ────────────────────────────────────────────────────────
    //  CT_Composite
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CT_Composite_ResourceID_RoundTrip()
    {
        var composite = new CT_Composite { ResourceID = new STRefID(88) };
        var rt = new CT_Composite(RoundTrip(composite));
        Assert.Equal(new STRefID(88), rt.ResourceID);
        Assert.Equal("Composite", rt.Element.Name.LocalName);

        Assert.Throws<System.ArgumentException>(() => new CT_Composite { ResourceID = null });
    }

    // ────────────────────────────────────────────────────────
    //  Res 资源集合 API
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Res_AddResource_GetResources_RoundTrip()
    {
        var fonts = new Fonts();
        var font = new CT_Font { ID = new STID(1), FontName = "KaiTi" };
        fonts.AddFont(font);

        var mms = new MultiMedias();
        var mm = new CT_MultiMedia { Type = "Image" };
        mm.SetObjID(new STID(2));
        mms.AddMultiMedia(mm);

        var res = new Res { BaseLoc = new STLoc("./Res") };
        res.AddResource(fonts).AddResource(mms);

        var rt = new Res(RoundTrip(res));
        var list = rt.GetResources();
        Assert.Equal(2, list.Count);
        Assert.IsType<Fonts>(list[0]);
        Assert.IsType<MultiMedias>(list[1]);
        Assert.Equal("./Res", rt.BaseLoc!.Value);
    }

    [Fact]
    public void Res_Empty_GetResources_ReturnsEmpty()
    {
        Assert.Empty(new Res().GetResources());
    }
}
