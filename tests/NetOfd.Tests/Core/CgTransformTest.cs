using NetOfd.Core;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.PageDescription.Text;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 文本标准类型测试：字形变换 CT_CGTransform（§11.4.1 表 48）、
/// 方向 Direction（§11.3 表 47）、字重 Weight（§11.3 表 45）、
/// 字符集 Charset（§11.1 表 44），以及 CT_Text / CT_Font 上的接入点。
/// 对齐 Java: org.ofdrw.core.text.*
/// </summary>
public class CgTransformTest
{
    // ────────────────────────────────────────────────────────
    //  CT_CGTransform
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CGTransform_ElementName_IsCGTransform()
    {
        var cg = new CT_CGTransform();
        Assert.Equal("CGTransform", cg.Element.Name.LocalName);
    }

    [Fact]
    public void CGTransform_DefaultOptionalAttr_AreOne()
    {
        var cg = new CT_CGTransform { CodePosition = 0 };

        // CodeCount / GlyphCount 为可选属性，缺失时按规范取默认值 1
        Assert.Equal(1, cg.CodeCount);
        Assert.Equal(1, cg.GlyphCount);
        Assert.Null(cg.Glyphs);
        Assert.Equal(0, cg.CodePosition);
    }

    [Fact]
    public void CGTransform_CodePositionMissing_ReturnsNull()
    {
        // Java 版此处抛 IllegalArgumentException；netofd 放宽为 null 以便容错解析
        Assert.Null(new CT_CGTransform().CodePosition);
    }

    [Fact]
    public void CGTransform_FullAttr_RoundTrip()
    {
        var cg = new CT_CGTransform
        {
            CodePosition = 3,
            CodeCount = 2,
            GlyphCount = 2
        };
        cg.Glyphs = new STArray(new double[] { 17, 42 });

        Assert.Equal(3, cg.CodePosition);
        Assert.Equal(2, cg.CodeCount);
        Assert.Equal(2, cg.GlyphCount);
        Assert.NotNull(cg.Glyphs);
        Assert.Equal(2, cg.Glyphs!.Count);
        Assert.Equal(17d, cg.Glyphs[0]);
        Assert.Equal(42d, cg.Glyphs[1]);
    }

    [Fact]
    public void CGTransform_Glyphs_WrittenAsChildElement()
    {
        var cg = new CT_CGTransform { CodePosition = 0 };
        cg.Glyphs = new STArray(new double[] { 5, 6, 7 });

        var glyphs = cg.Element.Element(OFDElement.OfdNs + "Glyphs");
        Assert.NotNull(glyphs);
        Assert.Equal("5 6 7", glyphs!.Value.Trim());
    }

    [Fact]
    public void CGTransform_InvalidCounts_Throw()
    {
        var cg = new CT_CGTransform { CodePosition = 0 };
        Assert.Throws<System.ArgumentOutOfRangeException>(() => cg.CodeCount = 0);
        Assert.Throws<System.ArgumentOutOfRangeException>(() => cg.GlyphCount = -1);
    }

    [Fact]
    public void CGTransform_Create_SetsGlyphCountFromGlyphs()
    {
        var cg = CT_CGTransform.Create(2, new STArray(new double[] { 9, 8, 7 }));

        Assert.Equal(2, cg.CodePosition);
        Assert.Equal(1, cg.CodeCount);   // 未显式指定时默认 1
        Assert.Equal(3, cg.GlyphCount);  // 由 Glyphs 元素个数推导
        Assert.Equal(3, cg.Glyphs!.Count);
    }

    [Fact]
    public void CGTransform_ParseFromExistingXml()
    {
        var xml = new System.Xml.Linq.XElement(OFDElement.OfdNs + "CGTransform",
            new System.Xml.Linq.XAttribute("CodePosition", "5"),
            new System.Xml.Linq.XAttribute("CodeCount", "2"),
            new System.Xml.Linq.XAttribute("GlyphCount", "3"),
            new System.Xml.Linq.XElement(OFDElement.OfdNs + "Glyphs", "10 20 30"));

        var cg = new CT_CGTransform(xml);
        Assert.Equal(5, cg.CodePosition);
        Assert.Equal(2, cg.CodeCount);
        Assert.Equal(3, cg.GlyphCount);
        Assert.Equal(3, cg.Glyphs!.Count);
        Assert.Equal(30d, cg.Glyphs[2]);
    }

