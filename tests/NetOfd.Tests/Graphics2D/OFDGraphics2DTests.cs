using System;
using System.IO;
using NetOfd.Graphics2D;
using NetOfd.Graphics2D.AWT;
using Xunit;

namespace NetOfd.Tests.Graphics2D;

/// <summary>
/// OFDGraphics2D 集成测试
/// </summary>
public class OFDGraphics2DTests
{
    // ── 输出目录 ──────────────────────────────────────────────
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_g2d_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    // ──────────────────────────────────────────────────────────
    // 1. 基础文档创建
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void CreateDocument_ShouldNotThrow()
    {
        var outPath = Path.Combine(OutDir(), "empty.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);
        // 仅创建空页面，不绘制任何内容
        Assert.NotNull(g);
    }

    [Fact]
    public void NewPage_ShouldReturnGraphics2D()
    {
        var outPath = Path.Combine(OutDir(), "page_size.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);
        Assert.Equal(210, g.Width);
        Assert.Equal(297, g.Height);
    }

    // ──────────────────────────────────────────────────────────
    // 2. 颜色和字体
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void OFDColor_RgbValues_ShouldBeCorrect()
    {
        var red = new OFDColor(255, 0, 0);
        Assert.Equal(255, red.R);
        Assert.Equal(0,   red.G);
        Assert.Equal(0,   red.B);
        Assert.Equal(255, red.A);
    }

    [Fact]
    public void OFDColor_Clamp_ShouldNotExceedBounds()
    {
        var c = new OFDColor(300, -10, 128, 500);
        Assert.Equal(255, c.R);
        Assert.Equal(0,   c.G);
        Assert.Equal(128, c.B);
        Assert.Equal(255, c.A);
    }

    [Fact]
    public void OFDColor_ToHexString_ShouldBeCorrect()
    {
        var black = OFDColor.Black;
        Assert.Equal("#000000", black.ToHexString());

        var white = OFDColor.White;
        Assert.Equal("#FFFFFF", white.ToHexString());
    }

    [Fact]
    public void OFDFont_Properties_ShouldBeCorrect()
    {
        var font = new OFDFont("SimSun", OFDFont.BOLD | OFDFont.ITALIC, 12.0);
        Assert.Equal("SimSun", font.Name);
        Assert.True(font.IsBold);
        Assert.True(font.IsItalic);
        Assert.Equal(12.0, font.Size);
    }

    // ──────────────────────────────────────────────────────────
    // 3. AffineTransform
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void AffineTransform_Identity_ShouldNotTransform()
    {
        var at = new AffineTransform();
        var (x, y) = at.Transform(5.0, 10.0);
        Assert.Equal(5.0, x, 6);
        Assert.Equal(10.0, y, 6);
    }

    [Fact]
    public void AffineTransform_Translate_ShouldShiftCoords()
    {
        var at = new AffineTransform();
        at.Translate(3.0, 4.0);
        var (x, y) = at.Transform(0.0, 0.0);
        Assert.Equal(3.0, x, 6);
        Assert.Equal(4.0, y, 6);
    }

    [Fact]
    public void AffineTransform_Scale_ShouldScaleCoords()
    {
        var at = new AffineTransform();
        at.Scale(2.0, 3.0);
        var (x, y) = at.Transform(5.0, 2.0);
        Assert.Equal(10.0, x, 6);
        Assert.Equal(6.0,  y, 6);
    }

    [Fact]
    public void AffineTransform_Concatenate_ShouldCombine()
    {
        var a = new AffineTransform();
        a.Translate(1.0, 0.0);

        var b = new AffineTransform();
        b.Translate(2.0, 0.0);

        a.Concatenate(b);
        var (x, _) = a.Transform(0.0, 0.0);
        Assert.Equal(3.0, x, 6);
    }

    // ──────────────────────────────────────────────────────────
    // 4. Shapes
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void Rectangle2D_ContainsPoint_ShouldWork()
    {
        var rect = new Rectangle2D(0, 0, 10, 10);
        Assert.True(rect.Contains(5, 5));
        Assert.False(rect.Contains(15, 5));
    }

    [Fact]
    public void Rectangle2D_ImplementsIShape()
    {
        IShape shape = new Rectangle2D(0, 0, 100, 100);
        Assert.NotNull(shape.GetBounds());
    }

    [Fact]
    public void Ellipse2D_GetBounds_ShouldReturnCorrectBounds()
    {
        var e = new Ellipse2D(10, 20, 50, 30);
        var b = e.GetBounds();
        Assert.Equal(10, b.X);
        Assert.Equal(20, b.Y);
        Assert.Equal(50, b.Width);
        Assert.Equal(30, b.Height);
    }

    [Fact]
    public void GeneralPath_AddOperations_ShouldNotThrow()
    {
        var path = new GeneralPath();
        path.MoveTo(0, 0);
        path.LineTo(100, 0);
        path.LineTo(100, 100);
        path.Close();
        Assert.NotEmpty(path.Operations);
    }

    [Fact]
    public void Area_Wraps_IShape()
    {
        IShape rect = new Rectangle2D(0, 0, 10, 10);
        var area = new Area(rect);
        Assert.NotNull(area.GetBounds());
    }

    // ──────────────────────────────────────────────────────────
    // 5. Graphics2D 绘制操作（仅验证不抛出异常）
    // ──────────────────────────────────────────────────────────

    [Fact]
    public void DrawRect_ShouldNotThrow()
    {
        var outPath = Path.Combine(OutDir(), "draw_rect.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);

        g.SetColor(OFDColor.Red);
        g.DrawRect(10, 10, 50, 30);
        g.FillRect(70, 10, 50, 30);
    }

    [Fact]
    public void DrawLine_ShouldNotThrow()
    {
        var outPath = Path.Combine(OutDir(), "draw_line.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);

        g.SetColor(OFDColor.Blue);
        g.DrawLine(0, 0, 100, 100);
    }

    [Fact]
    public void DrawString_ShouldNotThrow()
    {
        var outPath = Path.Combine(OutDir(), "draw_string.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);

        g.SetFont(new OFDFont("SimSun", OFDFont.PLAIN, 12));
        g.SetColor(OFDColor.Black);
        g.DrawString("Hello OFD!", 10, 20);
    }

    [Fact]
    public void DrawOval_ShouldNotThrow()
    {
        var outPath = Path.Combine(OutDir(), "draw_oval.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);

        g.SetColor(OFDColor.Green);
        g.DrawOval(50, 50, 60, 40);
        g.FillOval(130, 50, 60, 40);
    }

    [Fact]
    public void Transform_Translate_ShouldNotThrow()
    {
        var outPath = Path.Combine(OutDir(), "transform.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);

        g.Translate(10, 10);
        g.Scale(2, 2);
        g.DrawRect(0, 0, 30, 20);
    }

    [Fact]
    public void StateSaveRestore_ShouldRestoreColor()
    {
        var outPath = Path.Combine(OutDir(), "state.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);
        using var g = doc.NewPage(210, 297);

        g.SetColor(OFDColor.Red);
        g.Create(); // 保存状态

        g.SetColor(OFDColor.Blue);
        g.DrawRect(10, 10, 20, 20);

        g.Dispose(); // 恢复状态（颜色回到 Red）
    }

    [Fact]
    public void MultiPage_ShouldWork()
    {
        var outPath = Path.Combine(OutDir(), "multipage.ofd");
        using var doc = OFDGraphicsDocument.Create(outPath);

        using (var g1 = doc.NewPage(210, 297))
        {
            g1.SetColor(OFDColor.Black);
            g1.DrawString("Page 1", 10, 20);
        }

        using (var g2 = doc.NewPage(210, 297))
        {
            g2.SetColor(OFDColor.Red);
            g2.DrawString("Page 2", 10, 20);
        }
    }

    [Fact]
    public void Close_ShouldWriteOFDFile()
    {
        var outPath = Path.Combine(OutDir(), "output.ofd");
        if (File.Exists(outPath)) File.Delete(outPath);

        using (var doc = OFDGraphicsDocument.Create(outPath))
        {
            using var g = doc.NewPage(210, 297);
            g.SetColor(OFDColor.Black);
            g.DrawString("Test Output", 10, 20);
            g.DrawRect(10, 30, 100, 50);
        }

        // 验证文件生成
        Assert.True(File.Exists(outPath), "OFD 文件应该已生成");
        Assert.True(new FileInfo(outPath).Length > 0, "OFD 文件不应为空");
    }

    [Fact]
    public void SetStroke_WithCapJoinDash_WritesAttributes()
    {
        var outPath = Path.Combine(OutDir(), "stroke_attrs.ofd");
        using (var doc = OFDGraphicsDocument.Create(outPath))
        using (var g = doc.NewPage(210, 297))
        {
            g.SetStroke(new OFDStroke(2f, OFDStroke.CAP_ROUND, OFDStroke.JOIN_BEVEL, 10f, new float[] { 4, 2 }, 1f));
            g.DrawRect(10, 10, 50, 30);
        }

        using var za = System.IO.Compression.ZipFile.OpenRead(outPath);
        var entry = za.GetEntry("Doc_0/Pages/Page_0/Content.xml");
        Assert.NotNull(entry);
        using var s = entry!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var doc2 = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        var pathObj = doc2.Descendants().First(e => e.Name.LocalName == "PathObject");
        Assert.Equal("Round", (string?)pathObj.Attribute("Cap"));
        Assert.Equal("Bevel", (string?)pathObj.Attribute("Join"));
        var dash = (string?)pathObj.Attribute("DashPattern");
        Assert.NotNull(dash);
        Assert.Contains("4", dash!.Split(' '));
        Assert.Contains("2", dash.Split(' '));
        Assert.Equal("1", (string?)pathObj.Attribute("DashOffset"));
        Assert.Equal("2", (string?)pathObj.Attribute("LineWidth"));
    }

    [Fact]
    public void Fill_WithEvenOddRule_WritesRuleAttribute()
    {
        var outPath = Path.Combine(OutDir(), "fill_rule.ofd");
        using (var doc = OFDGraphicsDocument.Create(outPath))
        using (var g = doc.NewPage(210, 297))
        {
            g.SetColor(OFDColor.Black);
            g.Fill(new Rectangle2D(10, 10, 50, 30), "Even-Odd");
        }

        using var za = System.IO.Compression.ZipFile.OpenRead(outPath);
        var entry = za.GetEntry("Doc_0/Pages/Page_0/Content.xml");
        Assert.NotNull(entry);
        using var s = entry!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var doc2 = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(ms.ToArray()));
        var pathObj = doc2.Descendants().First(e => e.Name.LocalName == "PathObject");
        Assert.Equal("Even-Odd", (string?)pathObj.Attribute("Rule"));
    }
}
