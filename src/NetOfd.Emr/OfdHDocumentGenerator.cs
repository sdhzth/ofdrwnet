using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Font;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Layout.Edit;
using NetOfd.Sign;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using SkiaSharp;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Emr;

/// <summary>
/// GB/T 48666—2026《电子病历版式文档技术要求》电子病历 OFD-H 版式文档生成器。
/// <para>
/// 本类是库级 API，可直接被 NuGet 使用方调用：
/// <code>
/// var result = OfdHDocumentGenerator.Generate(new OfdHOptions
/// {
///     OutputPath = "病历.ofd",
///     CaseData   = myCaseData,
/// });
/// </code>
/// 命令行入口见 <c>samples/OfdHWriter</c>。
/// </para>
/// <para>生成流程与标准条款对照：</para>
/// <list type="bullet">
///   <item>6.1.2 单文档承载（Doc_0，OFD 格式）                     → OFDDoc 单文档</item>
///   <item>6.1.3 ZIP 6.2 + Deflate、线性化（OFDEntries.xml 索引）   → RepackLinearized</item>
///   <item>6.2   包内规范目录结构（表 1）                          → NormalizeStructure 规范化</item>
///   <item>7.1a) DocType="OFD-H"                                   → OFDRoot.OfdDocType</item>
///   <item>7.1c) 元数据 DocInfo（患者/责任者/时间/关键词）           → ConfigureDocInfo</item>
///   <item>7.2b) 字型数据嵌入（仿宋 FontFile）                      → EmbedFonts</item>
///   <item>7.2c) 图像资源嵌入（DocumentRes + Res/心电图 PNG）        → Img 元素</item>
///   <item>7.3c) 结构化数据以自定义标引存储                         → AddCustomTag ×2</item>
///   <item>7.4   附件（Attachs/Attachments.xml + Attach_N.png）     → OfdAttacher + 规范化</item>
///   <item>7.5   机读信息（文件头/元数据/标识信息，UTF-8）            → CustomTag 机读信息 XML</item>
///   <item>7.6   版本快照（历史版本全量保留）                        → OfdVersioner</item>
///   <item>3.12/5.3.2e) 加注记录（审核批注）                        → OfdAnnotator</item>
///   <item>6.3/8.c/d/e) SM2+SM3 国密签名（GB/T 35275），
///                保护范围除注释列表、签名列表外全部内容        → OfdSigner + GBT35275DSContainer</item>
///   <item>GM/T 0099 §7.4.3 防夹带/完整性保护                       → OfdIntegrity + GmProtectSigner</item>
/// </list>
/// </summary>
public static class OfdHDocumentGenerator
{
    private const string OfdNs = "http://www.ofdspec.org/2016";
    private const string DocTypeOfdH = "OFD-H";
    private const string MachineReadableTypeId = "urn:emr:gb48666:machine-readable";
    private const string ClinicalDataTypeId = "urn:emr:gb48666:clinical-structured";
    private const string BodyFontFamily = "仿宋";

    /// <summary>
    /// 各平台下可嵌入文档的仿宋/中文字型候选路径（按顺序探测，取第一个存在的文件）。
    /// <para>Windows 为「仿宋」simfang.ttf；Linux 常见为 wqy-zenhei / Noto Serif CJK；macOS 为 STFangsong。</para>
    /// </summary>
    private static readonly string[] FontCandidates =
    {
        @"C:\Windows\Fonts\simfang.ttf",
        @"C:\Windows\Fonts\simsun.ttc",
        "/usr/share/fonts/truetype/wqy/wqy-zenhei.ttc",
        "/usr/share/fonts/opentype/noto/NotoSerifCJK-Regular.ttc",
        "/usr/share/fonts/truetype/arphic/uming.ttc",
        "/System/Library/Fonts/STHeiti Light.ttc",
        "/System/Library/Fonts/Supplemental/Songti.ttc",
    };

    /// <summary>生成一份 GB/T 48666—2026 合规的电子病历版式文档（OFD-H）。</summary>
    /// <param name="options">生成参数，见 <see cref="OfdHOptions"/>。</param>
    /// <returns>生成结果，含签名自检、完整性校验与包内条目清单。</returns>
    public static OfdHResult Generate(OfdHOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var log = options.Log ?? Console.Out;
        var outPath = Path.GetFullPath(options.OutputPath);
        var workDir = options.WorkDirectory ?? Path.Combine(Path.GetDirectoryName(outPath)!, "_emr_work");
        var noSign = options.SkipSignature;
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);

        var c = options.CaseData ?? new EmrCaseData();
        var now = options.SigningTime ?? DateTime.Now;
        var signTime = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var docId = options.DocumentId ?? Guid.NewGuid().ToString("N");
        var uniqueId = "EMR-" + docId.Substring(0, Math.Min(16, docId.Length)).ToUpperInvariant();

        // 字型解析（7.2b）：显式指定优先，否则按平台探测；找不到时按选项决定跳过或抛错
        var (fontPath, fontWarning) = ResolveFont(options);
        var fontEmbedded = false;
        if (fontPath != null)
        {
            fontEmbedded = true;
        }
        else if (!options.SkipFontEmbeddingWhenMissing)
        {
            throw new FileNotFoundException(
                "未找到可嵌入的中文字型文件；请通过 OfdHOptions.FontPath 指定，或安装仿宋/中文字体后重试。");
        }
        log.WriteLine($"═══ GB/T 48666—2026 电子病历版式文档（OFD-H）生成 ═══");
        log.WriteLine($"患者: {c.PatientName}（{c.Sex}，{c.Age} 岁）  病案号: {c.MedicalRecordNo}  科室: {c.Dept}");
        log.WriteLine($"唯一识别码: {uniqueId}");

        // ── 0. 合成心电图检查图像（页面内嵌资源 + 附件共用同一源文件）──────
        var ecgPath = Path.Combine(workDir, "心电图影像.png");
        RenderEcgPng(ecgPath);

        // ── 1. 基础文档（3 页：病案首页 / 入院记录 / 辅助检查与诊疗）──────────
        var stage1 = Path.Combine(workDir, "stage1_base.ofd");
        BuildBaseDocument(stage1, c, signTime, uniqueId, docId, ecgPath);
        var ofd = File.ReadAllBytes(stage1);
        log.WriteLine($"[1/9] 基础文档完成（DocType={DocTypeOfdH}，3 页，元数据已写入 DocInfo）");

        // ── 2. 嵌入字型（7.2b：显示和打印所需全部字型数据包含在文件中）───────
        if (fontPath != null)
        {
            ofd = EmbedFonts(ofd, fontPath, options.FontFamily);
            log.WriteLine($"[2/9] 字型嵌入完成（{options.FontFamily} ← {Path.GetFileName(fontPath)} → Doc_0/PublicRes.xml FontFile + Doc_0/Res/）");
        }
        else
        {
            log.WriteLine($"[2/9] 已跳过字型嵌入（{fontWarning}）");
        }

