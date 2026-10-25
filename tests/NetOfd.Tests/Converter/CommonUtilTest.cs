using NetOfd.Converter.Utils;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using System.Globalization;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// 覆盖 <see cref="CommonUtil"/> 的单位换算、颜色转换、变换矩阵与临时文件工具。
/// 对应 Java: org.ofdrw.converter.utils.CommonUtil
/// </summary>
public class CommonUtilTest
{
    // ────────────────────────────────────────────────────────
    //  单位换算
    // ────────────────────────────────────────────────────────

    [Fact]
    public void MillimetersToPixel_At72Dpi()
    {
        // 1 英寸 = 25.4mm = 72px
        Assert.Equal(72.0, CommonUtil.MillimetersToPixel(25.4, 72), 6);
        Assert.Equal(210.0 * 300 / 25.4, CommonUtil.MillimetersToPixel(210, 300), 6);
    }

    [Fact]
    public void PixelMillimeters_RoundTrip()
    {
        const double mm = 123.456;
        double px = CommonUtil.MillimetersToPixel(mm, 200);
        Assert.Equal(mm, CommonUtil.PixelToMillimeters(px, 200), 6);
    }

    [Fact]
    public void DpiToPpm_EqualsOneMillimeterInPixel()
    {
        foreach (int dpi in new[] { 72, 96, 200, 300 })
        {
            Assert.Equal(CommonUtil.MillimetersToPixel(1, dpi), CommonUtil.DpiToPpm(dpi), 9);
        }
        // 300 DPI ≈ 11.811 ppm（PDF 常用换算）
        Assert.Equal(11.811, CommonUtil.DpiToPpm(300), 3);
    }

    [Fact]
    public void ConverterDpi_Uses72DpiBaseline()
    {
        Assert.Equal(72.0, CommonUtil.ConverterDpi(25.4), 6);
        Assert.Equal(CommonUtil.MillimetersToPixel(10, 72), CommonUtil.ConverterDpi(10), 9);
    }

    [Fact]
    public void DoubleArrayToFloatArray_ConvertsAndHandlesNull()
    {
        float[] r = CommonUtil.DoubleArrayToFloatArray(new[] { 1.5, 2.25, -3.0 });
        Assert.Equal(new[] { 1.5f, 2.25f, -3.0f }, r);

        Assert.Empty(CommonUtil.DoubleArrayToFloatArray(null));
    }

    // ────────────────────────────────────────────────────────
    //  页面区域
    // ────────────────────────────────────────────────────────

    [Fact]
    public void GetPageBox_PrefersPhysicalThenApplicationThenContent()
    {
        var area = new CT_PageArea
        {
            PhysicalBox = new STBox(0, 0, 210, 297),
            ApplicationBox = new STBox(1, 2, 3, 4),
            ContentBox = new STBox(5, 6, 7, 8),
        };
        Assert.Equal(new STBox(0, 0, 210, 297), CommonUtil.GetPageBox(area, 100, 100));

        area.PhysicalBox = null;
        Assert.Equal(new STBox(1, 2, 3, 4), CommonUtil.GetPageBox(area, 100, 100));

        area.ApplicationBox = null;
        Assert.Equal(new STBox(5, 6, 7, 8), CommonUtil.GetPageBox(area, 100, 100));
    }

    [Fact]
    public void GetPageBox_FallsBackToGivenSize()
    {
        Assert.Equal(new STBox(0, 0, 210, 297), CommonUtil.GetPageBox(new CT_PageArea(), 210, 297));
        Assert.Equal(new STBox(0, 0, 210, 297), CommonUtil.GetPageBox(null, 210, 297));
    }

    // ────────────────────────────────────────────────────────
    //  颜色
    // ────────────────────────────────────────────────────────

    [Theory]
    [InlineData("#FF0000", "#FF0000")]
    [InlineData("255 0 0", "rgb(255,0,0)")]
    [InlineData("0 0 0", "rgb(0,0,0)")]
    [InlineData("128", "rgb(128,128,128)")]
    [InlineData("0 255 255 0", "rgb(255,0,0)")]   // CMYK → 红
    public void ConvertOfdColorToHtml_ByString(string input, string expected)
        => Assert.Equal(expected, CommonUtil.ConvertOfdColorToHtml(input));

