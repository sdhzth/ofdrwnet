using System.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.Doc.VPreferences;
using NetOfd.Core.BasicStructure.Doc.VPreferences.Zoom;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.Image;
using NetOfd.Core.PageDescription.Color;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 视图首选项（VPreferences）测试：窗口模式 / 页面布局 / 标题栏 / 隐藏标志 / Zoom 与 ZoomMode 互斥。
/// 对齐 Java: org.ofdrw.core.basicStructure.doc.vpreferences（GB/T 33190 7.5 表 9）。
/// </summary>
public class VPreferencesTest
{
    [Fact]
    public void PageMode_Absent_DefaultsToNone()
        => Assert.Equal(PageMode.None, new CT_VPreferences().PageMode);

    [Fact]
    public void PageMode_RoundTrip()
    {
        var vp = new CT_VPreferences { PageMode = PageMode.UseThumbs };
        Assert.Equal(PageMode.UseThumbs, vp.PageMode);
        Assert.Contains("UseThumbs", vp.ToString());
    }

    [Fact]
    public void PageMode_ParseIsCaseInsensitive_AndUnknownThrows()
    {
        Assert.Equal(PageMode.FullScreen, "fullscreen".ToPageMode());
        Assert.Equal(PageMode.None, "".ToPageMode());
        Assert.Equal(PageMode.None, ((string?)null).ToPageMode());
        Assert.Throws<System.ArgumentException>(() => "NoSuchMode".ToPageMode());
    }

    [Fact]
    public void PageLayout_EmptyDefaultsToOnePage_AndRoundTrip()
    {
        Assert.Equal(PageLayout.OnePage, "".ToPageLayout());
        var vp = new CT_VPreferences { PageLayout = PageLayout.TwoColumnR };
        Assert.Equal(PageLayout.TwoColumnR, vp.PageLayout);
    }

    [Fact]
    public void TabDisplay_EmptyDefaultsToFileName_AndRoundTrip()
    {
        Assert.Equal(TabDisplay.FileName, ((string?)null).ToTabDisplay());
        var vp = new CT_VPreferences { TabDisplay = TabDisplay.DocTitle };
        Assert.Equal(TabDisplay.DocTitle, vp.TabDisplay);
        Assert.Equal(TabDisplay.FileName, new CT_VPreferences().TabDisplay);
    }

    [Fact]
    public void HideFlags_RoundTrip()
    {
        var vp = new CT_VPreferences { HideToolbar = true, HideMenubar = true, HideWindowUI = false };
        Assert.True(vp.HideToolbar);
        Assert.True(vp.HideMenubar);
        Assert.False(vp.HideWindowUI);
        Assert.False(new CT_VPreferences().HideToolbar);
    }

    [Fact]
    public void SetZoomScale_WritesZoomNode()
    {
        var vp = new CT_VPreferences().SetZoomScale(1.5);
        var scale = vp.GetZoomScale();
        var zoom = Assert.IsType<Zoom>(scale);
        Assert.Equal(1.5, zoom.Value, 6);
        Assert.Null(vp.ZoomModeValue);
    }

    [Fact]
    public void SetZoomMode_WritesZoomModeNode()
    {
        var vp = new CT_VPreferences().SetZoomMode(ZoomModeType.FitWidth);
        Assert.Equal(ZoomModeType.FitWidth, vp.ZoomModeValue);
        var mode = Assert.IsType<ZoomMode>(vp.GetZoomScale());
        Assert.Equal(ZoomModeType.FitWidth, mode.ModeType);
    }

    [Fact]
    public void Zoom_And_ZoomMode_AreMutuallyExclusive()
    {
        var vp = new CT_VPreferences().SetZoomScale(2.0);
        Assert.NotNull(vp.Element.Element(OFDElement.OfdNs + "Zoom"));

        vp.SetZoomMode(ZoomModeType.FitHeight);
        Assert.Null(vp.Element.Element(OFDElement.OfdNs + "Zoom"));
        Assert.NotNull(vp.Element.Element(OFDElement.OfdNs + "ZoomMode"));

        vp.SetZoomScale(0.8);
        Assert.Null(vp.Element.Element(OFDElement.OfdNs + "ZoomMode"));
        Assert.NotNull(vp.Element.Element(OFDElement.OfdNs + "Zoom"));
    }
}

/// <summary>
/// 文档元数据（DocInfo）子结构测试：DocUsage / Cover / Keywords / CustomDatas / DocID。
/// 对齐 Java: org.ofdrw.core.basicStructure.ofd.docInfo（GB/T 33190 表 4）。
/// </summary>
public class DocInfoTest
{
    [Fact]
    public void DocUsage_MissingOrUnknown_FallsBackToNormal()
    {
        Assert.Equal(DocUsage.Normal, new DocInfo().DocUsage);
        Assert.Equal(DocUsage.Normal, "SomeJunk".ToDocUsage());
        Assert.Equal(DocUsage.EBook, "ebook".ToDocUsage());
    }