        // ── 3. 版本快照（7.6：保留历史版本全部内容）────────────────────────
        int versionIndex;
        ofd = OfdVersioner.AddVersion(ofd, "初稿（未加注、未附附件）", out versionIndex);
        log.WriteLine($"[3/9] 版本快照完成（V_{versionIndex} 初稿，全量保留于 Doc_0/V_{versionIndex}/）");

        // ── 4. 自定义标引：WS/T 500 共享文档 + 机读信息（7.1d / 7.3c / 7.5）──
        //    7.3c 要求结构化数据「符合卫生行业现行标准，例如 WS/T 500 系列共享文档规范」，
        //    故以 WS/T 500.32—2016《住院病案首页》共享文档为主标引（标准结构），
        //    另保留机读信息标引（7.5 文件头/元数据/标识信息三要素）。
        var structured = BuildStructuredData(c, signTime, uniqueId);
        ofd = OfdTagger.AddCustomTag(ofd, EmrStructuredExporter.Wst500TypeId,
            Wst500Builder.BuildBytes(structured));
        ofd = OfdTagger.AddCustomTag(ofd, MachineReadableTypeId,
            BuildMachineReadableXml(c, signTime, uniqueId, docId));
        log.WriteLine("[4/9] 自定义标引完成（WS/T 500.32 共享文档 + 机读信息）");

        // ── 5. 内部附件：心电图影像（7.4a/b，非文书类资料）──────────────────
        ofd = OfdAttacher.AddAttachment(ofd,
            new Attachment("心电图影像.png", ecgPath).SetFormat("PNG").SetVisible(true));
        log.WriteLine("[5/9] 内部附件添加完成（心电图影像.png）");

        // ── 6. 加注记录：质控审核批注（3.12 / 5.3.2e / 6.3f）────────────────
        ofd = OfdAnnotator.AddAnnotation(ofd, 1, BuildAuditAnnotation());
        log.WriteLine("[6/9] 加注记录完成（第 1 页『已审核』质控批注）");

        // ── 7. 目录结构规范化（对齐 6.2 表 1）──────────────────────────────
        ofd = NormalizeStructure(ofd);
        log.WriteLine("[7/9] 目录结构规范化完成（Tags/CustomTag_N.xml、Attachs/Attach_N.png、注释绝对路径）");

        byte[] finalOfd;
        if (noSign)
        {
            finalOfd = ofd;
            log.WriteLine("[8/9] 已跳过签名（--no-sign）");
            log.WriteLine("[9/9] 已跳过防夹带保护（--no-sign）");
        }
        else
        {
            // ── 8. SM2+SM3 国密数字签名（GB/T 35275；6.3 / 8.c/d/e）─────────
            // 8.c)：保护范围涵盖除「注释列表、签名列表」外的全部内容
            var keyPair = GmCrypto.GenerateSm2KeyPair();
            var cert = CertUtil.GenerateSelfSignedSm2(options.SignerName, keyPair);
            var container = new GBT35275DSContainer(cert, keyPair);
            finalOfd = OfdSigner.Sign(ofd, container, signTime,
                signMode: SignMode.WholeProtected,
                protectFileFilter: abs =>
                {
                    var n = abs.Replace('\\', '/');
                    // 8.c) 签名列表 / 注释列表不纳入签名保护范围（允许后续加注与续签）
                    if (n.EndsWith("/Signs/Signatures.xml", StringComparison.OrdinalIgnoreCase)) return false;
                    if (n.EndsWith("/Annots/Annotations.xml", StringComparison.OrdinalIgnoreCase)) return false;
                    // 防夹带索引与完整性签名值由 GM/T 0099 协议单独保护
                    if (n.EndsWith("/OFDEntries.xml", StringComparison.OrdinalIgnoreCase)) return false;
                    if (n.EndsWith("/signedvalue.dat", StringComparison.OrdinalIgnoreCase)) return false;
                    return true;
                });
            log.WriteLine("[8/9] 国密签名完成（SM2withSM3，GB/T 35275 SignedData 封装）");

            // ── 9. 防夹带 / 完整性保护（GM/T 0099 §7.4.3，OFDEntries.xml）───
            var integritySigner = new NetOfd.Crypto.Integrity.GmProtectSigner(keyPair.Private, cert);
            finalOfd = NetOfd.Crypto.Integrity.OfdIntegrity.Protect(finalOfd, integritySigner);
            // OFDEntries.xml 归一为 ofd: 前缀形式后重签完整性签名值
            // （signedvalue.dat 不计入 EMR 签名保护范围，重签不破坏电子病历数字签名）
            finalOfd = NormalizeIntegrityFile(finalOfd, integritySigner);
            log.WriteLine("[9/9] 防夹带保护完成（OFDEntries.xml + signedvalue.dat，SM2/SM3，ofd: 前缀规范）");
        }

        // ── 线性化重排（6.1.3：索引置首、内容先行、资源随后）────────────────
        finalOfd = RepackLinearized(finalOfd);

        File.WriteAllBytes(outPath, finalOfd);
        var sizeMb = finalOfd.Length / 1024d / 1024d;
        log.WriteLine();
        log.WriteLine($"✔ 已生成: {outPath}（{sizeMb:F2} MB，{CountPages(finalOfd)} 页）");

        // ── 自检：签名验证 + 附件可读性 ────────────────────────────────────
        var verify = OfdValidator.Verify(finalOfd);
        log.WriteLine();
        log.WriteLine($"── 签名自检 ──");
        log.WriteLine($"AllValid={verify.AllValid}  签名数={verify.SignatureCount}");
        foreach (var s in verify.Signatures)
        {
            log.WriteLine($"  签名[{s.SignId}] 类型={s.Type} 有效={s.Valid} 保护文件数={s.ReferenceCount} 杂凑不符={s.ReferenceMismatch}");
            log.WriteLine($"    签章人={s.CertSubject}  签章时间={s.SignTime:yyyy-MM-dd HH:mm:ss}");
            if (s.Error != null) log.WriteLine($"    错误={s.Error}");
        }
        if (verify.Error != null) log.WriteLine($"  错误={verify.Error}");

        var integrityOk = false;
        if (!noSign)
        {
            integrityOk = new NetOfd.Crypto.Integrity.OfdIntegrityVerifier()
                .Integrity(finalOfd, new NetOfd.Crypto.Integrity.GmProtectVerifier());
            log.WriteLine($"  防夹带/完整性（OFDEntries.xml + signedvalue.dat，含夹带文件检查）: {(integrityOk ? "通过" : "失败")}");
        }