    [Fact]
    public void ConvertOfdColorToHtml_ClampsOutOfRangeAndHandlesEmpty()
    {
        Assert.Equal("rgb(255,255,255)", CommonUtil.ConvertOfdColorToHtml("300 400 500"));
        Assert.Null(CommonUtil.ConvertOfdColorToHtml((string?)null));
        Assert.Null(CommonUtil.ConvertOfdColorToHtml("   "));
    }

    [Fact]
    public void ConvertOfdColorToHtml_BySTArray()
    {
        Assert.Equal("rgb(1,2,3)", CommonUtil.ConvertOfdColorToHtml(new STArray("1 2 3")));
        Assert.Null(CommonUtil.ConvertOfdColorToHtml((STArray?)null));
    }

    [Fact]
    public void ConvertColorToHtml_ReusesOfdColorUtil()
    {
        Assert.Equal("rgb(0,0,0)", CommonUtil.ConvertColorToHtml(CT_Color.Black));
        Assert.Equal("rgb(255,255,255)", CommonUtil.ConvertColorToHtml(CT_Color.White));
        Assert.Null(CommonUtil.ConvertColorToHtml(null));
    }

    [Fact]
    public void ConvertColorToHtml_EmitsRgbaWhenAlphaPresent()
    {
        var color = CT_Color.FromRgb(255, 0, 0);
        color.Alpha = 128;
        var css = CommonUtil.ConvertColorToHtml(color);

        Assert.StartsWith("rgba(255,0,0,", css);
        Assert.EndsWith(")", css);
    }

    // ────────────────────────────────────────────────────────
    //  变换矩阵
    // ────────────────────────────────────────────────────────

    [Fact]
    public void MatrixFrom_SvgStyleUsesSpaceSeparator()
    {
        string m = CommonUtil.MatrixFrom(new STArray("1 0 0 1 0 0"));
        Assert.Equal("matrix(1 0 0 1 0 0)", m);
    }

    [Fact]
    public void MatrixFrom_CssStyleUsesCommaSeparator()
    {
        string m = CommonUtil.MatrixFrom(new STArray("1 0 0 1 0 0"), cssComma: true);
        Assert.Equal("matrix(1, 0, 0, 1, 0, 0)", m);
    }

    [Fact]
    public void MatrixFrom_ConvertsTranslationFromMmToPixel()
    {
        // e = 25.4mm → 72px；f = 12.7mm → 36px
        string m = CommonUtil.MatrixFrom(new STArray("2 0 0 2 25.4 12.7"));

        var nums = ExtractNumbers(m);
        Assert.Equal(new[] { 2.0, 0.0, 0.0, 2.0 }, nums[..4]);
        Assert.Equal(72.0, nums[4], 4);
        Assert.Equal(36.0, nums[5], 4);
    }

    [Fact]
    public void MatrixFrom_RejectsShortCtm()
    {
        Assert.Throws<ArgumentException>(() => CommonUtil.MatrixFrom(new STArray("1 0 0")));
        Assert.Throws<ArgumentNullException>(() => CommonUtil.MatrixFrom(null!));
    }

    // ────────────────────────────────────────────────────────
    //  临时文件
    // ────────────────────────────────────────────────────────

    [Fact]
    public void GenerateTempFilePath_UniqueAndUnderTempDir()
    {
        string a = CommonUtil.GenerateTempFilePath();
        string b = CommonUtil.GenerateTempFilePath();

        Assert.NotEqual(a, b);
        Assert.StartsWith(Path.GetTempPath(), a);
        Assert.False(File.Exists(a));   // 只生成路径，不创建文件
    }

    // ── 辅助 ────────────────────────────────────────────────
    private static double[] ExtractNumbers(string matrix)
    {
        int s = matrix.IndexOf('(');
        int e = matrix.LastIndexOf(')');
        return matrix[(s + 1)..e]
            .Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => double.Parse(p, CultureInfo.InvariantCulture))
            .ToArray();
    }
}
