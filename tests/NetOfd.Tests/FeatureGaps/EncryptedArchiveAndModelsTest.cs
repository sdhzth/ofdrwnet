using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicType;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Crypto;
using NetOfd.Archive.Convert;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.FeatureGaps;

/// <summary>
/// 跨模块集成：加密文档 → OFD-A 归档转换（需解密器）。
/// </summary>
public class EncryptedArchiveConvertTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_encarch_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private static string BuildSample()
    {
        var path = Out("enc_arch_src.ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("加密后归档转换测试").SetFontSize(5d));
        }
        return path;
    }

    [Fact]
    public void Convert_EncryptedWithoutDecryptor_ShouldExplainHowToFix()
    {
        var src = BuildSample();
        var protectedPath = Out("enc_arch_protected.ofd");
        using (var enc = new OfdEncryptor(src, protectedPath))
        {
            enc.AddUser(new UserPasswordEncryptor("归档员", "Pwd#123"));
            enc.Encrypt();
        }

        var dst = Out("enc_arch_out.ofd");
        var ex = Assert.Throws<InvalidOperationException>(() =>
        {
            var converter = new OFDArchiveConverter();
            converter.Convert(protectedPath, dst);
        });
        Assert.Contains("解密", ex.Message);

        // 提供解密器后成功，产物可读且文本保留
        var converter2 = new OFDArchiveConverter();
        converter2.AddDecryptor(new UserPasswordDecryptor("归档员", "Pwd#123"));
        converter2.Convert(protectedPath, dst);

        using var reader = new OFDReader(dst);
        Assert.Equal(1, reader.GetPageCount());
        var texts = ContentExtractor.ExtractAllPagesText(reader.GetPages().ToList());
        Assert.Contains(texts, t => t.Contains("加密后归档转换测试"));
    }

    [Fact]
    public void Convert_PlainDocument_ShouldSucceedDirectly()
    {
        var src = BuildSample();
        var dst = Out("plain_arch_out.ofd");
        var converter = new OFDArchiveConverter();
        converter.Convert(src, dst);
        using var reader = new OFDReader(dst);
        Assert.Equal(1, reader.GetPageCount());
    }
}

/// <summary>
/// 页面描述模型序列化往返（覆盖率缺口：CT_DrawParam/CT_Font/CTImage/CT_PageArea）。
/// </summary>
public class PageDescriptionModelRoundTripTest
{
    private static T Round<T>(T model) where T : OFDElement
        => (T)Activator.CreateInstance(typeof(T), new XElement(model.Element))!;

    [Fact]
    public void DrawParam_StyleFields_ShouldRoundtrip()
    {
        var dp = new CT_DrawParam { Id = "10" };
        dp.LineWidth = 0.53;
        dp.Join = LineJoinType.Round;
        dp.Cap = LineCapType.Round;

        var round = Round(dp);
        Assert.Equal("10", round.Id);
        Assert.Equal(0.53, round.LineWidth);
        Assert.Equal(LineJoinType.Round, round.Join);
        Assert.Equal(LineCapType.Round, round.Cap);
    }

    [Fact]
    public void Font_Fields_ShouldRoundtrip()
    {
        var font = new CT_Font { ID = new STID(3) };
        font.FontName = "SimSun";
        font.FamilyName = "宋体";

        var round = Round(font);
        Assert.Equal((long)3, round.ID?.Value);
        Assert.Equal("SimSun", round.FontName);
        Assert.Equal("宋体", round.FamilyName);
    }

    [Fact]
    public void Image_UnitFields_ShouldRoundtrip()
    {
        var img = new CTImage { ObjID = new STID(7), Alpha = 0.8 };
        img.Boundary = new STBox(10, 20, 100, 50);

        var round = Round(img);
        Assert.Equal((long)7, round.ObjID?.Value);
        Assert.Equal(0.8, round.Alpha);
        Assert.Equal(new STBox(10, 20, 100, 50), round.Boundary);
    }

    [Fact]
    public void PageArea_Boxes_ShouldRoundtrip()
    {
        var area = new CT_PageArea
        {
            PhysicalBox = new STBox(0, 0, 210, 297),
            ApplicationBox = new STBox(5, 5, 200, 287),
        };

        var round = Round(area);
        Assert.Equal(CT_PageArea.A4, round.PhysicalBox);
        Assert.Equal(new STBox(5, 5, 200, 287), round.ApplicationBox);
        Assert.Null(round.ContentBox);
    }
}
