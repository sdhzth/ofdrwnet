using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Font;
using Xunit;

namespace NetOfd.Tests.Font;

/// <summary>
/// 字体管理功能测试：环境字体发现、内置字体度量、字宽表完整性。
/// </summary>
public class FontModuleTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_font_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    [Fact]
    public void EnvFont_ShouldDiscoverSystemFonts()
    {
        Assert.NotEmpty(EnvFont.GetFontDirs());
        var map = EnvFont.GetFontMap();
        Assert.NotNull(map);

        // 测试机上 SimSun 应可被识别（Windows CI 环境自带；Linux 由 CI 安装 CJK 字体）
        EnvFont.ClearCache();
        var installed = EnvFont.IsFontInstalled("SimSun");
        if (installed)
            Assert.NotNull(EnvFont.FindFontFile("SimSun"));

        Assert.False(EnvFont.IsFontInstalled("NoSuchFont_XYZ_123"));
    }

    [Fact]
    public void BuiltInFont_Metrics_ShouldBeSane()
    {
        using var font = new OFDFont(FontName.SimSun);
        Assert.True(font.IsBuiltIn);
        Assert.Equal("宋体", font.FamilyName);
        Assert.True(font.Embeddable);

        // 宽字符不窄于窄字符
        Assert.True(font.GetCharWidthScale('W') >= font.GetCharWidthScale('i'));
        // 度量文本宽度随字号线性
        double w12 = font.MeasureTextWidth("测试AB", 12);
        double w24 = font.MeasureTextWidth("测试AB", 24);
        Assert.True(w12 > 0);
        Assert.Equal(2 * w12, w24, 5);
    }

    [Fact]
    public void CustomFont_FromFile_ShouldExposePath()
    {
        var path = EnvFont.FindFontFile("SimSun");
        if (path is null)
        {
            path = EnvFont.FindFontFile("Microsoft YaHei") ?? EnvFont.FindFontFile("Arial");
        }
        if (path is null) return; // 无可用系统字体时跳过

        using var font = new OFDFont(Path.GetFileNameWithoutExtension(path), "Custom", path);
        Assert.False(font.IsBuiltIn);
        Assert.Equal(path, font.FontFilePath);
        Assert.True(font.MeasureTextWidth("ABC", 10) > 0);
    }

    [Fact]
    public void FontNames_WidthTables_ShouldCoverAscii()
    {
        foreach (FontName name in Enum.GetValues<FontName>())
        {
            var widths = FontNames.GetAsciiWidths(name);
            Assert.Equal(95, widths.Length);
            // 可见 ASCII（A、z、0）应为正宽度
            Assert.True(widths['A' - 32] > 0, $"{name}: 'A' 宽度异常");
            Assert.True(widths['0' - 32] > 0, $"{name}: '0' 宽度异常");
        }

        Assert.Contains("宋", FontNames.GetFamilyName(FontName.SimSun));
        Assert.False(string.IsNullOrWhiteSpace(FontNames.GetFontNameValue(FontName.SimSun)));
    }
}

/// <summary>
/// Graphics2D 绘图接口功能测试：绘制 → 关闭打包 → 验证 OFD 内容结构。
/// </summary>
public class Graphics2DFunctionalTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_g2d_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private static byte[]? ReadEntry(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var e = za.GetEntry(entryName);
        if (e == null) return null;
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    private static XDocument EntryXml(byte[] ofd, string name)
        => XDocument.Parse(System.Text.Encoding.UTF8.GetString(ReadEntry(ofd, name)!));

    [Fact]
    public void Drawing_AllShapeTypes_ShouldLandInContentXml()
    {
        var outPath = Out("g2d_shapes.ofd");
        using (var doc = new NetOfd.Graphics2D.OFDGraphicsDocument(outPath))
        {
            using (var g = doc.NewPage(210, 297))
            {
                g.SetColor(NetOfd.Graphics2D.AWT.OFDColor.Red);
                g.DrawLine(10, 10, 100, 10);
                g.DrawRect(10, 20, 50, 30);
                g.FillRect(70, 20, 50, 30);
                g.DrawRoundRect(10, 60, 40, 25, 5, 5);
                g.FillOval(70, 60, 40, 25);
                g.DrawArc(120, 20, 40, 40, 0, 180);

                double[] xs = { 10, 40, 70 };
                double[] ys = { 200, 220, 200 };
                g.DrawPolyline(xs, ys, 3);
                g.FillPolygon(xs, ys, 3);

                g.SetColor(NetOfd.Graphics2D.AWT.OFDColor.Blue);
                g.DrawString("图形接口测试", 20, 250);
                doc.NewId();
            }
        }

        var ofd = File.ReadAllBytes(outPath);
        var content = EntryXml(ofd, "Doc_0/Pages/Page_0/Content.xml");
        // 圆角矩形/椭圆/圆弧曾因未实现 IShape 而静默输出空路径（回归护栏）
        var paths = content.Descendants()
            .Count(e => e.Name.LocalName == "PathObject" &&
                        !string.IsNullOrWhiteSpace(e.Value));
        var texts = content.Descendants().Count(e => e.Name.LocalName == "TextObject");
        Assert.True(paths >= 6, $"应包含多个非空 Path 图元，实际 {paths}");
        Assert.Equal(1, texts);

        // MaxUnitID 已分配（注意：CommonData 中 MaxUnitID 为子元素，DocInfo 中则为属性）
        var cdata = EntryXml(ofd, "Doc_0/Document.xml").Descendants()
            .First(e => e.Name.LocalName == "MaxUnitID");
        Assert.True(int.TryParse(cdata.Value, out var mid) && mid >= 1);
    }

    [Fact]
    public void Transform_RotateAndTranslate_ShouldProduceCTM()
    {
        var outPath = Out("g2d_transform.ofd");
        using (var doc = new NetOfd.Graphics2D.OFDGraphicsDocument(outPath))
        {
            using (var g = doc.NewPage())
            {
                g.Translate(50, 50);
                g.Rotate(Math.PI / 4);
                g.Scale(2, 1);
                g.DrawString("旋转文本", 0, 0);
            }
        }
        var ofd = File.ReadAllBytes(outPath);
        var text = EntryXml(ofd, "Doc_0/Pages/Page_0/Content.xml")
            .Descendants().First(e => e.Name.LocalName == "TextObject");
        // 变换应体现在 CTM 上（非单位矩阵），文本位置为变换后坐标
        var ctm = text.Attribute("CTM")?.Value;
        Assert.False(string.IsNullOrEmpty(ctm));
    }

    [Fact]
    public void AddImage_AndDraw_ShouldRegisterResource()
    {
        var png = Out("g2d_img.png");
        File.WriteAllBytes(png, Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="));

        var outPath = Out("g2d_image.ofd");
        long imgId;
        using (var doc = new NetOfd.Graphics2D.OFDGraphicsDocument(outPath))
        {
            using (var g = doc.NewPage())
            {
                var media = doc.AddImage(png);
                imgId = media.Id;
                g.DrawImage(media, 10, 10, 40, 30);
            }
        }

        // 图像资源登记在 DocumentRes.xml（Graphics2D 文档无页面级资源）
        var resXml = EntryXml(File.ReadAllBytes(outPath), "Doc_0/DocumentRes.xml");
        Assert.Contains(resXml.Descendants(), e =>
            e.Name.LocalName == "MultiMedia" && e.Attribute("ID")?.Value == imgId.ToString());
    }
}
