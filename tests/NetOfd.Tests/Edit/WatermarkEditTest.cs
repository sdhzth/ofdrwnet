using System;
using System.IO.Compression;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Edit;

/// <summary>
/// 增量 N：编辑子系统水印接入 + 会话版 AddStream 对称补全。
/// 对齐 Java edit.Watermark 的"平铺到页面最上层图层"语义（NetOfd 采用绘制型，非注释型）。
/// </summary>
public class WatermarkEditTest
{
    private static byte[] BuildMultiPage(int pages)
    {
        string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"wm_base_{Guid.NewGuid():N}.ofd");
        using (var doc = new OFDDoc(path))
        {
            for (int i = 0; i < pages; i++)
            {
                doc.AddPage();
                doc.Add(new Paragraph($"第{i + 1}页既有内容"));
            }
            doc.Close();
        }
        return System.IO.File.ReadAllBytes(path);
    }

    private static int CountTextObjects(byte[] ofd, int page)
    {
        using var ms = new System.IO.MemoryStream(ofd);
        using var zip = new ZipArchive(ms);
        var entry = zip.GetEntry($"Doc_0/Pages/Page_{page - 1}/Content.xml")
                   ?? throw new InvalidOperationException($"缺少 Page_{page - 1}/Content.xml");
        var doc = XDocument.Load(entry.Open());
        return doc.Descendants().Count(e => e.Name.LocalName == "TextObject");
    }

    [Fact]
    public void AddWatermark_SinglePage_ProducesTiledTextObjects()
    {
        var baseOfd = BuildMultiPage(1);
        var wm = new Watermark(0, 0, 210, 297).SetValue("内部资料").SetColor("#8899BB");
        var outBytes = OfdEditor.AddWatermark(baseOfd, wm, 1);
        // 水印按 IntervalX/Y 平铺，应产生多个 TextObject
        Assert.True(CountTextObjects(outBytes, 1) > 1, "水印应平铺产生多个 TextObject");
    }

    [Fact]
    public void AddWatermarkAllPages_AppliesToEveryPage()
    {
        var baseOfd = BuildMultiPage(3);
        var wm = new Watermark(-30, -30, 210 + 60, 297 + 60).SetValue("机密").SetFontSize(4.5);
        var outBytes = OfdEditor.AddWatermarkAllPages(baseOfd, wm);
        for (int p = 1; p <= 3; p++)
            Assert.True(CountTextObjects(outBytes, p) > 1, $"第{p}页应含水印 TextObject");
    }

    [Fact]
    public void Session_AddWatermark_SinglePage()
    {
        var baseOfd = BuildMultiPage(2);
        using var session = OfdEditSession.Open(baseOfd);
        session.AddWatermark(new Watermark(0, 0, 210, 297).SetValue("校对"), 1);
        var outBytes = session.Save();
        Assert.True(CountTextObjects(outBytes, 1) > 1, "会话版 AddWatermark 应在指定页平铺水印");
    }

    [Fact]
    public void Session_AddStream_AppendsContent()
    {
        var baseOfd = BuildMultiPage(1);
        using var session = OfdEditSession.Open(baseOfd);
        int before = CountTextObjects(session.Save(), 1);
        session.AddStream(new Div[] { new Paragraph("会话追加段落") });
        var outBytes = session.Save();
        Assert.True(CountTextObjects(outBytes, 1) >= before, "会话版 AddStream 应追加内容而不丢失既有图元");
    }
}
