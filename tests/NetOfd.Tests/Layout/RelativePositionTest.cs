using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 增量 K：Div Position.Relative 偏移语义（对齐 Java StreamingLayoutAnalyzer.itemIntoVirtualPage
/// 的 X += Left - Right / Y += Top）。采用「control vs relative 差值」隔离偏移，避免硬编码页边距常量。
/// </summary>
public class RelativePositionTest
{
    private static string TempOfd(params Div[] divs)
    {
        string path = Path.Combine(Path.GetTempPath(), $"rel_{System.Guid.NewGuid():N}.ofd");
        using (var doc = new OFDDoc(path))
        {
            foreach (var d in divs) doc.Add(d);
        }
        return path;
    }

    private static XDocument FirstPageContent(string ofd)
    {
        using var za = ZipFile.OpenRead(ofd);
        var entry = za.Entries
            .First(e => e.FullName.Replace('\\', '/').EndsWith("Doc_0/Pages/Page_0/Content.xml",
                StringComparison.OrdinalIgnoreCase));
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
    }

    /// <summary>页面中 PathObject（背景/边框盒）的 top-left 坐标集合。
    /// 路径图元自 2026-10-23 起带 Boundary，AbbreviatedData 为相对 Boundary 的局部坐标，
    /// 需叠加 Boundary.XY 还原绝对页面坐标。</summary>
    private static List<(double X, double Y)> BoxTops(XDocument content)
    {
        var list = new List<(double, double)>();
        foreach (var po in content.Descendants().Where(e => e.Name.LocalName == "PathObject"))
        {
            var ad = po.Descendants().FirstOrDefault(d => d.Name.LocalName == "AbbreviatedData")?.Value;
            if (ad is null) continue;
            var nums = System.Text.RegularExpressions.Regex
                .Matches(ad, @"-?\d+(?:\.\d+)?")
                .Select(m => double.Parse(m.Value, CultureInfo.InvariantCulture)).ToList();
            if (nums.Count < 2) continue;
            double x = nums[0], y = nums[1];
            var bAttr = (string?)po.Attribute("Boundary");
            if (bAttr is not null)
            {
                var b = bAttr.Split(' ').Select(p => double.Parse(p, CultureInfo.InvariantCulture)).ToArray();
                x += b[0]; y += b[1];
            }
            list.Add((x, y));
        }
        return list;
    }

    private static double TextObjectY(XDocument content)
    {
        var to = content.Descendants().First(e => e.Name.LocalName == "TextObject");
        var b = ((string?)to.Attribute("Boundary"))!.Split(' ');
        return double.Parse(b[1], CultureInfo.InvariantCulture);
    }

    private static Div Box(double w, double h, string color = "255 0 0")
        => new Div().SetWidth(w).SetHeight(h)
            .SetBackgroundColor(new NetOfd.Core.PageDescription.Color.CT_Color { Value = color });

    [Fact]
    public void RelativeDiv_X_Shift_Is_LeftMinusRight()
    {
        var control = Box(40, 10); // Static（默认），X=contentLeft
        var rel = Box(40, 10)
            .SetPosition(Position.Relative).SetLeft(10).SetRight(5).SetTop(3); // X+5, Y+13（含 control 高度10）

        var path = TempOfd(control, rel);
        var boxes = BoxTops(FirstPageContent(path));
        Assert.Equal(2, boxes.Count);

        var c = boxes[0]; // 先画的 control
        var r = boxes[1]; // 后画的 relative
        Assert.Equal(5, r.X - c.X, 3);   // Left - Right = 10 - 5
        Assert.Equal(13, r.Y - c.Y, 3);  // control 高度10 + Top3
    }

    [Fact]
    public void RelativeDiv_LeftOnly_Shift()
    {
        var control = Box(40, 10);
        var rel = Box(40, 10).SetPosition(Position.Relative).SetLeft(8); // X+8

        var path = TempOfd(control, rel);
        var boxes = BoxTops(FirstPageContent(path));
        Assert.Equal(8, boxes[1].X - boxes[0].X, 3);
    }

    [Fact]
    public void AbsoluteDiv_IgnoresLeftRightTop()
    {
        // 绝对定位：偏移 guard 跳过，X/Y 保持用户设定
        var abs = Box(40, 10)
            .SetPosition(Position.Absolute).SetXY(80, 60).SetLeft(10).SetRight(5).SetTop(3);

        var path = TempOfd(abs);
        var boxes = BoxTops(FirstPageContent(path));
        Assert.Single(boxes);
        Assert.Equal(80, boxes[0].X, 3);
        Assert.Equal(60, boxes[0].Y, 3);
    }

    [Fact]
    public void RelativeParagraph_Y_Shift_EqualsTop()
    {
        string Ctrl() => TempOfd(new Paragraph().AddText("Hello"));
        string Rel() => TempOfd(new Paragraph().AddText("Hello").SetPosition(Position.Relative).SetTop(4));

        double ctrlY = TextObjectY(FirstPageContent(Ctrl()));
        double relY = TextObjectY(FirstPageContent(Rel()));
        Assert.Equal(4, relY - ctrlY, 3); // 仅 Top 偏移，yOffset 常量抵消
    }
}