        using (var reader = new NetOfd.Reader.OFDReader(new MemoryStream(finalOfd)))
        {
            var att = reader.GetAttachmentFile("心电图影像.png");
            log.WriteLine();
            log.WriteLine("── 回读自检 ──");
            log.WriteLine($"  页数: {reader.GetPages().Count()}");
            log.WriteLine($"  附件『心电图影像.png』: {(att is null ? "读取失败" : $"{att.Length} 字节，与源文件一致={att.SequenceEqual(File.ReadAllBytes(ecgPath))}")}");
            var tags = reader.GetOfd().GetFirstDocBody()?.DocInfo;
            log.WriteLine($"  DocType: {reader.GetOfd().OfdDocType}  DocID: {tags?.DocID}");
        }

        // ── 包内条目清单 ───────────────────────────────────────────────────
        var entries = ListEntries(finalOfd);
        log.WriteLine();
        log.WriteLine("── 包内条目（对照 GB/T 48666—2026 表 1）──");
        foreach (var (name, len) in entries)
            log.WriteLine($"  {len,10} B  {name}");

        var success = verify.AllValid || noSign;
        return new OfdHResult
        {
            Success = success,
            OutputPath = outPath,
            OfdBytes = finalOfd,
            PageCount = CountPages(finalOfd),
            SizeBytes = finalOfd.LongLength,
            UniqueId = uniqueId,
            DocumentId = docId,
            SignatureCount = verify.SignatureCount,
            AllSignaturesValid = verify.AllValid,
            IntegrityVerified = integrityOk,
            VersionIndex = versionIndex,
            FontEmbedded = fontEmbedded,
            FontWarning = fontWarning,
            PackageEntries = entries,
            Error = success ? null : (verify.Error ?? "签名自检未通过"),
        };
    }

    // ═════════════════════════════════════════════════════════════════════
    //  1. 基础文档
    // ═════════════════════════════════════════════════════════════════════

    private static void BuildBaseDocument(string outPath, EmrCaseData c, string signTime, string uniqueId, string docId, string ecgImagePath)
    {
        if (File.Exists(outPath)) File.Delete(outPath);
        using var doc = new OFDDoc(outPath);
        doc.SetDefaultPageLayout(PageLayout.A4().SetMargin(20));
        ConfigureDocInfo(doc, c, signTime, uniqueId, docId);

        const double contentW = 170; // 210 - 2×20

        // ── 第 1 页：住院病案首页 ──────────────────────────────────────
        // 说明：流式排版在 Close 时统一 flush，AddPage() 之间不切换书写目标，
        // 逻辑分页统一用 PageAreaFiller（命令元素，其后仍有内容即强制换页）。
        doc.Add(Center("xx市第三人民医院", 6.5d, bold: true).SetWidth(contentW));
        doc.Add(Center("住 院 病 案 首 页", 5d, bold: true).SetWidth(contentW));
        doc.Add(new Paragraph().AddText("（OFD-H 电子病历版式文档 · 演示数据）").SetFont(FontName.FangSong)
            .SetFontSize(2.6d).SetTextAlign(TextAlign.Center).SetWidth(contentW));
        doc.AddTable(InfoTable(new[]
        {
            new[] { "姓　　名", c.PatientName, "性　　别", c.Sex },
            new[] { "年　　龄", $"{c.Age} 岁", "民　　族", c.Nation },
            new[] { "婚姻状况", c.Marriage, "职　　业", c.Occupation },
            new[] { "出生日期", c.BirthDate, "身份证号", c.IdCard },
            new[] { "联系电话", c.Phone, "联 系 人", c.ContactPerson },
            new[] { "现 住 址", c.Address, null, null },
        }, new double[] { 26, 59, 26, 59 }));
        doc.AddTable(InfoTable(new[]
        {
            new[] { "病 案 号", c.MedicalRecordNo, "住 院 号", c.AdmissionNo },
            new[] { "入院次数", "第 1 次", "入院途径", "门诊" },
            new[] { "入院日期", c.AdmissionTime, "出院日期", "—" },
            new[] { "科　　室", c.Dept, "病室床位", c.WardBed },
            new[] { "经治医师", c.Doctor, "主任医师", c.ChiefDoctor },
            new[] { "责任护士", c.Nurse, "疾病转归", "好转" },
        }, new double[] { 26, 59, 26, 59 }));
        doc.AddTable(InfoTable(new[]
        {
            new[] { "主要诊断", c.PrimaryDiagnosis, null, null },
            new[] { "其他诊断", c.OtherDiagnoses, null, null },
        }, new double[] { 26, 59, 26, 59 }, headerCols: 2));

        // ── 第 2 页：入院记录 ─────────────────────────────────────────
        doc.Add(new PageAreaFiller());
        doc.Add(Section("入 院 记 录", 4.5d));
        doc.Add(Body($"一般项目：患者 {c.PatientName}，{c.Sex}，{c.Age} 岁，{c.Marriage}，{c.Occupation}，"
                   + $"籍贯山东省xx市。因“{c.ChiefComplaint}”于 {c.AdmissionTime} 收入 {c.Dept}。"));
        doc.Add(Body($"主诉：{c.ChiefComplaint}"));
        doc.Add(Body($"现病史：{c.PresentIllness}"));
        doc.Add(Body($"既往史：{c.PastHistory}"));
        doc.Add(Body($"个人史、家族史：{c.PersonalHistory}{c.FamilyHistory}"));
        doc.Add(Body($"体格检查：{c.PhysicalExam}"));

        // ── 第 3 页：辅助检查与诊疗计划 ───────────────────────────────
        doc.Add(new PageAreaFiller());
        doc.Add(Section("辅助检查", 4.5d));
        doc.Add(Body($"心电图（2026-10-18 10:42）：{c.EcgText}"));
        doc.Add(Body($"心电图结论：{c.EcgConclusion}"));
        doc.Add(new Img(160, 100, ecgImagePath));
        doc.Add(Center("图 1　心电图检查报告（II 导联，25 mm/s，10 mm/mV）", 2.8d).SetWidth(contentW));
        doc.Add(Section("初步诊断与诊疗计划", 4.5d));
        doc.Add(Body($"初步诊断：{c.PrimaryDiagnosis}；{c.OtherDiagnoses}。"));
        doc.Add(Body($"诊疗计划：{c.TreatmentPlan}"));
        doc.Add(new Paragraph().AddText("医师签名：李明　　　　　　　　　　上级医师签名：赵鸿")
            .SetFont(FontName.FangSong).SetFontSize(3.2d).SetWidth(contentW));
        doc.Add(new Paragraph().AddText("2026 年 10 月 19 日 16:30")
            .SetFont(FontName.FangSong).SetFontSize(3.2d).SetTextAlign(TextAlign.Right).SetWidth(contentW));
    }

    /// <summary>OFD.xml 根节点 DocType=OFD-H + DocInfo 元数据（7.1a / 7.1c / 7.3a / 7.3b）。</summary>
    private static void ConfigureDocInfo(OFDDoc doc, EmrCaseData c, string signTime, string uniqueId, string docId)
    {
        var ofdRoot = doc.GetOfdDir().ObtainOfd();
        ofdRoot.OfdDocType = DocTypeOfdH;                       // GB/T 48666-2026 7.1 a)
        ofdRoot.OfdVersion = "1.2";                             // 6.2：GB/T 33190—2016 → "1.X"

        var info = ofdRoot.ObtainDocBody().ObtainDocInfo();
        info.DocID = docId;
        info.Title = $"住院病案首页——{c.PatientName}";
        info.Author = c.Doctor;
        info.Subject = $"{c.Dept}入院记录";
        info.Abstract = $"患者{c.PatientName}，{c.Sex}，{c.Age}岁，因“{c.ChiefComplaint}”入院；"
                      + $"主要诊断：{c.PrimaryDiagnosis}。本文件为 GB/T 48666—2026 合规演示数据。";
        info.CreationDate = signTime;
        info.ModDate = signTime;
        info.Creator = "NetOfd EMR 生成器（OFD-H）";
        info.CreatorVersion = "1.0.0";
        foreach (var kw in c.Keywords) info.AddKeyword(kw);
        // 7.3 a/b)：患者、责任者、生成时间、文件标识（类型/秘密等级/唯一识别码）
        info.AddCustomData("患者标识", $"{c.PatientName}（{c.IdCard}）");
        info.AddCustomData("病案号", c.MedicalRecordNo);
        info.AddCustomData("责任者", $"{c.Doctor}（经治医师）/ {c.ChiefDoctor}（主任医师）");
        info.AddCustomData("生成时间", signTime);
        info.AddCustomData("文件类型", DocTypeOfdH);
        info.AddCustomData("秘密等级", "内部");
        info.AddCustomData("唯一识别码", uniqueId);
        info.AddCustomData("适用标准", "GB/T 48666-2026; GB/T 33190-2016");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  2. 字型嵌入（7.2b）
    // ═════════════════════════════════════════════════════════════════════

    private static byte[] EmbedFonts(byte[] ofd, string fontPath, string fontFamily)
    {
        var map = ReadZip(ofd);
        var ofdNs = XNamespace.Get(OfdNs);
        var resKey = map.Keys.FirstOrDefault(k => k.EndsWith("/PublicRes.xml", StringComparison.OrdinalIgnoreCase));
        if (resKey == null) throw new InvalidOperationException("缺少 PublicRes.xml，无法嵌入字型");
        var doc = XDocument.Parse(Encoding.UTF8.GetString(map[resKey]));
        var docDir = ParentDir(resKey);

        var font = doc.Descendants(ofdNs + "Font")
            .FirstOrDefault(f => (string?)f.Attribute("FontName") == fontFamily);
        if (font == null) throw new InvalidOperationException($"PublicRes.xml 中未找到字体「{fontFamily}」");
        font.Element(ofdNs + "FontFile")?.Remove();
        font.Add(new XElement(ofdNs + "FontFile", "Res/Font_FangSong.ttf"));   // 相对 PublicRes.xml
        map[resKey] = ToUtf8Bytes(doc, indent: false);
        map[$"{docDir}/Res/Font_FangSong.ttf"] = File.ReadAllBytes(fontPath);
        return WriteZip(map);
    }

    /// <summary>
    /// 解析可嵌入的字型文件路径：显式指定优先，否则按平台探测候选路径。
    /// </summary>
    /// <returns>(字型路径, 警告信息)；路径为 null 表示未找到可用字型。</returns>
    private static (string? Path, string? Warning) ResolveFont(OfdHOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.FontPath))
        {
            if (File.Exists(options.FontPath))
                return (Path.GetFullPath(options.FontPath), null);
            return (null, $"指定的字型文件不存在：{options.FontPath}");
        }

        foreach (var candidate in FontCandidates)
        {
            if (File.Exists(candidate))
                return (candidate, null);
        }

        return (null,
            "未在本机找到可嵌入的中文字型（已尝试 " + FontCandidates.Length +
            " 个候选路径）；字型数据未嵌入文档，不满足 GB/T 48666—2026 第 7.2 b 条");
    }

    // ═════════════════════════════════════════════════════════════════════
    //  4. 机读信息 / 结构化数据（7.1d / 7.3c / 7.5）
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 机读信息文件（7.5 a/b/c）：半结构化 XML、UTF-8 编码、支持 GB 18030 字符；
    /// 含文件头（版本号/创建时间/修改时间）、元数据（文档属性/创作者/关键字）、
    /// 标识信息（文件类型/秘密等级/唯一识别码）。
    /// </summary>
    private static byte[] BuildMachineReadableXml(EmrCaseData c, string signTime, string uniqueId, string docId)
    {
        var ns = XNamespace.Get("urn:emr:machine-readable:2026");
        var doc = new XDocument(new XElement(ns + "EMRDocument",
            new XAttribute("Version", "1.0"),
            new XElement(ns + "FileHeader",
                new XElement(ns + "Version", "1.0"),
                new XElement(ns + "CreationTime", ToIso(signTime)),
                new XElement(ns + "ModifyTime", ToIso(signTime)),
                new XElement(ns + "Generator", "NetOfd EMR 生成器 1.0.0"),
                new XElement(ns + "Standard", "GB/T 48666-2026")),
            new XElement(ns + "Metadata",
                new XElement(ns + "DocumentProperty",
                    new XAttribute("Type", "电子病历版式文档"),
                    new XAttribute("DocType", DocTypeOfdH),
                    new XAttribute("Base", "GB/T 33190-2016")),
                new XElement(ns + "Title", $"住院病案首页——{c.PatientName}"),
                new XElement(ns + "Creator", c.Doctor),
                new XElement(ns + "Organization", c.Hospital),
                new XElement(ns + "Keywords", string.Join(";", c.Keywords))),
            new XElement(ns + "Identification",
                new XElement(ns + "FileType", DocTypeOfdH),
                new XElement(ns + "SecretLevel", "内部"),
                new XElement(ns + "UniqueID", uniqueId),
                new XElement(ns + "DocID", docId)),
            new XElement(ns + "Patient",
                new XElement(ns + "Name", c.PatientName),
                new XElement(ns + "Sex", c.Sex),
                new XElement(ns + "Age", c.Age),
                new XElement(ns + "BirthDate", c.BirthDate),
                new XElement(ns + "MedicalRecordNo", c.MedicalRecordNo)),
            new XElement(ns + "Encounter",
                new XElement(ns + "AdmissionNo", c.AdmissionNo),
                new XElement(ns + "AdmissionTime", c.AdmissionTime),
                new XElement(ns + "Department", c.Dept),
                new XElement(ns + "AttendingDoctor", c.Doctor))));
        return ToUtf8Bytes(doc, indent: true);
    }

    /// <summary>
    /// 构造标准结构化数据模型（WS/T 500.32—2016 住院病案首页共享文档 + WS 445.10 数据元）。
    /// <para>
    /// 字段值与版式页面内容同源（同一 <see cref="EmrCaseData"/>），保证「人读」与「机读」一致；
    /// 编码体系对齐：诊断 ICD-10、手术 ICD-9-CM-3、科室/病案号 WS 445 数据元码。
    /// </para>
    /// </summary>
    private static EmrStructuredData BuildStructuredData(EmrCaseData c, string signTime, string uniqueId)
    {
        var data = new EmrStructuredData
        {
            DocumentSerialNo = uniqueId,
            Title = Wst500Codes.InpatientHomePageDocName,
            EffectiveTime = ToCompactTime(signTime),
            VersionNumber = "1",

            // ── 患者信息 ──
            PatientName = c.PatientName,
            SexCode = c.Sex == "女" ? Wst500Codes.SexFemale : Wst500Codes.SexMale,
            BirthDate = c.BirthDate.Replace("-", ""),
            Age = c.Age,
            MaritalStatusCode = c.Marriage == "已婚" ? Wst500Codes.MaritalMarried : Wst500Codes.MaritalUnmarried,
            EthnicGroupCode = c.Nation == "汉" ? Wst500Codes.EthnicHan : "",
            NationalityCode = "156",
            IdCardNo = c.IdCard,
            Phone = c.Phone,
            MedicalRecordNo = c.MedicalRecordNo,
            AdmissionNo = c.AdmissionNo,
            AddressProvince = "山东省",
            AddressCity = "xx市",
            AddressCounty = "xx区",
            AddressTownship = "通济街道",
            AddressStreet = "新兴路",
            AddressHouseNo = "28 号 3 号楼",

            // ── 就诊信息 ──
            AdmissionTimes = 1,
            AdmissionTime = ToCompactTime(c.AdmissionTime),
            DischargeTime = ToCompactTime("2026-10-21 11:40"),
            HospitalDays = 4,
            AdmissionRouteCode = Wst500Codes.AdmissionRouteOutpatient,
            AdmissionDepartment = c.Dept,
            DischargeDepartment = c.Dept,
            AdmissionWard = c.WardBed,
            HospitalName = c.Hospital,
            HospitalOrgCode = "123456789012345678",
            PaymentMethodCode = "01",
            AttendingDoctor = c.Doctor,
            ResponsibleNurse = c.Nurse,
            CodingDoctor = c.ChiefDoctor,

            ChiefComplaint = c.ChiefComplaint,
            HospitalizationHistory = c.PastHistory,
            HospitalizationProcess = c.PresentIllness,
            TreatmentPlan = c.TreatmentPlan,
            OtherTreatment = c.PhysicalExam,
        };

        // ── 诊断（出院诊断）：ICD-10 编码 ──
        data.Diagnoses.Add(new EmrDiagnosis
        {
            Category = "出院诊断",
            Name = "冠状动脉粥样硬化性心脏病　不稳定型心绞痛",
            Code = "I20.0",
            Status = Wst500Codes.DiseaseStatusUncured,
        });
        data.Diagnoses.Add(new EmrDiagnosis
        {
            Category = "出院诊断",
            Name = "高血压病 3 级（极高危）",
            Code = "I10",
        });
        data.Diagnoses.Add(new EmrDiagnosis
        {
            Category = "出院诊断",
            Name = "2 型糖尿病",
            Code = "E11.9",
        });

        // ── 生命体征（DE 码 + 计量单位）──
        data.VitalSigns.Add(new EmrObservation
        {
            DeCode = "DE04.50.007.00", DisplayName = "体温", Qualifier = "入院体温",
            NumericValue = 36.5, Unit = "℃",
        });
        data.VitalSigns.Add(new EmrObservation
        {
            DeCode = "DE04.50.008.00", DisplayName = "脉搏", Qualifier = "入院脉搏",
            NumericValue = 78, Unit = "次/分",
        });
        data.VitalSigns.Add(new EmrObservation
        {
            DeCode = "DE04.50.009.00", DisplayName = "呼吸", Qualifier = "入院呼吸",
            NumericValue = 18, Unit = "次/分",
        });
        data.VitalSigns.Add(new EmrObservation
        {
            DeCode = "DE04.50.010.00", DisplayName = "收缩压", Qualifier = "入院血压",
            NumericValue = 152, Unit = "mmHg",
        });
        data.VitalSigns.Add(new EmrObservation
        {
            DeCode = "DE04.50.011.00", DisplayName = "舒张压", Qualifier = "入院血压",
            NumericValue = 94, Unit = "mmHg",
        });

        // ── 过敏史 ──
        data.Allergy = new EmrAllergy { Flag = "0", AllergicDrug = "否认药物、食物过敏史" };

        // ── 辅助检查（心电图）──
        data.LabResults.Add(new EmrObservation
        {
            DeCode = Wst500Codes.DeAuxiliaryExam, DisplayName = "辅助检查结果", Qualifier = "心电图",
            TextValue = c.EcgText,
        });

        // ── 主要健康问题（住院者疾病状态）──
        data.HealthProblems.Add(new EmrObservation
        {
            DeCode = Wst500Codes.DeDiseaseStatus, DisplayName = "住院者疾病状态代码",
            CodedValue = Wst500Codes.DiseaseStatusUncured,
        });

        return data;
    }

    /// <summary>yyyy-MM-dd HH:mm:ss → yyyyMMddHHmmss（WS/T 500 时间格式）。</summary>
    private static string ToCompactTime(string time)
    {
        if (DateTime.TryParse(time, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        }
        return new DateTime().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  6. 加注记录（3.12 / 5.3.2e）
    // ═════════════════════════════════════════════════════════════════════

    private static Annotation BuildAuditAnnotation()
    {
        return new Annotation(148, 246, 38, 18, AnnotType.Path, new AuditStampDrawer())
        {
            Creator = "王芳（质控医师）",
            Remark = "病历质量审核：病历内容完整、书写规范，予以审核通过。",
            LastModDate = DateOnly.FromDateTime(DateTime.Now),
        };
    }

    private sealed class AuditStampDrawer : IDrawer
    {
        public void Draw(DrawContext ctx)
        {
            ctx.LineWidth = 0.6;
            ctx.StrokeStyle = "#B71C1C";
            ctx.StrokeRect(1.2, 1.2, 35.6, 15.6);
            ctx.FillStyle = "#B71C1C";
            ctx.Font = "5mm 仿宋";
            ctx.FillText("已审核", 11.5, 8.6);
            ctx.Font = "2.6mm 仿宋";
            ctx.FillText("审核医师：王芳", 9.0, 12.8);
            ctx.Font = "2.2mm 仿宋";
            ctx.FillText("2026-10-19 16:30", 8.0, 15.6);
        }
    }

    // ═════════════════════════════════════════════════════════════════════
    //  7. 目录结构规范化（对齐 6.2 表 1 规范命名与目录结构）
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>
    /// 将编辑器产出规范化为 GB/T 48666—2026 表 1 的规范目录结构：
    /// <list type="bullet">
    /// <item>自定义标引列表移至 Doc_0/Tags/CustomTags.xml，内容文件命名 CustomTag_N.xml；</item>
    /// <item>附件移至 Doc_0/Attachs/Attachments.xml，内容命名 Attach_N.ext；</item>
    /// <item>注释分页文件 FileLoc 归一为容器绝对路径（兼容各阅读器）。</item>
    /// </list>
    /// FileLoc 统一使用绝对路径（ST_Loc 允许 "/" 起始），避免相对基准歧义。
    /// </summary>
    private static byte[] NormalizeStructure(byte[] ofd)
    {
        var map = ReadZip(ofd);
        var ofdNs = XNamespace.Get(OfdNs);
        var docKey = map.Keys.FirstOrDefault(k => k.EndsWith("/Document.xml", StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException("缺少 Document.xml");
        var docDir = ParentDir(docKey);

        // ── A. 自定义标引 ─────────────────────────────────────────────
        var tagsListKey = map.Keys.FirstOrDefault(k => k.EndsWith("/CustomTags.xml", StringComparison.OrdinalIgnoreCase));
        if (tagsListKey != null)
        {
            var tagsDoc = XDocument.Parse(Encoding.UTF8.GetString(map[tagsListKey]));
            var entries = tagsDoc.Descendants(ofdNs + "CustomTag").ToList();
            var tagsDir = $"{docDir}/Tags";
            for (int i = 0; i < entries.Count; i++)
            {
                var locEl = entries[i].Element(ofdNs + "FileLoc");
                if (locEl == null || string.IsNullOrWhiteSpace(locEl.Value)) continue;
                var oldKey = ResolveKey(map, locEl.Value, docDir);
                if (oldKey == null) continue;
                var ext = Path.GetExtension(oldKey);
                var newKey = $"{tagsDir}/CustomTag_{i + 1}{ext}";
                map[newKey] = map[oldKey];
                map.Remove(oldKey);
                locEl.Value = "/" + newKey;
            }
            map.Remove(tagsListKey);
            map[$"{tagsDir}/CustomTags.xml"] = ToUtf8Bytes(WithOfdPrefix(tagsDoc), indent: false);
            PatchDocumentLoc(map, docKey, ofdNs, "CustomTags", $"/{tagsDir}/CustomTags.xml");
        }

        // ── B. 附件 ───────────────────────────────────────────────────
        var attListKey = map.Keys.FirstOrDefault(k => k.EndsWith("/Attachments.xml", StringComparison.OrdinalIgnoreCase));
        if (attListKey != null)
        {
            var attDoc = XDocument.Parse(Encoding.UTF8.GetString(map[attListKey]));
            var entries = attDoc.Descendants(ofdNs + "Attachment").ToList();
            var attachsDir = $"{docDir}/Attachs";
            for (int i = 0; i < entries.Count; i++)
            {
                var locEl = entries[i].Element(ofdNs + "FileLoc");
                if (locEl == null || string.IsNullOrWhiteSpace(locEl.Value)) continue;
                var oldKey = ResolveKey(map, locEl.Value, docDir);
                if (oldKey == null) continue;
                var ext = Path.GetExtension(oldKey);
                var newKey = $"{attachsDir}/Attach_{i + 1}{ext}";
                map[newKey] = map[oldKey];
                map.Remove(oldKey);
                locEl.Value = "/" + newKey;
            }
            map.Remove(attListKey);
            map[$"{attachsDir}/Attachments.xml"] = ToUtf8Bytes(WithOfdPrefix(attDoc), indent: false);
            PatchDocumentLoc(map, docKey, ofdNs, "Attachments", $"/{attachsDir}/Attachments.xml");
        }

        // ── C. 注释分页文件 FileLoc → 绝对路径 ────────────────────────
        var annListKey = map.Keys.FirstOrDefault(k => k.EndsWith("/Annots/Annotations.xml", StringComparison.OrdinalIgnoreCase));
        if (annListKey != null)
        {
            var annDoc = XDocument.Parse(Encoding.UTF8.GetString(map[annListKey]));
            foreach (var page in annDoc.Descendants(ofdNs + "Page").ToList())
            {
                var locEl = page.Element(ofdNs + "FileLoc");
                if (locEl == null || string.IsNullOrWhiteSpace(locEl.Value)) continue;
                if (locEl.Value.TrimStart().StartsWith('/')) continue;   // 已是绝对路径
                var key = ResolveKey(map, locEl.Value, docDir);
                if (key != null) locEl.Value = "/" + key;
            }
            map[annListKey] = ToUtf8Bytes(WithOfdPrefix(annDoc), indent: false);
        }

        return WriteZip(map);
    }

    /// <summary>
    /// 匹配 XML 元素开/闭标签名（不含属性，且不匹配处理指令、注释等非元素形式）。
    /// </summary>
    private static readonly Regex TagNameRegex = new(
        @"<(/?)([a-zA-Z_][\w\-.]*)(?=\s|>|/)",
        RegexOptions.Compiled);

    /// <summary>
    /// 将默认命名空间形式（<c>xmlns="..."</c>）的 OFD XML 规范为 <c>ofd:</c> 前缀形式。
    /// <para>
    /// GB/T 33190—2016 §7.1：元素节点应使用命名空间标识（标识为 ofd），元素属性不使用命名空间标识；
    /// 映射式编辑器（ZipMapIO 直写）产出的是默认命名空间形式，与 Pkg 容器层产物不一致，此处归一。
    /// </para>
    /// <para>
    /// ⚠️ 实现说明：LINQ-to-XML 对"构造/解析得到的命名空间元素"序列化时会强制追加隐式
    /// <c>xmlns="..."</c> 默认声明——即便事后 <c>Add(new XAttribute(XNamespace.Xmlns + "ofd", ns))</c>
    /// 也只会得到 <c>xmlns=</c> 与 <c>xmlns:ofd=</c> 双声明（实测残留）。因此改用与
    /// <c>NetOfd.Pkg.Container.VirtualContainer.OfdXmlWithPrefix</c> 相同的字符串规范化：
    /// 先按默认命名空间序列化，再统一改写标签前缀与命名空间声明，最后回解析得到干净的
    /// 单 <c>xmlns:ofd</c> 声明树。仅适用于根及全部后代元素均处于 OFD 命名空间的文件
    /// （OFDEntries/CustomTags/Attachments/Annotations 四个列表文件均满足）。
    /// </para>
    /// </summary>
    private static XDocument WithOfdPrefix(XDocument doc)
    {
        var root = doc.Root;
        if (root == null) return doc;
        if (root.Name.Namespace != XNamespace.Get(OfdNs)) return doc;
        if (root.Attributes().Any(a => a.Name == XNamespace.Xmlns + "ofd")) return doc;
        var xml = root.ToString(SaveOptions.DisableFormatting);
        xml = TagNameRegex.Replace(xml, "<$1ofd:$2");
        xml = xml.Replace($"xmlns=\"{OfdNs}\"", $"xmlns:ofd=\"{OfdNs}\"");
        return XDocument.Parse(xml);
    }

    /// <summary>
    /// OFDEntries.xml 前缀规范化 + 重签完整性签名值（signedvalue.dat）。
    /// <para>
    /// signedvalue.dat 与 OFDEntries.xml 均不计入电子病历数字签名的保护范围（8.c 的
    /// “注释列表、签名列表等文件”之外的技术文件），重签不破坏 EMR 签名验证。
    /// </para>
    /// </summary>
    private static byte[] NormalizeIntegrityFile(byte[] ofd, NetOfd.Crypto.Integrity.GmProtectSigner signer)
    {
        var map = ReadZip(ofd);
        var key = map.Keys.FirstOrDefault(k => k.EndsWith("OFDEntries.xml", StringComparison.OrdinalIgnoreCase));
        if (key == null) return ofd;
        var doc = XDocument.Parse(Encoding.UTF8.GetString(map[key]));
        map[key] = ToUtf8Bytes(WithOfdPrefix(doc), indent: false);
        map["signedvalue.dat"] = signer.DigestThenSign(map[key]);
        return WriteZip(map);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  线性化重排（6.1.3）
    // ═════════════════════════════════════════════════════════════════════
    private static byte[] RepackLinearized(byte[] ofd)
    {
        var map = ReadZip(ofd);
        string Norm(string s) => s.Replace('\\', '/').TrimStart('/');
        bool Has(string name) => map.Keys.Any(k => string.Equals(Norm(k), name, StringComparison.OrdinalIgnoreCase));

        var order = new List<string>();
        void Put(string name)
        {
            var key = map.Keys.FirstOrDefault(k => string.Equals(Norm(k), name, StringComparison.OrdinalIgnoreCase));
            if (key != null) order.Add(key);
        }
        // 头部：内容索引（OFDEntries.xml）→ 主入口 → 文档描述 → 资源索引
        if (Has("OFDEntries.xml")) Put("OFDEntries.xml");
        Put("OFD.xml");
        Put($"{docDirOf(map)}/Document.xml");
        Put($"{docDirOf(map)}/PublicRes.xml");
        Put($"{docDirOf(map)}/DocumentRes.xml");
        // 页面内容（按名称自然序）
        order.AddRange(map.Keys
            .Where(k => Norm(k).Contains("/Pages/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Norm, StringComparer.OrdinalIgnoreCase));
        // 其余结构性文件
        foreach (var seg in new[] { "Annots", "Tags", "Attachs", "Signs", "Versions" })
            order.AddRange(map.Keys
                .Where(k => Norm(k).Contains($"/{seg}/", StringComparison.OrdinalIgnoreCase))
                .OrderBy(Norm, StringComparer.OrdinalIgnoreCase));
        // 版本快照目录 V_N
        order.AddRange(map.Keys
            .Where(k => System.Text.RegularExpressions.Regex.IsMatch(Norm(k), @"/V_\d+/"))
            .OrderBy(Norm, StringComparer.OrdinalIgnoreCase));
        // 二进制资源置后
        order.AddRange(map.Keys
            .Where(k => Norm(k).Contains("/Res/", StringComparison.OrdinalIgnoreCase))
            .OrderBy(Norm, StringComparer.OrdinalIgnoreCase));
        // 签名值文件置末
        if (Has("signedvalue.dat")) Put("signedvalue.dat");
        // 兜底：未归类的按原顺序补入
        order.AddRange(map.Keys.Where(k => !order.Contains(k)));

        return WriteZip(map, order.ToArray());
    }

    private static string docDirOf(Dictionary<string, byte[]> map)
    {
        var key = map.Keys.FirstOrDefault(k => k.EndsWith("/Document.xml", StringComparison.OrdinalIgnoreCase));
        return key == null ? "Doc_0" : ParentDir(key);
    }

    // ═════════════════════════════════════════════════════════════════════
    //  心电图图像（SkiaSharp 合成演示波形）
    // ═════════════════════════════════════════════════════════════════════

    private static void RenderEcgPng(string outPath)
    {
        const int W = 800, H = 500, GridTop = 66, GridBottom = 428;
        using var bmp = new SKBitmap(W, H);
        using (var c = new SKCanvas(bmp))
        {
            c.Clear(SKColors.White);
            // 心电坐标网格（1mm/5mm）
            using var minor = new SKPaint { Color = new SKColor(250, 216, 222), StrokeWidth = 1f };
            using var major = new SKPaint { Color = new SKColor(240, 158, 172), StrokeWidth = 1.4f };
            for (int x = 0; x <= W; x += 5) c.DrawLine(x, GridTop, x, GridBottom, x % 25 == 0 ? major : minor);
            for (int y = GridTop; y <= GridBottom; y += 5) c.DrawLine(0, y, W, y, y % 25 == 0 ? major : minor);

            // II 导联波形（窦性心律，72 bpm）
            using var wave = new SKPaint
            {
                Color = new SKColor(20, 20, 20),
                StrokeWidth = 2f,
                IsAntialias = true,
                Style = SKPaintStyle.Stroke
            };
            using var path = new SKPath();
            float baseline = (GridTop + GridBottom) / 2f, period = 104f; // 25mm/s、5px/mm → RR≈104px
            for (float x = 6f; x < W - 6f; x += 1.1f)
            {
                float y = baseline + EcgOffset(x % period, period);
                if (path.PointCount == 0) path.MoveTo(x, y); else path.LineTo(x, y);
            }
            c.DrawPath(path, wave);

            // 文本
            var tfBold = SKTypeface.FromFamilyName("SimHei") ?? SKTypeface.FromFamilyName("Microsoft YaHei") ?? SKTypeface.Default;
            var tf = SKTypeface.FromFamilyName("Microsoft YaHei") ?? SKTypeface.Default;
            using var paint = new SKPaint { IsAntialias = true };
            paint.Typeface = tfBold; paint.Color = new SKColor(26, 26, 26); paint.TextSize = 24;
            c.DrawText("xx市第三人民医院", 20, 38, paint);
            paint.TextSize = 13;
            c.DrawText("心电图检查报告", 20, 58, paint);
            c.DrawText("姓　名：张建国　　病案号：78420931　　科　室：心血管内科　　检查时间：2026-10-18 10:42", 300, 58, paint);
            paint.Typeface = tfBold; paint.TextSize = 16;
            c.DrawText("Ⅱ", 12, baseline + 6, paint);
            paint.Typeface = tf; paint.TextSize = 13; paint.Color = new SKColor(26, 26, 26);
            c.DrawText("HR 72 bpm　　PR 160 ms　　QRS 92 ms　　QT 400 ms　　QTc 435 ms　　定标：25 mm/s，10 mm/mV", 20, H - 34, paint);
            paint.Color = new SKColor(110, 110, 110); paint.TextSize = 12;
            c.DrawText("报告医师：李明　　审核医师：王芳　　（本图像为 GB/T 48666—2026 合规演示数据）", 20, H - 14, paint);
        }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 92);
        File.WriteAllBytes(outPath, data.ToArray());
    }

    private static float Gauss(float x, float mu, float sigma)
        => MathF.Exp(-0.5f * (x - mu) * (x - mu) / (sigma * sigma));

    private static float EcgOffset(float p, float period)
    {
        float y = 0f;
        y += -9f * Gauss(p, period * 0.26f, 15f);   // P 波
        y += 5f * Gauss(p, period * 0.42f, 4f);     // Q 波
        y += -50f * Gauss(p, period * 0.47f, 5f);   // R 波
        y += 15f * Gauss(p, period * 0.51f, 5f);    // S 波
        y += -13f * Gauss(p, period * 0.64f, 18f);  // T 波
        return y;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  文档构建辅助
    // ═════════════════════════════════════════════════════════════════════

    private static Paragraph Center(string text, double sizeMm, bool bold = false)
        => new Paragraph()
            .Add(new Span(text).SetFont(FontName.FangSong).SetFontSize(sizeMm).SetBold(bold))
            .SetFont(FontName.FangSong).SetFontSize(sizeMm)
            .SetTextAlign(TextAlign.Center).SetLineSpace(1.2d);

    private static Paragraph Section(string text, double sizeMm)
        => new Paragraph()
            .Add(new Span(text).SetFont(FontName.FangSong).SetFontSize(sizeMm).SetBold())
            .SetFont(FontName.FangSong).SetFontSize(sizeMm).SetWidth(170).SetLineSpace(2.5d);

    private static Paragraph Body(string text)
        => new Paragraph().AddText(text)
            .SetFont(FontName.FangSong).SetFontSize(3.2d)
            .SetWidth(170).SetLineSpace(1.4d).SetFirstLineIndent(2);

    private static Table InfoTable(string?[][] rows, double[] colWidths, int headerCols = 0)
    {
        var t = new Table
        {
            ColumnCount = colWidths.Length,
            RowCount = rows.Length,
            ColumnWidths = colWidths,
            RowHeight = 7.5d,
            FontSize = 3.0d,
            FontFamily = BodyFontFamily,
            BorderWidth = 0.3,
            CellPadding = 0.8,
        };
        for (int r = 0; r < rows.Length; r++)
        {
            for (int c = 0; c < colWidths.Length; c++)
            {
                var text = rows[r][c];
                var span = 1;
                // 空后续列 → 合并（跨列）
                if (text == null)
                {
                    if (c > 0 && rows[r][c - 1] != null && rows[r].Skip(c).All(x => x == null))
                    {
                        // 向前合并：前一个非空单元格扩展占满剩余列
                        var prev = t.Cells.FirstOrDefault(x => x.RowIndex == r && x.ColumnIndex == c - 1);
                        if (prev != null) prev.ColumnSpan += colWidths.Length - c;
                    }
                    continue;
                }
                t.AddCell(new TableCell
                {
                    Content = text,
                    RowIndex = r,
                    ColumnIndex = c,
                    ColumnSpan = span,
                    BackgroundColor = headerCols > 0 && c < headerCols ? "#F2F2F2" : null,
                }, r, c);
            }
        }
        return t;
    }

    // ═════════════════════════════════════════════════════════════════════
    //  ZIP / XML 辅助
    // ═════════════════════════════════════════════════════════════════════

    private static Dictionary<string, byte[]> ReadZip(byte[] bytes)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var ms = new MemoryStream(bytes);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var e in za.Entries)
        {
            if (e.FullName.EndsWith("/")) continue;    // 目录项
            using var s = e.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            map[e.FullName.Replace('\\', '/')] = buf.ToArray();
        }
        return map;
    }

    private static byte[] WriteZip(Dictionary<string, byte[]> map, string[]? firstOrder = null)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var written = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (firstOrder != null)
                foreach (var key in firstOrder)
                    if (map.ContainsKey(key) && written.Add(key))
                        WriteEntry(za, key, map[key]);
            foreach (var kv in map)
                if (written.Add(kv.Key))
                    WriteEntry(za, kv.Key, kv.Value);
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive za, string name, byte[] content)
    {
        var entryName = name.Replace('\\', '/').TrimStart('/');
        var e = za.CreateEntry(entryName, CompressionLevel.Optimal);   // Deflate（6.1.3）
        using var s = e.Open();
        s.Write(content, 0, content.Length);
    }

    private static List<(string Name, long Len)> ListEntries(byte[] ofd)
    {
        var result = new List<(string, long)>();
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var e in za.Entries)
            if (!e.FullName.EndsWith("/"))
                result.Add((e.FullName, e.Length));
        return result;
    }

    private static int CountPages(byte[] ofd)
    {
        try
        {
            using var reader = new NetOfd.Reader.OFDReader(new MemoryStream(ofd));
            return reader.GetPages().Count();
        }
        catch
        {
            return -1;
        }
    }

    private static byte[] ToUtf8Bytes(XDocument doc, bool indent)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = indent,
            NewLineHandling = NewLineHandling.None,
            NewLineChars = "\n",
        };
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings))
            doc.Save(w);
        return ms.ToArray();
    }

    private static string ToIso(string cnDateTime)
        => DateTime.TryParse(cnDateTime, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)
            ? dt.ToString("yyyy-MM-ddTHH:mm:sszzz", CultureInfo.InvariantCulture)
            : cnDateTime;

    private static string ParentDir(string path)
    {
        var n = path.Replace('\\', '/').TrimStart('/');
        var idx = n.LastIndexOf('/');
        return idx <= 0 ? "" : n[..idx];
    }

    /// <summary>将 ST_Loc（绝对或相对文档目录）解析为包内键。</summary>
    private static string? ResolveKey(Dictionary<string, byte[]> map, string loc, string docDir)
    {
        var n = loc.Replace('\\', '/').Trim().TrimStart('/');
        if (n.Length == 0) return null;
        if (map.ContainsKey(n)) return n;
        if (docDir.Length > 0)
        {
            var alt = $"{docDir}/{n}";
            if (map.ContainsKey(alt)) return alt;
        }
        return null;
    }

    /// <summary>修补 Document.xml 中的资源入口指向（Attachments / CustomTags 等）。</summary>
    private static void PatchDocumentLoc(Dictionary<string, byte[]> map, string docKey, XNamespace ofdNs,
        string elementName, string newLoc)
    {
        var doc = XDocument.Parse(Encoding.UTF8.GetString(map[docKey]));
        var el = doc.Descendants(ofdNs + elementName).FirstOrDefault();
        if (el != null) el.Value = newLoc;
        map[docKey] = ToUtf8Bytes(doc, indent: false);
    }
}
