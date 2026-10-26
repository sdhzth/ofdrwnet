using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Emr;
using NetOfd.Layout;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element;
using NetOfd.Reader;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Emr;

/// <summary>
/// OFD-H 电子病历文档 + 固定版式页「插入到指定页码」组合场景测试。
/// <para>
/// 验证 v1.3.1 新增的 <c>VirtualPage.SetPageNum</c> 插入语义在 OFD-H 链路上同样可用：
/// 插入后页序正确、原三页内容完整对应、OFD-H 专有要素（DocType / 机读标引 / 附件）不受影响；
/// 同时确认「文档内容被改动后，原国密签名不再通过」这一正确行为。
/// </para>
/// </summary>
public class OfdHPageInsertTest : IClassFixture<OfdHGeneratedDoc>
{
    private readonly OfdHGeneratedDoc _doc;

    public OfdHPageInsertTest(OfdHGeneratedDoc doc) => _doc = doc;

    /// <summary>列出 OFD 包内条目名（保持 ZIP 物理顺序）。</summary>
    private static List<string> EntryNamesOf(byte[] ofdBytes)
    {
        var names = new List<string>();
        using var za = new ZipArchive(new MemoryStream(ofdBytes), ZipArchiveMode.Read);
        foreach (var e in za.Entries) names.Add(e.FullName);
        return names;
    }

    /// <summary>
    /// 在已生成（含签名）的 OFD-H 文档中，把一个固定版式的「附页」插入到第 2 页。
    /// </summary>
    [Fact]
    public void OFDH文档_插入固定版式页到指定页码_页序与要素均正确()
    {
        const string insertText = "附页：检验单";

        // 先记录原文档三页的文本（不硬编码内容，避免与生成器文案耦合）
        List<string> originalTexts;
        using (var origReader = new OFDReader(new MemoryStream(_doc.Bytes)))
        {
            Assert.Equal(3, origReader.GetPageCount());
            originalTexts = Enumerable.Range(0, 3).Select(i => origReader.GetPageText(i)).ToList();
        }

        // 打开既有 OFD-H 文档（编辑模式）
        using var doc = OFDDoc.OpenEdit(_doc.Bytes);

        var vPage = new VirtualPage(PageLayout.A4());
        vPage.Add(new Paragraph()
            .AddText(insertText)
            .SetFontSize(9d)
            .SetPosition(Position.Absolute)
            .SetXY(25, 30)
            .SetWidth(150));
        vPage.SetPageNum(2);          // 插入到第 2 页
        doc.AddVPage(vPage);

        var outPath = Path.Combine(_doc.Root, "emr_with_inserted_page.ofd");
        doc.Save(outPath);
        var bytes = File.ReadAllBytes(outPath);

        // ── 1. 页数与页序 ────────────────────────────────────────────────
        using var reader = new OFDReader(new MemoryStream(bytes));
        Assert.Equal(4, reader.GetPageCount());

        var newTexts = Enumerable.Range(0, 4).Select(i => reader.GetPageText(i)).ToList();

        // 插入页落在第 2 位
        Assert.Contains(insertText, newTexts[1]);
        // 其余三页与原文档三页一一对应（位置后移：0→0, 1→2, 2→3）
        Assert.Equal(originalTexts[0], newTexts[0]);
        Assert.Equal(originalTexts[1], newTexts[2]);
        Assert.Equal(originalTexts[2], newTexts[3]);
        // 插入页不应污染其它页
        Assert.DoesNotContain(insertText, newTexts[0]);
        Assert.DoesNotContain(insertText, newTexts[2]);
        Assert.DoesNotContain(insertText, newTexts[3]);

        // 虚拟页回填了实际页码
        Assert.Equal(2, vPage.PageNum);

        // ── 2. OFD-H 专有要素不受影响 ───────────────────────────────────
        Assert.Equal("OFD-H", reader.GetOfd().OfdDocType);

        var mrTag = OfdTagger.GetCustomTags(bytes)
            .FirstOrDefault(t => t.Tag.TypeID == EmrStructuredExporter.MachineReadableTypeId);
        Assert.True(mrTag.Tag is not null, "插入页面后机读信息标引丢失");
        var mrDoc = XDocument.Parse(Encoding.UTF8.GetString(mrTag.Content!));
        Assert.Equal("OFD-H", mrDoc.Root!.Element(XNamespace.Get("urn:emr:machine-readable:2026") + "Identification")!
            .Element(XNamespace.Get("urn:emr:machine-readable:2026") + "FileType")?.Value);

        var attachment = reader.GetAttachmentFile("心电图影像.png");
        Assert.NotNull(attachment);
        Assert.True(attachment!.Length > 0);

        // 编辑模式保存不得丢失原包中的任何条目
        // （曾因 GetRaw 只读不缓存 → 二进制资源未进 RawFiles，保存时被整体丢弃：
        //   字体 Font_FangSong.ttf、图像 8.png、附件 Attach_1.png、签名值 SignedValue.dat 全部消失）
        var newNames = EntryNamesOf(bytes);
        foreach (var n in EntryNamesOf(_doc.Bytes))
            Assert.Contains(n, newNames);

        // ── 3. 文档被改动后原签名不再通过（正确行为）──────────────────
        var verify = OfdValidator.Verify(bytes);
        Assert.False(verify.AllValid, "内容改动后原国密签名不应再通过");
    }
}