    // ────────────────────────────────────────────────────────
    //  Direction（表 47）
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Direction_Parse_AllowsOnlyFourAngles()
    {
        Assert.Equal(Direction.Angle0, DirectionUtil.Parse("0"));
        Assert.Equal(Direction.Angle90, DirectionUtil.Parse("90"));
        Assert.Equal(Direction.Angle180, DirectionUtil.Parse("180"));
        Assert.Equal(Direction.Angle270, DirectionUtil.Parse("270"));

        // 空值按默认 0 度处理
        Assert.Equal(Direction.Angle0, DirectionUtil.Parse(""));
        Assert.Equal(Direction.Angle0, DirectionUtil.Parse(null));
        Assert.Equal(Direction.Angle0, DirectionUtil.Parse("   "));
    }

    [Fact]
    public void Direction_Parse_IntegerOverload()
    {
        Assert.Equal(Direction.Angle90, DirectionUtil.Parse(90));
        Assert.Equal(Direction.Angle270, DirectionUtil.Parse(270));
    }

    [Fact]
    public void Direction_InvalidAngle_Throws()
    {
        Assert.Throws<System.FormatException>(() => DirectionUtil.Parse("45"));
        Assert.Throws<System.FormatException>(() => DirectionUtil.Parse("360"));
    }

    [Fact]
    public void Direction_ToXml_IsAngleNumber()
    {
        Assert.Equal("0", DirectionUtil.ToXml(Direction.Angle0));
        Assert.Equal("90", DirectionUtil.ToXml(Direction.Angle90));
        Assert.Equal("180", DirectionUtil.ToXml(Direction.Angle180));
        Assert.Equal("270", DirectionUtil.ToXml(Direction.Angle270));
        Assert.Equal(90, DirectionUtil.Angle(Direction.Angle90));
    }

    // ────────────────────────────────────────────────────────
    //  Weight（表 45）
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Weight_Parse_AllowsOnlyNineSteps()
    {
        for (int w = 100; w <= 900; w += 100)
            Assert.Equal((Weight)w, WeightUtil.Parse(w.ToString()));

        // 空值按默认 400 处理
        Assert.Equal(Weight.W400, WeightUtil.Parse(""));
        Assert.Equal(Weight.W400, WeightUtil.Parse(null));
    }

    [Fact]
    public void Weight_InvalidValue_Throws()
    {
        Assert.Throws<System.FormatException>(() => WeightUtil.Parse("150"));
        Assert.Throws<System.FormatException>(() => WeightUtil.Parse("1000"));
        Assert.Throws<System.FormatException>(() => WeightUtil.Parse("bold"));
    }

    [Fact]
    public void Weight_ToXml_IsWeightNumber()
    {
        Assert.Equal("400", WeightUtil.ToXml(Weight.W400));
        Assert.Equal("700", WeightUtil.ToXml(Weight.W700));
        Assert.Equal(700, WeightUtil.Value(Weight.W700));
    }

    // ────────────────────────────────────────────────────────
    //  Charset（表 44）
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Charset_Parse_IsCaseInsensitive()
    {
        Assert.Equal(Charset.Symbol, CharsetUtil.Parse("symbol"));
        Assert.Equal(Charset.Prc, CharsetUtil.Parse("PRC"));
        Assert.Equal(Charset.Big5, CharsetUtil.Parse("big5"));
        Assert.Equal(Charset.ShiftJis, CharsetUtil.Parse("shift-jis"));
        Assert.Equal(Charset.Wansung, CharsetUtil.Parse("wansung"));
        Assert.Equal(Charset.Johab, CharsetUtil.Parse("johab"));
        Assert.Equal(Charset.Unicode, CharsetUtil.Parse("unicode"));

        // 空值按默认 unicode 处理
        Assert.Equal(Charset.Unicode, CharsetUtil.Parse(""));
        Assert.Equal(Charset.Unicode, CharsetUtil.Parse(null));
    }

    [Fact]
    public void Charset_ShiftJis_UsesHyphenInXml()
    {
        // 枚举名为 ShiftJis，但 XML 中必须写作 shift-jis
        Assert.Equal("shift-jis", CharsetUtil.ToXml(Charset.ShiftJis));
        Assert.Equal("unicode", CharsetUtil.ToXml(Charset.Unicode));
        Assert.Equal("prc", CharsetUtil.ToXml(Charset.Prc));
    }

