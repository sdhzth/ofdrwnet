using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// ArtWord 艺术字元素测试。
/// 对齐 Java: org.ofdrw.layout.element.canvas.ArtWord（水平/垂直缩放、倾斜、偏移、字间距、下划线、对齐）。
/// </summary>
public class ArtWordTest
{
    private static string OutDir()
    {
        var dir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "netofd_artword_tests");
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

    private static string TextOf(XElement textObj)
    {
        var sb = new StringBuilder();
        foreach (var tc in textObj.Descendants().Where(el => el.Name.LocalName == "TextCode"))
            sb.Append(tc.Value);
        return sb.ToString();
    }

    /// <summary>TextObject 的 CTM 数组（缺省 [1,0,0,1,0,0]）与局部 Boundary 原点。</summary>
    private static (double[] Ctm, double Bx, double By) Geom(XElement textObj)
    {
        var ctm = new double[] { 1, 0, 0, 1, 0, 0 };
        var a = textObj.Attribute("CTM")?.Value;
        if (a != null)
        {
            var parts = a.Split(' ');
            for (int i = 0; i < 6; i++) ctm[i] = double.Parse(parts[i], System.Globalization.CultureInfo.InvariantCulture);
        }
        var b = ((string?)textObj.Attribute("Boundary"))!
            .Split(' ').Select(double.Parse).ToArray();
        return (ctm, b[0], b[1]);
    }

    /// <summary>Transform 绝对坐标（CTM·局部原点）。</summary>
    private static (double X, double Y) Abs(double[] ctm, double x, double y)
        => (ctm[0] * x + ctm[2] * y + ctm[4], ctm[1] * x + ctm[3] * y + ctm[5]);

    /// <summary>左对齐基础定位：TextObject 局部 Boundary 原点随 CTM 平移落到绝对 (30,43)。</summary>
    [Fact]
    public void ArtWord_DefaultLeft_PositionsByCtm()
    {
        var path = System.IO.Path.Combine(OutDir(), "artword_default.ofd");
        using (var doc = new OFDDoc(path))
        {
            var art = new ArtWord(80, 20).SetPosition(30, 40)
                .SetText("艺术").SetFontSize(3);
            doc.Add(new Canvas(80, 20, art));
            doc.Close();
        }

        var content = AllPageContents(path)[0];
        var txt = TextObjects(content).First(t => TextOf(t).Contains("艺术"));
        var (ctm, bx, by) = Geom(txt);
        // CTM e/f = 盒原点 + 偏移（offset=0）
        Assert.Equal(new double[] { 1, 0, 0, 1, 30, 40 }, ctm);
        // 局部原点 = 对齐后 x=0, 基线=字号 3
        Assert.Equal(0, bx);
        Assert.Equal(3, by);
        var (ax, ay) = Abs(ctm, bx, by);
        Assert.Equal(30, ax);
        Assert.Equal(43, ay);
    }

    /// <summary>水平居中：文本整体右移使中心落于盒中央，局部 x 偏移体现于 CTM 平移（在缩放时同样正确）。</summary>
    [Fact]
    public void ArtWord_CenterAlign_OffsetsLocalX()
    {
        var path = System.IO.Path.Combine(OutDir(), "artword_center.ofd");
        using (var doc = new OFDDoc(path))
        {
            // "AB" ASCII 各 1.8mm（0.6em×3）→ 文本宽 3.6；盒宽 80 → center x = (80-3.6)/2 = 38.2
            var art = new ArtWord(80, 20).SetPosition(30, 40)
                .SetText("AB").SetFontSize(3).SetTextAlign(TextAlign.Center);
            doc.Add(new Canvas(80, 20, art));
            doc.Close();
        }

        var txt = TextObjects(AllPageContents(path)[0]).First(t => TextOf(t) == "AB");
        var (ctm, bx, by) = Geom(txt);
        Assert.Equal(38.2, bx, 3);
        Assert.Equal(new double[] { 1, 0, 0, 1, 30, 40 }, ctm);
    }

