using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// Line（线条）与 Cell（单元格）画布元素测试。
/// 对齐 Java: org.ofdrw.layout.element.canvas.Line / Cell（经 Canvas 元素绘制，NetOfd 以页面绝对 mm 绘制）。
/// </summary>
public class LineAndCellTest
{
    private static string OutDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "netofd_linecell_tests");
        System.IO.Directory.CreateDirectory(dir);
        return dir;
    }

    private static List<XDocument> AllPageContents(string ofd)
    {
        using var za = ZipFile.OpenRead(ofd);
        var docs = new List<XDocument>();
        foreach (var e in za.Entries.Where(e => e.FullName.EndsWith("/Content.xml", StringComparison.OrdinalIgnoreCase)))
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            docs.Add(XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray())));
        }
        return docs;
    }

    private static IEnumerable<XElement> TextObjects(XDocument content)
        => content.Descendants().Where(el => el.Name.LocalName == "TextObject");

    /// <summary>解析 PathObject 的 AbbreviatedData 子元素为坐标点列表（还原绝对页面坐标）。
    /// 自 2026-10-23 起路径图元带 Boundary，AbbreviatedData 为相对 Boundary 的局部坐标，
    /// 需叠加 Boundary.XY 还原（半线宽外扩在加减中相消，绝对值不变）。</summary>
    private static List<(char Op, double X, double Y)> PathCoords(XElement pathObj)
    {
        var data = pathObj.Descendants().FirstOrDefault(d => d.Name.LocalName == "AbbreviatedData")?.Value ?? "";
        double ox = 0, oy = 0;
        var bAttr = (string?)pathObj.Attribute("Boundary");
        if (bAttr is not null)
        {
            var b = bAttr.Split(' ').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            ox = b[0]; oy = b[1];
        }
        var list = new List<(char, double, double)>();
        var ms = Regex.Matches(data, @"([ML])\s+(-?\d+(?:\.\d+)?)\s+(-?\d+(?:\.\d+)?)");
        foreach (Match m in ms)
            list.Add((m.Groups[1].Value[0],
                double.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) + ox,
                double.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture) + oy));
        return list;
    }

    /// <summary>Line 在绝对坐标绘制一条描边线段，含线宽与颜色。</summary>
    [Fact]
    public void Line_DrawsStrokeSegment_AtAbsoluteCoords()
    {
        var path = System.IO.Path.Combine(OutDir(), "line.ofd");
        var line = new Line(80, 20).SetPosition(30, 40)
            .SetBeginPoint(0, 0).SetEndPoint(60, 0)
            .SetLineColor("#FF0000").SetLineWidth(1.2);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Canvas(80, 20, line));
            doc.Close();
        }

        var content = AllPageContents(path)[0];
        var pathObj = content.Descendants().First(e => e.Name.LocalName == "PathObject");
        Assert.Equal("true", (string?)pathObj.Attribute("Stroke"));
        var coords = PathCoords(pathObj);
        // M (30,40) → L (90,40)：绝对 = 区域左上角 + 局部坐标
        Assert.Equal('M', coords[0].Op);
        Assert.Equal(30, coords[0].X, 2);
        Assert.Equal(40, coords[0].Y, 2);
        Assert.Equal('L', coords[1].Op);
        Assert.Equal(90, coords[1].X, 2);
        Assert.Equal(40, coords[1].Y, 2);
        Assert.Equal(1.2, double.Parse(((string?)pathObj.Attribute("LineWidth"))!, System.Globalization.CultureInfo.InvariantCulture), 2);
        // 颜色写入 StrokeColor 子元素的 Value 属性（255 0 0 = 红）
        var sc = pathObj.Descendants().FirstOrDefault(d => d.Name.LocalName == "StrokeColor");
        Assert.EndsWith("255 0 0", (string?)sc?.Attribute("Value"));
    }

    /// <summary>Line 透明度写入 PathObject Alpha。</summary>
    [Fact]
    public void Line_Opacity_WritesAlpha()
    {
        var path = System.IO.Path.Combine(OutDir(), "line_alpha.ofd");
        var line = new Line(80, 20).SetPosition(30, 40)
            .SetBeginPoint(0, 0).SetEndPoint(60, 0).SetLineOpacity(0.5);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Canvas(80, 20, line));
            doc.Close();
        }

        var content = AllPageContents(path)[0];
        var pathObj = content.Descendants().First(e => e.Name.LocalName == "PathObject");
        Assert.True(double.TryParse(((string?)pathObj.Attribute("Alpha"))!, out var alpha));
        Assert.InRange(alpha, 125, 130); // 0.5*255 ≈ 127.5
    }

    /// <summary>Line 起点终点重合则不绘制（无 Stroke PathObject）。</summary>
    [Fact]
    public void Line_ZeroLength_NotDrawn()
    {
        var path = System.IO.Path.Combine(OutDir(), "line_zero.ofd");
        var line = new Line(80, 20).SetPosition(30, 40)
            .SetBeginPoint(10, 10).SetEndPoint(10, 10);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Canvas(80, 20, line));
            doc.Close();
        }
        var content = AllPageContents(path)[0];
        Assert.DoesNotContain(content.Descendants(), e => e.Name.LocalName == "PathObject"
            && ((string?)e.Attribute("Stroke") == "true"));
    }

    /// <summary>Cell 文字内容经绝对定位写入页面，边界与对齐生效。</summary>
    [Fact]
    public void Cell_Text_AtAbsolutePositionWithAlignment()
    {
        var path = System.IO.Path.Combine(OutDir(), "cell_text.ofd");
        var cell = new Cell(100, 20).SetPosition(50, 60)
            .SetValue("张三").SetFontSize(3);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(cell);
            doc.Close();
        }

        var content = AllPageContents(path)[0];
        var txt = TextObjects(content).First(t => t.Descendants().Any(d => d.Name.LocalName == "TextCode" && d.Value.Contains("张三")));
        // CellContentDrawer 绝对定位在 (50,60)；默认左对齐 + 垂直居中单行 → 基线 Y = 60 + height/2 + fontSize/2 - ...
        var bx = ((string?)txt.Attribute("Boundary"))!.Split(' ').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // 垂直居中单行 3mm 字体、20mm 盒：文字顶 Y = 60 + (20-3)/2 = 68.5
        Assert.Equal(50, bx[0], 1);        // 文字左缘对齐 50
        Assert.InRange(bx[1], 68, 69);     // 文字顶 ≈ 68.5（盒顶 60 + 垂直居中偏移）
    }

    /// <summary>Cell 水平居中：文字整体落在盒水平中心。</summary>
    [Fact]
    public void Cell_CenterAlign_XCentered()
    {
        var path = System.IO.Path.Combine(OutDir(), "cell_center.ofd");
        var cell = new Cell(100, 20).SetPosition(50, 60)
            .SetValue("张三").SetFontSize(3).SetTextAlign(TextAlign.Center);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(cell);
            doc.Close();
        }
        var content = AllPageContents(path)[0];
        var txt = TextObjects(content).First(t => t.Descendants().Any(d => d.Name.LocalName == "TextCode" && d.Value.Contains("张三")));
        var bx = ((string?)txt.Attribute("Boundary"))!.Split(' ').Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        // 文字中心 = 50 + 50 = 100；宽 6 → 左缘 97
        Assert.Equal(97, bx[0], 1);
    }
}
