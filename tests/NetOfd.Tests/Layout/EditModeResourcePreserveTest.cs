using System.IO.Compression;
using NetOfd.Layout;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 编辑模式保存的「二进制资源保全」独立回归测试（不依赖 OFD-H 链路）。
/// <para>
/// 历史缺陷：<c>VirtualContainer.HydrateFromPhysical()</c> 补二进制文件时用了只读不缓存的
/// <c>GetRaw()</c>，而 <c>WriteToZip()</c> 只遍历 <c>RawFiles</c> 写二进制 →
/// 编辑模式保存后，字体/图像/附件/签名值等二进制资源被**整体丢弃**（静默数据损坏：
/// 保存成功、文件可打开、资源却没了）。此处以「二进制附件」为探针锁死该行为。
/// </para>
/// </summary>
public class EditModeResourcePreserveTest
{
    private const string AttachName = "探针.bin";
    private const string AttachName2 = "探针二.bin";

    // ── 辅助 ─────────────────────────────────────────────────────────────

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_editmode_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>构造一段确定性的二进制负载（伪 PNG 头 + 填充）。</summary>
    private static byte[] MakeBinaryPayload(byte seed)
    {
        var data = new byte[512];
        data[0] = 0x89; data[1] = 0x50; data[2] = 0x4E; data[3] = 0x47;   // PNG 魔数
        for (int i = 4; i < data.Length; i++) data[i] = (byte)(i * 7 + seed);
        return data;
    }

    /// <summary>生成一个带二进制附件的基础 OFD 文档。</summary>
    private static (byte[] Ofd, byte[] Payload) BuildOfdWithAttachment()
    {
        var dir = TempDir();
        var binPath = Path.Combine(dir, "probe.bin");
        var payload = MakeBinaryPayload(1);
        File.WriteAllBytes(binPath, payload);

        var basePath = Path.Combine(dir, "base.ofd");
        using (var doc = new OFDDoc(basePath))
        {
            doc.Add(new Paragraph().AddText("基础文档正文").SetFontSize(9d));
            doc.Close();
        }

        var bytes = OfdAttacher.AddAttachment(
            File.ReadAllBytes(basePath),
            new Attachment(AttachName, binPath));

        return (bytes, payload);
    }

    /// <summary>列出 OFD 包内条目名（保持 ZIP 物理顺序）。</summary>
    private static List<string> EntryNamesOf(byte[] ofdBytes)
    {
        var names = new List<string>();
        using var za = new ZipArchive(new MemoryStream(ofdBytes), ZipArchiveMode.Read);
        foreach (var e in za.Entries) names.Add(e.FullName);
        return names;
    }

    // ── 用例 ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 编辑模式追加流式内容后保存：二进制附件不得丢失，字节须与源完全一致。
    /// </summary>
    [Fact]
    public void 编辑模式保存_二进制附件不得丢失()
    {
        var (ofd, payload) = BuildOfdWithAttachment();

        var outPath = Path.Combine(TempDir(), "edited_append.ofd");
        using (var doc = OFDDoc.OpenEdit(ofd))
        {
            doc.Add(new Paragraph().AddText("编辑追加的内容").SetFontSize(9d));
            doc.Save(outPath);
        }
        var saved = File.ReadAllBytes(outPath);

        // ① 原包条目一个不少
        var savedNames = EntryNamesOf(saved);
        foreach (var n in EntryNamesOf(ofd))
            Assert.Contains(n, savedNames);

        // ② 附件字节与源一致
        using var reader = new OFDReader(new MemoryStream(saved));
        var got = reader.GetAttachmentFile(AttachName);
        Assert.NotNull(got);
        Assert.Equal(payload, got);

        // ③ 追加内容确实写入
        Assert.Contains("编辑追加的内容", reader.GetAllText());
        Assert.Contains("基础文档正文", reader.GetAllText());
    }