    /// <summary>水平/垂直缩放与偏移写入 CTM：a=hs, d=vs, e/f=盒原点+offset。</summary>
    [Fact]
    public void ArtWord_ScalingAndOffset_WriteCtm()
    {
        var path = System.IO.Path.Combine(OutDir(), "artword_scale.ofd");
        using (var doc = new OFDDoc(path))
        {
            var art = new ArtWord(80, 20).SetPosition(30, 40)
                .SetText("艺术").SetFontSize(3)
                .SetHorizontalScaling(0.5).SetVerticalScaling(2)
                .SetOffsetX(5).SetOffsetY(7);
            doc.Add(new Canvas(80, 20, art));
            doc.Close();
        }

        var txt = TextObjects(AllPageContents(path)[0]).First(t => TextOf(t).Contains("艺术"));
        var (ctm, bx, by) = Geom(txt);
        Assert.Equal(0.5, ctm[0], 3);
        Assert.Equal(0.0, ctm[1], 3);
        Assert.Equal(0.0, ctm[2], 3);
        Assert.Equal(2.0, ctm[3], 3);
        Assert.Equal(35.0, ctm[4], 3); // 30 + 5
        Assert.Equal(47.0, ctm[5], 3); // 40 + 7
        // 局部原点随 CTM 变换：x(0)*0.5 + y(3)*2 + 47 = 53
        var (ax, ay) = Abs(ctm, bx, by);
        Assert.Equal(35, ax);
        Assert.Equal(53, ay);
    }

    /// <summary>字间距：逐字符拆分为多个 TextObject，相邻字符间距 = 字宽 + 字间距。</summary>
    [Fact]
    public void ArtWord_LetterSpacing_SplitsIntoCharObjects()
    {
        var path = System.IO.Path.Combine(OutDir(), "artword_spacing.ofd");
        using (var doc = new OFDDoc(path))
        {
            var art = new ArtWord(80, 20).SetPosition(30, 40)
                .SetText("AB").SetFontSize(3).SetLetterSpacing(1.5);
            doc.Add(new Canvas(80, 20, art));
            doc.Close();
        }

        var objs = TextObjects(AllPageContents(path)[0]).Where(t => TextOf(t) is "A" or "B").ToList();
        Assert.Equal(2, objs.Count);
        var a = objs.First(t => TextOf(t) == "A");
        var b = objs.First(t => TextOf(t) == "B");
        // 局部 x：A=0，B=1.8(字宽)+1.5(间距)=3.3
        Assert.Equal(0, Geom(a).Bx, 3);
        Assert.Equal(3.3, Geom(b).Bx, 3);
    }

    /// <summary>加粗/斜体写入 TextObject Bold/Italic 属性。</summary>
    [Fact]
    public void ArtWord_BoldItalic_WritesAttributes()
    {
        var path = System.IO.Path.Combine(OutDir(), "artword_bold.ofd");
        using (var doc = new OFDDoc(path))
        {
            var art = new ArtWord(80, 20).SetPosition(30, 40)
                .SetText("艺术").SetFontSize(3).SetBold(true).SetItalic(true);
            doc.Add(new Canvas(80, 20, art));
            doc.Close();
        }

        var txt = TextObjects(AllPageContents(path)[0]).First(t => TextOf(t).Contains("艺术"));
        Assert.Equal("true", (string?)txt.Attribute("Bold"));
        Assert.Equal("true", (string?)txt.Attribute("Italic"));
    }

    /// <summary>下划线：产生一段描边 PathObject（或至少记录线段）。</summary>
    [Fact]
    public void ArtWord_Underline_EmitsUnderlinePath()
    {
        var path = System.IO.Path.Combine(OutDir(), "artword_underline.ofd");
        using (var doc = new OFDDoc(path))
        {
            var art = new ArtWord(80, 20).SetPosition(30, 40)
                .SetText("艺术").SetFontSize(5).SetUnderline(true);
            doc.Add(new Canvas(80, 20, art));
            doc.Close();
        }

        var content = AllPageContents(path)[0];
        // 含下划线 PathObject：一条 L 段描边路径（AbbreviatedData 为子元素）
        Assert.Contains(content.Descendants(), e => e.Name.LocalName == "PathObject"
            && ((string?)e.Attribute("Stroke") == "true")
            && (e.Descendants().FirstOrDefault(d => d.Name.LocalName == "AbbreviatedData")?.Value ?? "").Contains('L'));
    }
}
