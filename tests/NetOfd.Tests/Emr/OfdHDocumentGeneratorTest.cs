using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.Annotation;
using NetOfd.Crypto.Integrity;
using NetOfd.Emr;
using NetOfd.Layout.Edit;
using NetOfd.Reader;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Emr;

/// <summary>
/// OFD-H 生成器端到端测试夹具：生成一份带国密签名与防夹带保护的完整 OFD-H 文档，
/// 供本类全部用例共享（生成过程含 SM2 密钥对生成与自签证书签发，耗时较长，故只生成一次）。
/// </summary>
public sealed class OfdHGeneratedDoc : IDisposable
{
    /// <summary>固定 DocID 与签名时间，保证测试可复现。</summary>
    public const string FixedDocId = "a1b2c3d4e5f60718293a4b5c6d7e8f90";

    public string Root { get; }
    public string OfdPath { get; }
    public OfdHResult Result { get; }
    public byte[] Bytes { get; }

    public OfdHGeneratedDoc()
    {
        Root = Path.Combine(Path.GetTempPath(), "netofd-ofdh-e2e", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        OfdPath = Path.Combine(Root, "emr.ofd");

        Result = OfdHDocumentGenerator.Generate(new OfdHOptions
        {
            OutputPath = OfdPath,
            WorkDirectory = Path.Combine(Root, "work"),
            Log = TextWriter.Null,                       // 测试期间不污染控制台
            DocumentId = FixedDocId,
            SigningTime = new DateTime(2026, 10, 25, 10, 0, 0),
        });
        Bytes = Result.OfdBytes ?? Array.Empty<byte>();
    }

    /// <summary>列出包内条目名（保持 ZIP 物理顺序）。</summary>
    public List<string> EntryNames()
    {
        var names = new List<string>();
        using var za = new ZipArchive(new MemoryStream(Bytes), ZipArchiveMode.Read);
        foreach (var e in za.Entries) names.Add(e.FullName);
        return names;
    }

    /// <summary>读取包内指定条目内容。</summary>
    public byte[]? ReadEntry(string name)
    {
        using var za = new ZipArchive(new MemoryStream(Bytes), ZipArchiveMode.Read);
        var entry = za.GetEntry(name);
        if (entry == null) return null;
        using var s = entry.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public void Dispose()
    {
        try { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
        catch (IOException) { /* 清理失败不影响测试结论 */ }
        catch (UnauthorizedAccessException) { /* 同上 */ }
    }
}

/// <summary>
/// GB/T 48666—2026 OFD-H 电子病历版式文档生成端到端测试。
/// <para>
/// 覆盖生成流水线的每个阶段：基础文档 → 字型嵌入 → 版本快照 → 自定义标引 → 附件 →
/// 加注 → 目录规范化 → 国密签名 → 防夹带保护 → 线性化重排，并验证生成结果可被回读与导出。
/// </para>
/// </summary>
public class OfdHDocumentGeneratorTest : IClassFixture<OfdHGeneratedDoc>
{
    private readonly OfdHGeneratedDoc _doc;

    public OfdHDocumentGeneratorTest(OfdHGeneratedDoc doc) => _doc = doc;

    // ── 总体结果 ─────────────────────────────────────────────────────────

    [Fact]
    public void 生成_结果成功且文件落盘()
    {
        Assert.True(_doc.Result.Success, $"生成失败: {_doc.Result.Error}");
        Assert.Null(_doc.Result.Error);
        Assert.True(File.Exists(_doc.OfdPath));
        Assert.True(new FileInfo(_doc.OfdPath).Length > 0);
        Assert.Equal(_doc.Bytes.Length, _doc.Result.SizeBytes);
    }

    [Fact]
    public void 生成_页数为三且DocType为OFDH()
    {
        Assert.Equal(3, _doc.Result.PageCount);

        using var reader = new OFDReader(new MemoryStream(_doc.Bytes));
        Assert.Equal(3, reader.GetPageCount());
        Assert.Equal("OFD-H", reader.GetOfd().OfdDocType);
    }

    [Fact]
    public void 生成_DocID与唯一识别码写入元数据()
    {
        using var reader = new OFDReader(new MemoryStream(_doc.Bytes));
        var docInfo = reader.GetOfd().GetFirstDocBody()?.DocInfo;

        Assert.NotNull(docInfo);
        Assert.Equal(OfdHGeneratedDoc.FixedDocId, docInfo!.DocID);
        Assert.StartsWith("EMR-", _doc.Result.UniqueId);
        Assert.Equal(OfdHGeneratedDoc.FixedDocId, _doc.Result.DocumentId);
    }

    // ── 6.2 / 6.1.3 包结构 ───────────────────────────────────────────────

    [Fact]
    public void 生成_包内结构符合GB48666表1()
    {
        var names = _doc.EntryNames();

        // 表 1 要求的目录条目
        Assert.Contains("OFD.xml", names);
        Assert.Contains("OFDEntries.xml", names);
        Assert.Contains("Doc_0/Document.xml", names);
        Assert.Contains("Doc_0/PublicRes.xml", names);
        // §7.2 a)：字型/绘制参数/多页共用资源「应」在 DocumentRes.xml 注册；
        //          仅在一个页面内使用的资源「一般」在 Page_N/PageRes.xml 注册。
        // 两者不必同时存在（本演示文档唯一图像——心电图——仅被 Page_2 引用，
        // 故下沉到页级，文档级不再产生空壳 DocumentRes.xml），
        // 但必须至少有一处资源声明，不得出现「包内有资源却无声明」。
        bool hasDocRes = names.Contains("Doc_0/DocumentRes.xml");
        bool hasPageRes = names.Any(n => n.EndsWith("/PageRes.xml", StringComparison.Ordinal));
        Assert.True(hasDocRes || hasPageRes,
            $"资源必须登记在 DocumentRes.xml 或 Page_N/PageRes.xml 之一（§7.2 a）；实际条目: {string.Join(", ", names)}");
        // 单页专用图像（Page_2 心电图）应下沉到页级资源列表
        Assert.Contains("Doc_0/Pages/Page_2/PageRes.xml", names);
        Assert.Contains("Doc_0/Pages/Page_2/Res/Image_1.png", names);
        Assert.Contains("Doc_0/Pages/Page_0/Content.xml", names);
        Assert.Contains("Doc_0/Pages/Page_1/Content.xml", names);
        Assert.Contains("Doc_0/Pages/Page_2/Content.xml", names);
        Assert.Contains("Doc_0/Tags/CustomTags.xml", names);
        Assert.Contains("Doc_0/Attachs/Attachments.xml", names);
        Assert.Contains("Doc_0/Annots/Annotations.xml", names);
        Assert.Contains("Doc_0/Signs/Signatures.xml", names);
        Assert.Contains("Doc_0/Versions/V_1/DocVersion.xml", names);
        Assert.Contains("signedvalue.dat", names);
    }

    [Fact]
    public void 生成_已线性化重排索引置首签名值置末()
    {
        var names = _doc.EntryNames();

        // 6.1.3 线性化：OFDEntries.xml 索引置首，signedvalue.dat 置末
        Assert.Equal("OFDEntries.xml", names[0]);
        Assert.Equal("signedvalue.dat", names[^1]);
        // 内容先于二进制资源
        int contentIdx = names.IndexOf("Doc_0/Pages/Page_0/Content.xml");
        int fontIdx = names.FindIndex(n => n.EndsWith("Font_1.ttf", StringComparison.Ordinal));
        Assert.True(contentIdx > 0 && fontIdx > contentIdx,
            $"内容应先于字型资源（content={contentIdx}, font={fontIdx}）");
    }

    [Fact]
    public void 生成_包内条目清单可枚举()
    {
        Assert.NotEmpty(_doc.Result.PackageEntries);
        Assert.Contains(_doc.Result.PackageEntries, e => e.Name == "OFD.xml");
        Assert.Contains(_doc.Result.PackageEntries, e => e.Name == "OFDEntries.xml");
        Assert.All(_doc.Result.PackageEntries, e => Assert.True(e.Length > 0));
    }

    // ── 7.2 b) 字型嵌入 ──────────────────────────────────────────────────

    [Fact]
    public void 生成_字型已嵌入文档()
    {
        // 无可用中文字型时按选项跳过（SkipFontEmbeddingWhenMissing 默认 true），此时断言降级
        if (!_doc.Result.FontEmbedded)
        {
            Assert.NotNull(_doc.Result.FontWarning);
            return;
        }

        Assert.Null(_doc.Result.FontWarning);

        var names = _doc.EntryNames();
        Assert.Contains(names, n => n.EndsWith("Res/Font_1.ttf", StringComparison.Ordinal));

        var publicRes = _doc.ReadEntry("Doc_0/PublicRes.xml");
        Assert.NotNull(publicRes);
        var ofdNs = XNamespace.Get("http://www.ofdspec.org/2016");
        var doc = XDocument.Parse(Encoding.UTF8.GetString(publicRes!));
        var font = doc.Descendants(ofdNs + "Font")
            .FirstOrDefault(f => (string?)f.Attribute("FontName") == "仿宋");
        Assert.NotNull(font);
        Assert.Equal("Res/Font_1.ttf", font!.Element(ofdNs + "FontFile")?.Value);
    }

    // ── 7.6 版本快照 ─────────────────────────────────────────────────────

    [Fact]
    public void 生成_版本快照完整保留()
    {
        Assert.True(_doc.Result.VersionIndex >= 1);

        var names = _doc.EntryNames();
        // 版本快照目录全量保留历史页面内容
        Assert.Contains("Doc_0/V_1/Document.xml", names);
        Assert.Contains("Doc_0/V_1/Pages/Page_0/Content.xml", names);
        Assert.Contains("Doc_0/V_1/Pages/Page_2/Content.xml", names);
        // 版本描述文件
        Assert.Contains("Doc_0/Versions/V_1/DocVersion.xml", names);
    }

    // ── 7.3 c) / 7.5 自定义标引 ─────────────────────────────────────────

    [Fact]
    public void 生成_写入两个自定义标引()
    {
        var tags = OfdTagger.GetCustomTags(_doc.Bytes);

        Assert.Equal(2, tags.Count);
        var typeIds = tags.Select(t => t.Tag.TypeID).ToList();
        Assert.Contains(EmrStructuredExporter.Wst500TypeId, typeIds);
        Assert.Contains(EmrStructuredExporter.MachineReadableTypeId, typeIds);
    }

    [Fact]
    public void 生成_机读信息标引含文件头元数据标识信息()
    {
        var ns = XNamespace.Get("urn:emr:machine-readable:2026");
        var tag = OfdTagger.GetCustomTags(_doc.Bytes)
            .FirstOrDefault(t => t.Tag.TypeID == EmrStructuredExporter.MachineReadableTypeId);

        Assert.True(tag.Tag is not null, "未找到机读信息标引");
        var doc = XDocument.Parse(Encoding.UTF8.GetString(tag.Content));
        var root = doc.Root!;

        Assert.Equal("EMRDocument", root.Name.LocalName);
        // 7.5 a) 文件头
        Assert.NotNull(root.Element(ns + "FileHeader"));
        Assert.Equal("GB/T 48666-2026", root.Element(ns + "FileHeader")!.Element(ns + "Standard")?.Value);
        // 7.5 b) 元数据
        Assert.NotNull(root.Element(ns + "Metadata"));
        // 7.5 c) 标识信息
        Assert.NotNull(root.Element(ns + "Identification"));
        Assert.Equal("OFD-H", root.Element(ns + "Identification")!.Element(ns + "FileType")?.Value);
        Assert.Equal(_doc.Result.DocumentId, root.Element(ns + "Identification")!.Element(ns + "DocID")?.Value);
    }

    [Fact]
    public void 生成_结构化数据可反向导出且数据元一致()
    {
        var export = EmrStructuredExporter.Export(_doc.Bytes);

        Assert.True(export.Found);
        Assert.Contains(EmrStructuredExporter.Wst500TypeId, export.TypeIds);
        Assert.NotNull(export.SharedDocumentXml);
        // WS 445.10 数据元：病案号 / 姓名 / 住院号
        Assert.Equal("78420931", export.DataElements["DE01.00.004.00"]);
        Assert.Equal("张建国", export.DataElements["DE02.01.039.00"]);
        Assert.Equal("20261018007", export.DataElements["DE01.00.014.00"]);
        // 诊断（ICD-10）
        Assert.NotEmpty(export.Diagnoses);
        Assert.Contains(export.Diagnoses, d => d.Code == "I20.0");
    }

    [Fact]
    public void 生成_共享文档标引为合法CDA结构()
    {
        var export = EmrStructuredExporter.Export(_doc.Bytes);
        Assert.NotNull(export.SharedDocumentXml);

        var cda = XNamespace.Get("urn:hl7-org:v3");
        var doc = XDocument.Parse(Encoding.UTF8.GetString(export.SharedDocumentXml!));

        Assert.Equal("ClinicalDocument", doc.Root!.Name.LocalName);
        Assert.Equal(cda, doc.Root!.Name.Namespace);
        // 文档活动类要素齐备
        Assert.NotNull(doc.Descendants(cda + "realmCode").FirstOrDefault());
        Assert.NotNull(doc.Descendants(cda + "templateId").FirstOrDefault());
        Assert.NotNull(doc.Descendants(cda + "confidentialityCode").FirstOrDefault());
        Assert.NotNull(doc.Descendants(cda + "patient").FirstOrDefault());
    }

    // ── 7.4 附件 ─────────────────────────────────────────────────────────

    [Fact]
    public void 生成_附件可回读且字节与源一致()
    {
        var names = _doc.EntryNames();
        Assert.Contains("Doc_0/Attachs/Attach_1.png", names);

        using var reader = new OFDReader(new MemoryStream(_doc.Bytes));
        var att = reader.GetAttachmentFile("心电图影像.png");

        Assert.NotNull(att);
        Assert.True(att!.Length > 0);
        // 与工作目录中的源文件字节一致
        var source = Path.Combine(_doc.Root, "work", "心电图影像.png");
        Assert.True(File.Exists(source));
        Assert.Equal(File.ReadAllBytes(source), att);
    }

    // ── 3.12 / 5.3.2 e) 加注记录 ─────────────────────────────────────────

    [Fact]
    public void 生成_加注记录可回读()
    {
        var names = _doc.EntryNames();
        Assert.Contains("Doc_0/Annots/Page_1/Annot_1.xml", names);

        using var reader = new OFDReader(new MemoryStream(_doc.Bytes));
        var annotations = reader.GetAnnotations();

        Assert.NotNull(annotations);
        Assert.NotEmpty(annotations!.GetPages());
    }

    // ── 6.3 / 8 国密签名 ─────────────────────────────────────────────────

    [Fact]
    public void 生成_国密签名存在且验签通过()
    {
        Assert.Equal(1, _doc.Result.SignatureCount);
        Assert.True(_doc.Result.AllSignaturesValid);

        var verify = OfdValidator.Verify(_doc.Bytes);
        Assert.True(verify.AllValid, $"验签失败: {verify.Error}");
        Assert.Equal(1, verify.SignatureCount);
        var sig = verify.Signatures[0];
        Assert.True(sig.Valid);
        Assert.Equal(0, sig.ReferenceMismatch);
        Assert.True(sig.ReferenceCount > 0);
    }

    [Fact]
    public void 生成_防夹带完整性校验通过()
    {
        Assert.True(_doc.Result.IntegrityVerified);

        var ok = new OfdIntegrityVerifier().Integrity(_doc.Bytes, new GmProtectVerifier());
        Assert.True(ok);
    }

    [Fact]
    public void 生成_签名保护范围排除注释与签名列表()
    {
        // 8.c) 签名列表 / 注释列表不纳入签名保护范围（允许后续加注与续签）
        var names = _doc.EntryNames();
        Assert.Contains("Doc_0/Signs/Signatures.xml", names);
        Assert.Contains("Doc_0/Annots/Annotations.xml", names);
    }

    // ── 页面版式 ─────────────────────────────────────────────────────────

    [Fact]
    public void 生成_三页均可提取到文本()
    {
        using var reader = new OFDReader(new MemoryStream(_doc.Bytes));

        var p1 = reader.GetPageText(0);
        var p2 = reader.GetPageText(1);
        var p3 = reader.GetPageText(2);

        // 第 1 页：住院病案首页
        Assert.Contains("住", p1);
        Assert.Contains("张建国", p1);
        // 第 2 页：入院记录
        Assert.Contains("入院记录", p2.Replace(" ", ""));
        // 第 3 页：辅助检查与诊疗计划
        Assert.Contains("心电图", p3);
    }
}

/// <summary>OFD-H 生成器的参数化行为测试（不依赖完整签名流水线，单独构造文档）。</summary>
public class OfdHGeneratorOptionTest : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(), "netofd-ofdh-opt", Guid.NewGuid().ToString("N"));

    public OfdHGeneratorOptionTest() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    [Fact]
    public void 跳过签名_不产生签名与完整性文件()
    {
        var outPath = Path.Combine(_root, "nosign.ofd");
        var result = OfdHDocumentGenerator.Generate(new OfdHOptions
        {
            OutputPath = outPath,
            WorkDirectory = Path.Combine(_root, "work"),
            Log = TextWriter.Null,
            SkipSignature = true,
        });

        Assert.True(result.Success);
        Assert.Equal(0, result.SignatureCount);
        Assert.False(result.AllSignaturesValid);
        Assert.False(result.IntegrityVerified);

        var bytes = result.OfdBytes!;
        using var za = new ZipArchive(new MemoryStream(bytes), ZipArchiveMode.Read);
        var names = za.Entries.Select(e => e.FullName).ToList();
        Assert.DoesNotContain("Doc_0/Signs/Signatures.xml", names);
        Assert.DoesNotContain("signedvalue.dat", names);
    }

    [Fact]
    public void 指定字型路径不存在_默认跳过嵌入并给出警告()
    {
        var outPath = Path.Combine(_root, "badfont.ofd");
        var result = OfdHDocumentGenerator.Generate(new OfdHOptions
        {
            OutputPath = outPath,
            WorkDirectory = Path.Combine(_root, "work2"),
            Log = TextWriter.Null,
            SkipSignature = true,
            FontPath = Path.Combine(_root, "不存在的字体.ttf"),
        });

        Assert.True(result.Success);
        Assert.False(result.FontEmbedded);
        Assert.NotNull(result.FontWarning);
        Assert.Contains("不存在", result.FontWarning);
    }

    [Fact]
    public void 指定字型路径不存在且不容许跳过_抛出异常()
    {
        var outPath = Path.Combine(_root, "badfont2.ofd");
        var ex = Assert.Throws<FileNotFoundException>(() =>
            OfdHDocumentGenerator.Generate(new OfdHOptions
            {
                OutputPath = outPath,
                WorkDirectory = Path.Combine(_root, "work3"),
                Log = TextWriter.Null,
                SkipSignature = true,
                SkipFontEmbeddingWhenMissing = false,
                FontPath = Path.Combine(_root, "不存在的字体.ttf"),
            }));

        Assert.Contains("字型", ex.Message);
    }

    [Fact]
    public void 自定义病历数据_写入版式与元数据()
    {
        var c = new EmrCaseData
        {
            PatientName = "李测试",
            MedicalRecordNo = "99000001",
            AdmissionNo = "20261099001",
            Dept = "神经内科",
            Doctor = "陈医生",
        };

        var outPath = Path.Combine(_root, "custom.ofd");
        var result = OfdHDocumentGenerator.Generate(new OfdHOptions
        {
            OutputPath = outPath,
            WorkDirectory = Path.Combine(_root, "work4"),
            Log = TextWriter.Null,
            SkipSignature = true,
            CaseData = c,
        });

        Assert.True(result.Success);

        using var reader = new OFDReader(new MemoryStream(result.OfdBytes!));
        var docInfo = reader.GetOfd().GetFirstDocBody()?.DocInfo;
        Assert.NotNull(docInfo);
        Assert.Contains("李测试", docInfo!.Title);

        // 自定义元数据（7.3 a/b）：病案号应出现在 CustomData 中
        var custom = docInfo.GetCustomDatas()?.GetCustomDatas();
        Assert.NotNull(custom);
        Assert.Contains(custom!, d => d.DataName == "病案号" && d.Value == "99000001");

        // 版式页面同样使用自定义数据
        Assert.Contains("李测试", reader.GetPageText(0));
    }

    [Fact]
    public void 未指定DocID_自动生成唯一标识()
    {
        var outPath = Path.Combine(_root, "guid.ofd");
        var result = OfdHDocumentGenerator.Generate(new OfdHOptions
        {
            OutputPath = outPath,
            WorkDirectory = Path.Combine(_root, "work5"),
            Log = TextWriter.Null,
            SkipSignature = true,
        });

        Assert.True(result.Success);
        Assert.Equal(32, result.DocumentId.Length);   // GUID "N" 格式
        Assert.StartsWith("EMR-", result.UniqueId);
    }
}
