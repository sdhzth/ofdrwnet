using System;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.PageDescription.Color;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 高洛德渐变模型测试（GB/T 33190 §8.3.4.4/8.3.4.5）：
/// CT_GouraudShd（自由三角网格）/ CT_LaGouraudShd（网格三角带）/ Point / EdgeFlag 的
/// 序列化结构、往返解析、CT_Color 互斥挂接与 EdgeFlag 解析规则。
/// 对齐 Java: CT_GouraudShd / CT_LaGouraudShd / Point / EdgeFlag
/// </summary>
public class GouraudShdModelTest
{
    // ── 1. 自由高洛德序列化结构 ──────────────────────────────────
    [Fact]
    public void GouraudShd_Serialize_ShouldEmitPointsWithEdgeFlagAndColors()
    {
        var shd = new CT_GouraudShd()
            .SetExtend(true)
            .SetBackColor(CT_Color.White)
            .AddPoints(
                CT_GouraudShd.P(20, 20, CT_Color.Red),
                CT_GouraudShd.P(120, 20, CT_Color.Green),
                CT_GouraudShd.P(20, 60, CT_Color.Blue, EdgeFlag.Flag1));

        var xml = shd.ToString();
        // 顶层元素 + Extend 属性
        Assert.Contains("<GouraudShd", xml);
        Assert.Contains("Extend=\"1\"", xml);
        // BackColor 子元素（改名挂接）
        Assert.Contains("<BackColor", xml);
        // 三个 Point，各自携带 X/Y 属性与 Color 子元素
        Assert.Contains("X=\"20\"", xml);
        Assert.Contains("Y=\"60\"", xml);
        Assert.Contains("EdgeFlag=\"1\"", xml);
        // EdgeFlag 缺省（前两个点）不写属性
        Assert.DoesNotContain("EdgeFlag=\"0\"", xml);
    }

    // ── 2. 往返解析（含 BackColor 改名读回）────────────────────
    [Fact]
    public void GouraudShd_ParseRoundTrip_ShouldRestorePointsBackColorExtend()
    {
        var shd = new CT_GouraudShd()
            .SetExtend(false)
            .SetBackColor(CT_Color.FromRgb(200, 200, 200))
            .AddPoints(
                CT_GouraudShd.P(0, 0, CT_Color.Red),
                CT_GouraudShd.P(100, 0, CT_Color.Green, EdgeFlag.Flag2),
                CT_GouraudShd.P(0, 80, CT_Color.Blue));

        var reparsed = new CT_GouraudShd(XElement.Parse(shd.ToString()));

        Assert.Equal(false, reparsed.Extend);
        Assert.NotNull(reparsed.BackColor);
        Assert.Equal("200 200 200", reparsed.BackColor!.Value!.Trim());

        var pts = reparsed.GetPoints();
        Assert.Equal(3, pts.Count);
        Assert.Equal(0, pts[0].X);
        Assert.Equal(100, pts[1].X);
        Assert.Equal(EdgeFlag.Flag2, pts[1].EdgeFlag);   // 显式标志
        Assert.Null(pts[2].EdgeFlag);                    // 缺省不写 → null
        Assert.Equal("0 0 255", pts[2].Color!.Value!.Trim());
    }

    // ── 3. 网格高洛德 VerticesPerRow ───────────────────────────
    [Fact]
    public void LaGouraudShd_Serialize_ShouldEmitVerticesPerRowAttribute()
    {
        var lat = new CT_LaGouraudShd()
            .SetVerticesPerRow(3)
            .AddPoints(
                CT_GouraudShd.P(0, 0, CT_Color.Red),
                CT_GouraudShd.P(50, 0, CT_Color.Green),
                CT_GouraudShd.P(100, 0, CT_Color.Blue),
                CT_GouraudShd.P(0, 50, CT_Color.Black),
                CT_GouraudShd.P(50, 50, CT_Color.White),
                CT_GouraudShd.P(100, 50, CT_Color.Red));

        var xml = lat.ToString();
        Assert.Contains("<LaGouraudShd", xml);
        Assert.Contains("VerticesPerRow=\"3\"", xml);

        // 往返：子类元素名保留 + 属性读回 + 点列表
        var re = new CT_LaGouraudShd(XElement.Parse(xml));
        Assert.Equal(3, re.VerticesPerRow);
        Assert.Equal(6, re.GetPoints().Count);
        Assert.Contains("<LaGouraudShd", re.ToString());
    }

    // ── 4. CT_Color 互斥挂接：GouraudShd 进入颜色簇链 ─────────────
    [Fact]
    public void Color_MutualExclusion_ShouldSwapGouraudClusters()
    {
        var fill = new CT_Color()
            .SetGouraudShd(new CT_GouraudShd().AddPoints(
                CT_GouraudShd.P(0, 0, CT_Color.Red),
                CT_GouraudShd.P(10, 0, CT_Color.Green),
                CT_GouraudShd.P(0, 10, CT_Color.Blue)));

        Assert.NotNull(fill.GetGouraudShd());
        Assert.Null(fill.GetLaGouraudShd());
        Assert.NotNull(fill.GetColorCluster());     // 链上可见
        Assert.IsType<CT_GouraudShd>(fill.GetColorCluster());

        // 换挂 LaGouraudShd：GouraudShd 被移除（互斥）
        var lat = new CT_LaGouraudShd().SetVerticesPerRow(2);
        lat.AddPoints(
            CT_GouraudShd.P(0, 0, CT_Color.Red),
            CT_GouraudShd.P(10, 0, CT_Color.Green),
            CT_GouraudShd.P(0, 10, CT_Color.Blue),
            CT_GouraudShd.P(10, 10, CT_Color.White));
        fill.SetLaGouraudShd(lat);

        Assert.Null(fill.GetGouraudShd());
        Assert.NotNull(fill.GetLaGouraudShd());
        Assert.IsType<CT_LaGouraudShd>(fill.GetColorCluster());

        // 再换 Pattern：全部渐变簇被移除
        var pattern = new CT_Pattern { Width = 5, Height = 5 };
        fill.SetPattern(pattern);
        Assert.Null(fill.GetGouraudShd());
        Assert.Null(fill.GetLaGouraudShd());
        Assert.IsType<CT_Pattern>(fill.GetColorCluster());
    }

    // ── 5. EdgeFlag 解析规则 ──────────────────────────────────
    [Fact]
    public void EdgeFlag_Parse_ShouldDefaultFlag0AndRejectInvalid()
    {
        Assert.Equal(EdgeFlag.Flag0, EdgeFlagUtil.Parse(null));
        Assert.Equal(EdgeFlag.Flag0, EdgeFlagUtil.Parse(""));
        Assert.Equal(EdgeFlag.Flag0, EdgeFlagUtil.Parse("  "));
        Assert.Equal(EdgeFlag.Flag0, EdgeFlagUtil.Parse("0"));
        Assert.Equal(EdgeFlag.Flag1, EdgeFlagUtil.Parse("1"));
        Assert.Equal(EdgeFlag.Flag2, EdgeFlagUtil.Parse("2"));
        Assert.Equal("2", EdgeFlagUtil.ToXml(EdgeFlag.Flag2));
        Assert.Throws<ArgumentException>(() => EdgeFlagUtil.Parse("3"));
        Assert.Throws<ArgumentException>(() => EdgeFlagUtil.Parse("x"));
    }
}
