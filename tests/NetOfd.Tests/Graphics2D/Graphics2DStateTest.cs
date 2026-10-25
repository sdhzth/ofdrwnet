using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Font;
using NetOfd.Graphics2D;
using NetOfd.Graphics2D.AWT;
using Xunit;

namespace NetOfd.Tests.Font;

/// <summary>
/// Graphics2D 状态管理测试：图形状态栈（Create/Dispose）、画笔/字体设置、变换矩阵、命中测试。
/// </summary>
public class Graphics2DStateTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_g2d_state_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    [Fact]
    public void Create_ShouldSnapshotAndRestoreState()
    {
        using var doc = new OFDGraphicsDocument(Out("g2d_state.ofd"));
        using var g = doc.NewPage();

        var originalColor = g.GetColor();
        g.SetColor(OFDColor.Red);
        g.Translate(30, 40);

        using (var child = (OFDGraphics2D)g.Create())
        {
            // 子上下文继承父状态
            Assert.Equal(OFDColor.Red, child.GetColor());
            // 子上下文修改不影响父
            child.SetColor(OFDColor.Green);
            child.Translate(10, 10);
        }

        Assert.Equal(OFDColor.Red, g.GetColor());
        var ctm = g.GetTransform();
        Assert.Equal(30, ctm.TranslateX, 6);
        Assert.Equal(40, ctm.TranslateY, 6);
    }

    [Fact]
    public void AffineTransform_Operations_ShouldCompose()
    {
        var tx = new AffineTransform();
        tx.Translate(100, 50);
        tx.Scale(2, 3);
        var p = tx.Transform(1, 1);
        Assert.Equal(102, p.X, 6);
        Assert.Equal(53, p.Y, 6);

        tx.Rotate(Math.PI / 2); // 绕原点旋转 90°

        // 逆变换往返：任意点的 正变换 → 逆变换 应还原
        var fwd = tx.Transform(1, 1);
        var back = tx.CreateInverse().Transform(fwd.X, fwd.Y);
        Assert.Equal(1, back.X, 6);
        Assert.Equal(1, back.Y, 6);

        // Clone 独立性
        var clone = tx.Clone();
        clone.Translate(5, 5);
        Assert.NotEqual(clone.TranslateX, tx.TranslateX);
    }

    [Fact]
    public void Stroke_Settings_ShouldAffectPathAttributes()
    {
        var outPath = Out("g2d_stroke.ofd");
        using (var doc = new OFDGraphicsDocument(outPath))
        {
            using (var g = doc.NewPage())
            {
                var stroke = new OFDStroke
                {
                    LineWidth = 1.5f,
                    DashArray = new[] { 2f, 1f },
                    EndCap = OFDStroke.CAP_ROUND,
                };
                g.SetStroke(stroke);
                Assert.Equal(1.5f, g.GetStroke().LineWidth);
                Assert.Equal(OFDStroke.CAP_ROUND, g.GetStroke().EndCap);
                g.DrawLine(10, 10, 100, 10);
            }
        }

        // 线宽与虚线应真实写入图元（此前被静默丢弃）；虚线属性对齐规范/Java 名为 DashPattern
        var content = ReadContent(outPath);
        var pathObj = content.Descendants().First(e => e.Name.LocalName == "PathObject");
        Assert.Equal("1.5", pathObj.Attribute("LineWidth")?.Value ?? pathObj.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "LineWidth")?.Value);
        Assert.NotNull(pathObj.Attribute("DashPattern") ?? (object?)pathObj.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "DashPattern"));
    }

    [Fact]
    public void CompositeAlpha_ShouldRecordObjectAlpha()
    {
        var outPath = Out("g2d_alpha.ofd");
        using (var doc = new OFDGraphicsDocument(outPath))
        {
            using (var g = doc.NewPage())
            {
                g.SetComposite(AlphaComposite.SrcOver); // 默认不写
                g.FillRect(0, 0, 20, 20);

                g.SetComposite(AlphaComposite.SrcOver.Derive(0.4f));
                g.FillRect(30, 0, 20, 20);
            }
        }

        var content = ReadContent(outPath);
        var paths = content.Descendants()
            .Where(e => e.Name.LocalName == "PathObject").ToList();
        Assert.Equal(2, paths.Count);
        Assert.Null(paths[0].Attribute("Alpha"));
        Assert.Equal("102", paths[1].Attribute("Alpha")?.Value); // 0.4*255=102
    }

    private static XElement ReadContent(string ofdPath)
    {
        using var ms = File.OpenRead(ofdPath);
        using var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var entry = za.Entries.First(e => e.FullName.EndsWith("Content.xml", StringComparison.Ordinal));
        using var s = entry.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return XDocument.Parse(System.Text.Encoding.UTF8.GetString(buf.ToArray())).Root!;
    }

    [Fact]
    public void HitTest_AndClipBounds_ShouldWork()
    {
        using var doc = new OFDGraphicsDocument(Out("g2d_hit.ofd"));
        using var g = doc.NewPage();

        var rect = new Rectangle2D(10, 10, 50, 50);
        Assert.True(g.Hit(new Rectangle2D(20, 20, 5, 5), rect, false));
        Assert.False(g.Hit(new Rectangle2D(200, 200, 5, 5), rect, false));

        var bounds = g.GetClipBounds();
        Assert.True(bounds.Width > 0 && bounds.Height > 0);

        g.Clip(new Rectangle2D(0, 0, 100, 100));
        Assert.NotNull(g.GetClip());

        // 渲染提示存取
        g.SetRenderingHint("key", "value");
        Assert.Equal("value", g.GetRenderingHint("key"));
    }

    [Fact]
    public void SetFont_CustomFont_ShouldAffectTextOutput()
    {
        var outPath = Out("g2d_font.ofd");
        using (var doc = new OFDGraphicsDocument(outPath))
        {
            using (var g = doc.NewPage())
            {
                var font = new NetOfd.Graphics2D.AWT.OFDFont("SimSun", 0, 12);
                g.SetFont(font);
                Assert.Equal(font, g.GetFont());
                g.DrawString("自定义字体", 10, 20);
            }
        }
        var ofd = File.ReadAllBytes(outPath);
        using var ms = new MemoryStream(ofd);
        using var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var resEntry = za.Entries.FirstOrDefault(e => e.FullName.EndsWith("Res.xml", StringComparison.Ordinal));
        Assert.NotNull(resEntry);
        using var s = resEntry.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        var res = XDocument.Parse(System.Text.Encoding.UTF8.GetString(buf.ToArray()));
        Assert.Contains(res.Descendants(), e => e.Name.LocalName == "Font");
    }
}
