using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using Xunit;
using Xunit.Abstractions;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 批量编辑会话测试：多操作单次落盘的正确性、与逐次编辑的等价性、性能对照。
/// </summary>
public class EditSessionTest
{
    private readonly ITestOutputHelper _output;

    public EditSessionTest(ITestOutputHelper output) => _output = output;

    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_session_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private class TinyDrawer : IDrawer
    {
        public void Draw(DrawContext ctx)
        {
            ctx.FillStyle = "#202020";
            ctx.Font = "3mm SimSun";
            ctx.FillText("批", 2, 5);
        }
    }

    private static byte[] BuildSampleOfdBytes(int pages)
    {
        var path = Out($"session_base_{pages}.ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            for (int i = 0; i < pages; i++) doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("会话测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    [Fact]
    public void BatchOps_ShouldApplyAllInOneWrite()
    {
        var baseDoc = BuildSampleOfdBytes(3);
        var attFile = Out("session_att.bin");
        File.WriteAllBytes(attFile, new byte[] { 9, 9 });

        using (var session = OfdEditSession.Open(baseDoc))
        {
            Assert.Equal(3, session.PageCount);

            for (int p = 1; p <= 3; p++)
                session.AddAnnotation(p, new Annotation(5, 5, 30, 14, AnnotType.Path,
                    new TinyDrawer()));

            session.AppendPage();
            session.AddAttachment(new Attachment("会话附件", attFile));
            session.AddCustomTag("urn:session", "<T/>"u8.ToArray());
            Assert.Equal(1, session.AddVersion("会话版本"));

            // 会话内查询应即时反映已做修改
            Assert.Equal(4, session.PageCount);
            Assert.Single(session.GetCustomTags());
            Assert.Single(session.GetVersions());

            var saved = session.Save();
            Assert.NotNull(ReadEntry(saved, "Doc_0/Annots/Page_2/Annot_1.xml"));
            Assert.NotNull(ReadEntry(saved, "Doc_0/Attachments.xml"));
            Assert.NotNull(ReadEntry(saved, "Doc_0/CustomTags.xml"));
            Assert.NotNull(ReadEntry(saved, "Doc_0/Versions/V_1/DocVersion.xml"));
        }
    }

    [Fact]
    public void Session_ShouldMatchSequentialEditors()
    {
        var a = BuildSampleOfdBytes(2);
        var b = BuildSampleOfdBytes(2);
        var ann = new Func<int, Annotation>(i =>
            new Annotation(i, i, 20 + i, 12, AnnotType.Path, new TinyDrawer()));

        // 逐次编辑
        var stepwise = OfdAnnotator.AddAnnotation(a, 1, ann(1));
        stepwise = OfdAnnotator.AddAnnotation(stepwise, 2, ann(2));

        // 会话编辑
        using (var session = OfdEditSession.Open(b))
        {
            session.AddAnnotation(1, ann(1)).AddAnnotation(2, ann(2));
            b = session.Save();
        }

        Assert.Equal(
            ReadEntry(stepwise, "Doc_0/Annots/Page_1/Annot_1.xml"),
            ReadEntry(b, "Doc_0/Annots/Page_1/Annot_1.xml"));
        Assert.Equal(
            ReadEntry(stepwise, "Doc_0/Annots/Page_2/Annot_1.xml"),
            ReadEntry(b, "Doc_0/Annots/Page_2/Annot_1.xml"));
        Assert.Equal(
            GetMaxUnitId(ReadEntry(stepwise, "Doc_0/Document.xml")),
            GetMaxUnitId(ReadEntry(b, "Doc_0/Document.xml")));
    }

    [Fact]
    public void Session_BatchAnnotations_FasterThanStepwise()
    {
        const int pages = 40;
        var forStepwise = BuildSampleOfdBytes(pages);
        var forSession = BuildSampleOfdBytes(pages);
        static Annotation Make(int p) => new(10, 10, 40, 16, AnnotType.Path, new TinyDrawer());

        var sw = Stopwatch.StartNew();
        var d = forStepwise;
        for (int p = 1; p <= pages; p++) d = OfdAnnotator.AddAnnotation(d, p, Make(p));
        sw.Stop();
        var stepMs = sw.ElapsedMilliseconds;

        sw.Restart();
        using (var session = OfdEditSession.Open(forSession))
        {
            for (int p = 1; p <= pages; p++) session.AddAnnotation(p, Make(p));
            session.Save();
        }
        sw.Stop();
        var sessionMs = sw.ElapsedMilliseconds;

        _output.WriteLine($"[PERF] {pages} 页批注: 逐次 {stepMs} ms | 会话 {sessionMs} ms");
        Assert.True(sessionMs < Math.Max(stepMs, 200),
            $"会话模式应不慢于逐次模式: {sessionMs} vs {stepMs}");
    }

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

    private static int GetMaxUnitId(byte[]? documentXml)
    {
        var el = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(documentXml!))
            .Descendants().First(e => e.Name.LocalName == "CommonData");
        return int.TryParse(el.Attribute("MaxUnitID")?.Value, out var v) ? v : 0;
    }
}