    /// <summary>
    /// 编辑模式插入固定版式页后保存：二进制资源同样不得丢失。
    /// </summary>
    [Fact]
    public void 编辑模式插入虚拟页后保存_二进制资源不得丢失()
    {
        var (ofd, payload) = BuildOfdWithAttachment();

        var outPath = Path.Combine(TempDir(), "edited_insert.ofd");
        using (var doc = OFDDoc.OpenEdit(ofd))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            vPage.Add(new Paragraph()
                .AddText("插入的固定版式页")
                .SetFontSize(9d)
                .SetPosition(Position.Absolute)
                .SetXY(20, 30)
                .SetWidth(150));
            vPage.SetPageNum(1);
            doc.AddVPage(vPage);
            doc.Save(outPath);
        }
        var saved = File.ReadAllBytes(outPath);

        var savedNames = EntryNamesOf(saved);
        foreach (var n in EntryNamesOf(ofd))
            Assert.Contains(n, savedNames);

        using var reader = new OFDReader(new MemoryStream(saved));
        Assert.Equal(payload, reader.GetAttachmentFile(AttachName));
        Assert.Equal(2, reader.GetPageCount());
        Assert.Contains("插入的固定版式页", reader.GetPageText(0));
    }

    /// <summary>
    /// 连续多轮「打开 → 编辑 → 保存」：每一轮都不应消耗掉既有二进制资源。
    /// </summary>
    [Fact]
    public void 编辑模式连续两轮保存_资源仍不丢失()
    {
        var (ofd, payload) = BuildOfdWithAttachment();
        var originalNames = EntryNamesOf(ofd);

        var current = ofd;
        for (int round = 1; round <= 2; round++)
        {
            using var doc = OFDDoc.OpenEdit(current);
            doc.Add(new Paragraph().AddText($"第 {round} 轮追加").SetFontSize(9d));
            var ms = new MemoryStream();
            doc.Save(ms);
            current = ms.ToArray();

            // 每轮都校验：原包条目仍在 + 附件字节一致
            var names = EntryNamesOf(current);
            foreach (var n in originalNames)
                Assert.Contains(n, names);

            using var reader = new OFDReader(new MemoryStream(current));
            Assert.Equal(payload, reader.GetAttachmentFile(AttachName));
        }

        using var finalReader = new OFDReader(new MemoryStream(current));
        Assert.Contains("第 1 轮追加", finalReader.GetAllText());
        Assert.Contains("第 2 轮追加", finalReader.GetAllText());
    }

    /// <summary>
    /// 带**两个**二进制附件时，两者都不得丢失（防止只保住第一个）。
    /// </summary>
    [Fact]
    public void 编辑模式保存_多个二进制附件均不得丢失()
    {
        var dir = TempDir();
        var p1 = Path.Combine(dir, "probe1.bin");
        var p2 = Path.Combine(dir, "probe2.bin");
        var d1 = MakeBinaryPayload(1);
        var d2 = MakeBinaryPayload(2);
        File.WriteAllBytes(p1, d1);
        File.WriteAllBytes(p2, d2);

        var basePath = Path.Combine(dir, "base_multi.ofd");
        using (var doc = new OFDDoc(basePath))
        {
            doc.Add(new Paragraph().AddText("基础文档").SetFontSize(9d));
            doc.Close();
        }
        var bytes = OfdAttacher.AddAttachment(
            File.ReadAllBytes(basePath), new Attachment(AttachName, p1));
        bytes = OfdAttacher.AddAttachment(
            bytes, new Attachment(AttachName2, p2));

        var outPath = Path.Combine(dir, "edited_multi.ofd");
        using (var doc = OFDDoc.OpenEdit(bytes))
        {
            doc.Add(new Paragraph().AddText("追加").SetFontSize(9d));
            doc.Save(outPath);
        }
        var saved = File.ReadAllBytes(outPath);

        using var reader = new OFDReader(new MemoryStream(saved));
        Assert.Equal(d1, reader.GetAttachmentFile(AttachName));
        Assert.Equal(d2, reader.GetAttachmentFile(AttachName2));
    }
}