    [Fact]
    public void DocUsage_RoundTrip()
    {
        var info = new DocInfo { DocUsage = DocUsage.EMagzine };
        Assert.Equal(DocUsage.EMagzine, info.DocUsage);
    }

    [Fact]
    public void Cover_RoundTrip()
    {
        var info = new DocInfo { Cover = "/Doc_0/Res/cover.png" };
        Assert.Equal("/Doc_0/Res/cover.png", info.Cover!.Value);
        Assert.Null(new DocInfo().Cover);
    }

    [Fact]
    public void Keywords_AddAndReadBack()
    {
        var info = new DocInfo().AddKeyword("发票").AddKeyword("电子病历");
        var kws = info.GetKeywords();
        Assert.NotNull(kws);
        Assert.Equal(new[] { "发票", "电子病历" }, kws!.GetKeywords());
    }

    [Fact]
    public void CustomDatas_AddAndLookupByName()
    {
        var info = new DocInfo().AddCustomData("Hospital", "即墨三院").AddCustomData("Dept", "信息科");
        var cds = info.GetCustomDatas();
        Assert.NotNull(cds);
        Assert.Equal(2, cds!.GetCustomDatas().Count);
        Assert.Equal("即墨三院", cds.GetCustomDataValue("Hospital"));
        Assert.Equal("信息科", cds.GetCustomDataValue("Dept"));
        Assert.Null(cds.GetCustomDataValue("Nope"));
    }

    [Fact]
    public void CustomData_ElementHasNameAttributeAndText()
    {
        var cd = new CustomData("K", "V");
        Assert.Equal("K", cd.DataName);
        Assert.Equal("V", cd.Value);
        Assert.Equal("CustomData", cd.Element.Name.LocalName);
    }

    [Fact]
    public void RandomDocID_Produces32HexChars()
    {
        var info = new DocInfo().RandomDocID();
        var id = info.DocID;
        Assert.False(string.IsNullOrEmpty(id));
        Assert.Equal(32, id!.Length);
        Assert.DoesNotContain("-", id);
    }
}

/// <summary>
/// 图像边框（Border）测试：默认线宽 0.353mm、圆角/虚线属性往返、BorderColor 元素名改写。
/// 对齐 Java: org.ofdrw.core.image.Border（GB/T 33190 10 表 43）。
/// </summary>
public class BorderTest
{
    [Fact]
    public void Defaults_MatchStandard()
    {
        var b = new Border();
        Assert.Equal(0.353d, b.LineWidth, 6);
        Assert.Equal(0d, b.HorizonalCornerRadius, 6);
        Assert.Equal(0d, b.VerticalCornerRadius, 6);
        Assert.Equal(0d, b.DashOffset, 6);
        Assert.Null(b.DashPattern);
        Assert.Null(b.GetBorderColor());
    }

    [Fact]
    public void Attributes_RoundTrip()
    {
        var b = new Border
        {
            LineWidth = 1.2,
            HorizonalCornerRadius = 3,
            VerticalCornerRadius = 4.5,
            DashOffset = 2
        };
        var rt = new Border(b.Element);
        Assert.Equal(1.2, rt.LineWidth, 6);
        Assert.Equal(3d, rt.HorizonalCornerRadius, 6);
        Assert.Equal(4.5, rt.VerticalCornerRadius, 6);
        Assert.Equal(2d, rt.DashOffset, 6);
    }

    [Fact]
    public void DashPattern_RoundTrip()
    {
        var b = new Border { DashPattern = new NetOfd.Core.BasicType.STArray(new double[] { 3, 2, 1 }) };
        var rt = new Border(b.Element);
        Assert.NotNull(rt.DashPattern);
        Assert.Equal(3, rt.DashPattern!.Count);
        Assert.Equal(3d, rt.DashPattern[0], 6);
        Assert.Equal(1d, rt.DashPattern[2], 6);
    }

    [Fact]
    public void SetBorderColor_WithPlainColor_RenamesElementToBorderColor()
    {
        var b = new Border().SetBorderColor(CT_Color.Red);
        var color = b.GetBorderColor();
        Assert.NotNull(color);
        Assert.Equal("BorderColor", color!.Element.Name.LocalName);
        Assert.Equal("255 0 0", color.Value);
    }

    [Fact]
    public void SetBorderColor_WithBorderColor_KeepsElementName()
    {
        var bc = new BorderColor { Value = "0 0 255" };
        var b = new Border().SetBorderColor(bc);
        Assert.Equal("BorderColor", b.GetBorderColor()!.Element.Name.LocalName);
        Assert.Equal("0 0 255", b.GetBorderColor()!.Value);
    }
}
