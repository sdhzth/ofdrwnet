using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.Attachment;
using NetOfd.Core.BasicType;
using NetOfd.Layout.Edit;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 附件（Attachment）功能测试：向已有 OFD 文档添加/删除附件。
/// 对应 Java: ofdrw-layout edit.Attachment + OFDDoc#addAttachment
/// </summary>
public class AttachmentTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_attach_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>构建单页样例文档并返回字节。</summary>
    private static byte[] BuildSampleOfdBytes(string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("附件功能测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    /// <summary>创建临时附件文件。</summary>
    private static string MakeFile(string name, byte[] content)
    {
        var path = Out(name);
        File.WriteAllBytes(path, content);
        return path;
    }

    private static byte[]? ReadEntry(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var e = za.GetEntry(entryName);
        if (e == null) return null;
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    private static XDocument EntryXml(byte[] ofd, string entryName)
        => XDocument.Parse(Encoding.UTF8.GetString(ReadEntry(ofd, entryName)!));

    // ──────────────────────────────────────────────────────────────

    /// <summary>添加附件应创建附件列表文件并登记文档入口。</summary>
    [Fact]
    public void AddAttachment_ShouldCreateStructureAndEntry()
    {
        var ofd = BuildSampleOfdBytes("Att_Basic.ofd");
        var fileContent = Encoding.UTF8.GetBytes(new string('A', 2500)); // 约 2.4 KB
        var filePath = MakeFile("合同扫描件.pdf", fileContent);

        var attached = OfdAttacher.AddAttachment(ofd,
            new Attachment("合同扫描件", filePath).SetFormat("PDF").SetUsage("attach"));

        // 附件列表文件存在且登记于 Document.xml
        Assert.NotNull(ReadEntry(attached, "Doc_0/Attachments.xml"));
        var docXml = EntryXml(attached, "Doc_0/Document.xml");
        var attLoc = docXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "Attachments")?.Value;
        Assert.Equal("/Doc_0/Attachments.xml", attLoc);

        // 附件记录内容校验
        var attachments = new Attachments(EntryXml(attached, "Doc_0/Attachments.xml").Root!);
        var att = Assert.Single(attachments.GetAttachments());
        Assert.Equal("合同扫描件", att.Name);
        Assert.Equal("PDF", att.Format);
        Assert.Equal("attach", att.Usage);
        Assert.True(long.TryParse(att.ID, out var id) && id > 0, "附件 ID 应为正整数");
        Assert.False(string.IsNullOrEmpty(att.CreationDate), "应自动填充创建时间");
        Assert.NotNull(att.FileLoc);
        Assert.Equal(fileContent.Length / 1024d, att.Size!.Value, 3);

        // 附件文件已复制进包内（FileLoc 为相对文档目录路径，读 zip 需补文档目录前缀）
        var fileBytes = ReadEntry(attached, "Doc_0/" + att.FileLoc!.Value);
        Assert.NotNull(fileBytes);
        Assert.Equal(fileContent, fileBytes);

        // MaxUnitID 覆盖附件 ID
        var maxUnit = long.Parse(EntryXml(attached, "OFD.xml")
            .Descendants().First(e => e.Name.LocalName == "MaxUnitID").Value);
        Assert.True(maxUnit >= id, "MaxUnitID 应覆盖附件对象 ID");
        var docMaxUnit = long.Parse(docXml.Descendants()
            .First(e => e.Name.LocalName == "MaxUnitID").Value);
        Assert.True(docMaxUnit >= id, "CommonData MaxUnitID 应覆盖附件对象 ID");
    }

    /// <summary>同名附件默认替换：旧记录与旧文件被清除。</summary>
    [Fact]
    public void AddAttachment_SameName_ShouldReplaceOldFile()
    {
        var ofd = BuildSampleOfdBytes("Att_Replace.ofd");
        var fileA = MakeFile("report_a.bin", new byte[] { 1, 2, 3 });
        var fileB = MakeFile("report_b.bin", new byte[] { 9, 8, 7, 6 });

        var once = OfdAttacher.AddAttachment(ofd, new Attachment("检验报告", fileA));
        var twice = OfdAttacher.AddAttachment(once, new Attachment("检验报告", fileB));

        var attachments = new Attachments(EntryXml(twice, "Doc_0/Attachments.xml").Root!);
        var att = Assert.Single(attachments.GetAttachments());
        Assert.Equal("检验报告", att.Name);

        // 新文件存在、旧文件已被删除（FileLoc 为相对文档目录路径，读 zip 需补文档目录前缀）
        Assert.Equal(File.ReadAllBytes(fileB), ReadEntry(twice, "Doc_0/" + att.FileLoc!.Value));
        Assert.Null(ReadEntry(twice, "Doc_0/Res/report_a.bin"));
    }

    /// <summary>禁用同名替换时两条附件共存。</summary>
    [Fact]
    public void AddAttachment_SameName_DisableReplace_ShouldKeepBoth()
    {
        var ofd = BuildSampleOfdBytes("Att_NoReplace.ofd");
        var fileA = MakeFile("keep_a.bin", new byte[] { 1 });
        var fileB = MakeFile("keep_b.bin", new byte[] { 2 });

        var once = OfdAttacher.AddAttachment(ofd, new Attachment("影像资料", fileA));
        var twice = OfdAttacher.AddAttachment(once,
            new Attachment("影像资料", fileB, disableReplace: true));

        var attachments = new Attachments(EntryXml(twice, "Doc_0/Attachments.xml").Root!);
        Assert.Equal(2, attachments.GetAttachments().Count);
        Assert.NotNull(ReadEntry(twice, "Doc_0/Res/keep_a.bin"));
        Assert.NotNull(ReadEntry(twice, "Doc_0/Res/keep_b.bin"));
    }

    /// <summary>删除附件应同时移除记录与包内文件。</summary>
    [Fact]
    public void DeleteAttachment_ShouldRemoveRecordAndFile()
    {
        var ofd = BuildSampleOfdBytes("Att_Delete.ofd");
        var fileA = MakeFile("del_a.bin", new byte[] { 1, 1 });
        var fileB = MakeFile("del_b.bin", new byte[] { 2, 2 });

        var withA = OfdAttacher.AddAttachment(ofd, new Attachment("病历摘要", fileA));
        var withB = OfdAttacher.AddAttachment(withA,
            new Attachment("检查图像", fileB, disableReplace: true));
        var deleted = OfdAttacher.DeleteAttachment(withB, "病历摘要");

        var attachments = new Attachments(EntryXml(deleted, "Doc_0/Attachments.xml").Root!);
        var remaining = Assert.Single(attachments.GetAttachments());
        Assert.Equal("检查图像", remaining.Name);
        Assert.Null(ReadEntry(deleted, "Doc_0/Res/del_a.bin"));
        Assert.NotNull(ReadEntry(deleted, "Doc_0/Res/del_b.bin"));
    }

    /// <summary>删除不存在的附件或无附件文档时应忽略。</summary>
    [Fact]
    public void DeleteAttachment_NotExists_ShouldIgnore()
    {
        var ofd = BuildSampleOfdBytes("Att_Ignore.ofd");

        // 无附件列表文件的文档：删除操作不产生副作用
        var untouched = OfdAttacher.DeleteAttachment(ofd, "不存在");
        Assert.Null(ReadEntry(untouched, "Doc_0/Attachments.xml"));
        Assert.Null(EntryXml(untouched, "Doc_0/Document.xml").Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "Attachments"));

        // 有附件但名称不匹配：记录保持不变
        var file = MakeFile("ignore.bin", new byte[] { 3 });
        var attached = OfdAttacher.AddAttachment(ofd, new Attachment("手术记录", file));
        var after = OfdAttacher.DeleteAttachment(attached, "不存在");
        var attachments = new Attachments(EntryXml(after, "Doc_0/Attachments.xml").Root!);
        Assert.Single(attachments.GetAttachments());
        Assert.NotNull(ReadEntry(after, "Doc_0/Res/ignore.bin"));
    }

    /// <summary>附件构造器属性序列化往返。</summary>
    [Fact]
    public void Attachment_ModelRoundtrip()
    {
        var file = MakeFile("roundtrip.bin", new byte[] { 4, 5, 6 });
        var builder = new Attachment("出院小结", file)
            .SetFormat("DOCX")
            .SetCreationDate(new DateTime(2026, 8, 21, 10, 30, 0))
            .SetModDate(new DateTime(2026, 8, 21, 11, 0, 0))
            .SetVisible(false)
            .SetUsage("archive");

        var ct = builder.GetAttachment();
        ct.ID = "66";
        ct.Size = 1024;
        ct.FileLoc = new NetOfd.Core.BasicType.STLoc("Doc_0/Res/roundtrip.bin");

        var parsed = new CTAttachment(XDocument.Parse(
            Encoding.UTF8.GetString(OfdAnnotator.ToUtf8Bytes(ct.Element))).Root!);

        Assert.Equal("66", parsed.ID);
        Assert.Equal("出院小结", parsed.Name);
        Assert.Equal("DOCX", parsed.Format);
        Assert.Equal("2026-08-21 10:30:00", parsed.CreationDate);
        Assert.Equal("2026-08-21 11:00:00", parsed.ModDate);
        Assert.False(parsed.Visible);
        Assert.Equal("archive", parsed.Usage);
        Assert.Equal(1024, parsed.Size!.Value);
        Assert.Equal("Doc_0/Res/roundtrip.bin", parsed.FileLoc!.Value);
    }

    /// <summary>
    /// 附件 摘要/替代（Java 未实现，NetOfd 自行扩展）：
    /// 经 layout 附件 API（Attachment.SetSummary/SetSubstitutional）设置后，
    /// OfdAttacher 写入的 Attachments.xml 应保留这两个字段，且模型可正确读回。
    /// </summary>
    [Fact]
    public void AddAttachment_WithSummaryAndSubstitutional_ShouldPersist()
    {
        var ofd = BuildSampleOfdBytes("Att_Summary.ofd");
        var filePath = MakeFile("影像报告.pdf", Encoding.UTF8.GetBytes("scan"));

        var attached = OfdAttacher.AddAttachment(ofd,
            new Attachment("影像报告", filePath)
                .SetFormat("PDF")
                .SetSummary("2026-08-10 胸部CT，未见明显异常")
                .SetSubstitutional("2"));

        var attachments = new Attachments(EntryXml(attached, "Doc_0/Attachments.xml").Root!);
        var att = Assert.Single(attachments.GetAttachments());
        Assert.Equal("2026-08-10 胸部CT，未见明显异常", att.Summary);
        Assert.NotNull(att.Substitutional);
        Assert.Equal(2L, att.Substitutional!.Value);
    }

    /// <summary>
    /// 附件 摘要/替代 经 OFDReader 读回往返校验（验证读取侧模型兼容）。
    /// </summary>
    [Fact]
    public void AddAttachment_SummaryAndSubstitutional_ShouldRoundTripViaReader()
    {
        var ofd = BuildSampleOfdBytes("Att_Reader_src.ofd");
        var filePath = MakeFile("病历附件.txt", Encoding.UTF8.GetBytes("emr"));
        var attached = OfdAttacher.AddAttachment(ofd,
            new Attachment("病历附件", filePath)
                .SetSummary("门诊病历摘要")
                .SetSubstitutional("5"));

        var ofdPath = Out("Att_Reader.ofd");
        File.WriteAllBytes(ofdPath, attached);

        using var reader = new OFDReader(ofdPath);
        var list = reader.GetAttachmentList();
        var att = Assert.Single(list);
        Assert.Equal("门诊病历摘要", att.Summary);
        Assert.NotNull(att.Substitutional);
        Assert.Equal(5L, att.Substitutional!.Value);
    }
}