    [Fact]
    public void Charset_UnknownValue_Throws()
    {
        Assert.Throws<System.FormatException>(() => CharsetUtil.Parse("utf-8"));
    }

    // ────────────────────────────────────────────────────────
    //  CT_Text 接入点
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CTText_AddAndGetCGTransforms()
    {
        var text = new CTText();
        text.AddCGTransform(CT_CGTransform.Create(0, new STArray(new double[] { 11 })));
        text.AddCGTransform(CT_CGTransform.Create(1, new STArray(new double[] { 22 })));

        var list = text.GetCGTransforms();
        Assert.Equal(2, list.Count);
        Assert.Equal(0, list[0].CodePosition);
        Assert.Equal(1, list[1].CodePosition);
        Assert.Equal(11d, list[0].Glyphs![0]);
        Assert.Equal(22d, list[1].Glyphs![0]);
    }

    [Fact]
    public void CTText_ClearCGTransforms_RemovesAll()
    {
        var text = new CTText();
        text.AddCGTransform(CT_CGTransform.Create(0, new STArray(new double[] { 1, 2 })));
        Assert.Single(text.GetCGTransforms());

        text.ClearCGTransforms();
        Assert.Empty(text.GetCGTransforms());
    }

    [Fact]
    public void CTText_CGTransform_DoesNotBreakTextCodes()
    {
        var text = new CTText();
        var code = new TextCode { X = 1, Y = 2, Text = "甲" };
        text.AddTextCode(code);
        text.AddCGTransform(CT_CGTransform.Create(0, new STArray(new double[] { 3 })));

        Assert.Single(text.GetTextCodes());
        Assert.Single(text.GetCGTransforms());
        Assert.Equal("甲", text.GetTextCodes()[0].Text);
    }

    [Fact]
    public void CTText_StrongTypedDirectionAndWeight()
    {
        var text = new CTText();

        text.ReadDirectionAngle = Direction.Angle90;
        text.CharDirectionAngle = Direction.Angle270;
        text.WeightKind = Weight.W700;

        // 强类型视图与数值视图应保持一致
        Assert.Equal(90, text.ReadDirection);
        Assert.Equal(270, text.CharDirection);
        Assert.Equal(700, text.Weight);

        Assert.Equal(Direction.Angle90, text.ReadDirectionAngle);
        Assert.Equal(Direction.Angle270, text.CharDirectionAngle);
        Assert.Equal(Weight.W700, text.WeightKind);
    }

    [Fact]
    public void CTText_WeightKind_DefaultsTo400()
    {
        Assert.Equal(Weight.W400, new CTText().WeightKind);
    }

    // ────────────────────────────────────────────────────────
    //  CT_Font 接入点
    // ────────────────────────────────────────────────────────

    [Fact]
    public void CT_Font_Charset_StrongTypedRoundTrip()
    {
        var font = new CT_Font { ID = new STID(1), FontName = "宋体" };

        // 未设置时为 null（不写出 Charset 属性）
        Assert.Null(font.Charset);
        Assert.Null(font.Element.Attribute("Charset"));

        font.Charset = Charset.Prc;
        Assert.Equal("prc", font.Element.Attribute("Charset")!.Value);
        Assert.Equal(Charset.Prc, font.Charset);

        font.Charset = Charset.ShiftJis;
        Assert.Equal("shift-jis", font.Element.Attribute("Charset")!.Value);

        // 置回 null 应移除属性
        font.Charset = null;
        Assert.Null(font.Element.Attribute("Charset"));
    }

    [Fact]
    public void CT_Font_Charset_ParsedFromXml()
    {
        var xml = new System.Xml.Linq.XElement(OFDElement.OfdNs + "Font",
            new System.Xml.Linq.XAttribute("ID", "7"),
            new System.Xml.Linq.XAttribute("FontName", "KaiTi"),
            new System.Xml.Linq.XAttribute("Charset", "big5"));

        var font = new CT_Font(xml);
        Assert.Equal(Charset.Big5, font.Charset);
        Assert.Equal("KaiTi", font.FontName);
    }
}
