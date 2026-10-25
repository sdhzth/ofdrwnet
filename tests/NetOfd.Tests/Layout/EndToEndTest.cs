using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Sign;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 端到端集成测试：OFDDoc 构建 → 版本快照 → 页面编辑 → 注释 → 附件 → 自定义标 → 签名/验签 → 版本提取。
/// 锁定各编辑器链式协作时结构共存与 MaxUnitID 单调递增。
/// </summary>
public class EndToEndTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_e2e_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>文字绘制器。</summary>
    private class TextDrawer : IDrawer
    {
        public string Text { get; set; } = "编辑内容";

        public void Draw(DrawContext ctx)
        {
            ctx.FillStyle = "#0000CC";
            ctx.Font = "4mm SimSun";
            ctx.FillText(Text, 20, 30);
        }
    }

    /// <summary>注释内文字绘制器。</summary>
    private class AnnotTextDrawer : IDrawer
    {
        public string Text { get; set; } = "批注";
        public bool DrawRect { get; set; }

        public void Draw(DrawContext ctx)
        {
            if (DrawRect)
            {
                ctx.LineWidth = 0.353;
                ctx.StrokeStyle = "#FF0000";
                ctx.StrokeRect(1, 1, 38, 18);
            }
            ctx.FillStyle = "#0000CC";
            ctx.Font = "3mm SimSun";
            ctx.FillText(Text, 3, 10);
        }
    }

    private static byte[] BuildSampleOfdBytes(string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("端到端集成测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
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

    private static Dictionary<string, byte[]> ReadAllEntries(byte[] ofd)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var e in za.Entries)
        {
            using var s = e.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            map[e.FullName.Replace('\\', '/')] = buf.ToArray();
        }
        return map;
    }

    private static XDocument EntryXml(byte[] ofd, string entryName)
        => XDocument.Parse(Encoding.UTF8.GetString(ReadEntry(ofd, entryName)!));

    private static int GetMaxUnitID(byte[] ofd)
    {
        // MaxUnitID 可能写在 OFD.xml 的 DocInfo 或 Document.xml 的 CommonData 中，取最大值
        var ids = new List<int>();
        var ofdEntry = ReadEntry(ofd, "OFD.xml");
        if (ofdEntry != null)
        {
            var docInfo = XDocument.Parse(Encoding.UTF8.GetString(ofdEntry)).Descendants()
                .FirstOrDefault(e => e.Name.LocalName == "DocInfo");
            if (int.TryParse(docInfo?.Attribute("MaxUnitID")?.Value, out var v1)) ids.Add(v1);
        }
        var docXml = EntryXml(ofd, "Doc_0/Document.xml");
        var cdata = docXml.Descendants().First(e => e.Name.LocalName == "CommonData");
        if (int.TryParse(cdata.Attribute("MaxUnitID")?.Value, out var v2)) ids.Add(v2);
        return ids.Count == 0 ? 0 : ids.Max();
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    private static byte[] BuildSeal(AsymmetricCipherKeyPair sealKp, Org.BouncyCastle.X509.X509Certificate sealCert)
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        return SesV4Seal.Build(
            esId: "SEAL-E2E", vid: "v1.0", sealName: "E2E 测试单位", sealType: SesV4Seal.TypeUnit,
            pictureData: png, pictureType: "PNG", widthMm: 30, heightMm: 30,
            sealCert: sealCert, sealKeyPair: sealKp,
            createDate: new DateTime(2024, 1, 1),
            validStart: new DateTime(2024, 1, 1),
            validEnd: new DateTime(2034, 1, 1));
    }

    // ──────────────────────────────────────────────────────────────

    [Fact]
    public void FullPipeline_VersionSnapshot_IsolatedFromLaterEdits()
    {
        // ── 1. 构建两页文档并立即留存"初稿"版本 ──
        var baseDoc = BuildSampleOfdBytes("e2e_base.ofd");
        var withV1 = OfdVersioner.AddVersion(baseDoc, "初稿", out var idx1);
        Assert.Equal(1, idx1);

        // ── 2. 一连串编辑：加页、页面内容、注释、附件、自定义标 ──
        var edited = OfdEditor.AppendPage(withV1);
        edited = OfdEditor.AddContent(edited, 1, new TextDrawer { Text = "补写内容" });
        edited = OfdAnnotator.AddAnnotation(edited, 2,
            new Annotation(10, 20, 40, 20, AnnotType.Path,
                new AnnotTextDrawer { Text = "审核意见", DrawRect = true }));

        var attachFile = Out("e2e_attach.pdf");
        File.WriteAllBytes(attachFile, Encoding.UTF8.GetBytes("%PDF-1.4 e2e"));
        edited = OfdAttacher.AddAttachment(edited,
            new Attachment("合同扫描件", attachFile).SetFormat("PDF"));

        edited = OfdTagger.AddCustomTag(edited, "urn:e2e:keyword",
            Encoding.UTF8.GetBytes("<Tag xmlns=\"http://www.ofdspec.org\">重要</Tag>"));

        // ── 3. 最终文档应同时具备全部结构，且 MaxUnitID 不回退 ──
        Assert.NotNull(ReadEntry(edited, "Doc_0/Annots/Page_2/Annot_1.xml"));
        Assert.NotNull(ReadEntry(edited, "Doc_0/Attachments.xml"));
        Assert.NotNull(ReadEntry(edited, "Doc_0/CustomTags.xml"));
        Assert.NotNull(ReadEntry(edited, "Doc_0/V_1/Document.xml"));
        Assert.True(GetMaxUnitID(edited) >= GetMaxUnitID(baseDoc));

        var finalPages = EntryXml(edited, "Doc_0/Document.xml").Descendants()
            .Count(e => e.Name.LocalName == "Page" && e.Parent?.Name.LocalName == "Pages");
        Assert.Equal(3, finalPages);

        // ── 4. 提取"初稿"：不含后续任何编辑痕迹 ──
        var draft = OfdVersioner.ExtractVersion(edited, "v1");
        Assert.Null(ReadEntry(draft, "Doc_0/Annots/Annotations.xml"));
        Assert.Null(ReadEntry(draft, "Doc_0/Attachments.xml"));
        Assert.Null(ReadEntry(draft, "Doc_0/CustomTags.xml"));

        var draftMap = ReadAllEntries(draft);
        Assert.DoesNotContain(draftMap.Keys, k => k.Contains("/Annots/", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(draftMap.Keys, k => k.Contains("/V_", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(draftMap.Keys, k => k.Contains("/Versions/", StringComparison.OrdinalIgnoreCase));

        var draftPages = EntryXml(draft, "Doc_0/Document.xml").Descendants()
            .Count(e => e.Name.LocalName == "Page" && e.Parent?.Name.LocalName == "Pages");
        Assert.Equal(2, draftPages);

        // 提取结果可再次作为输入使用（自洽）
        var reEdited = OfdAnnotator.AddAnnotation(draft, 1,
            new Annotation(1, 1, 10, 10, AnnotType.Path,
                new AnnotTextDrawer { Text = "再批注" }));
        Assert.NotNull(ReadEntry(reEdited, "Doc_0/Annots/Page_1/Annot_1.xml"));
    }

    [Fact]
    public void FullPipeline_SignAfterEdits_ShouldVerifyAndKeepVersionsReadable()
    {
        // 编辑链 + 快照后签名
        var baseDoc = BuildSampleOfdBytes("e2e_sign.ofd");
        var withV1 = OfdVersioner.AddVersion(baseDoc, "初稿", out _);

        var annotated = OfdAnnotator.AddAnnotation(withV1, 1,
            new Annotation(5, 5, 20, 15, AnnotType.Path,
                new AnnotTextDrawer { Text = "盖章前批注" }));

        var attachFile = Out("e2e_attach_b.txt");
        File.WriteAllBytes(attachFile, Encoding.UTF8.GetBytes("e2e attachment"));
        var attached = OfdAttacher.AddAttachment(annotated,
            new Attachment("附件A", attachFile).SetUsage("证明"));

        var (signerKp, signerCert) = MakeKeys("E2E Signer");
        var (sealKp, sealCert) = MakeKeys("E2E Seal Maker");
        var seal = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(BuildSeal(sealKp, sealCert));

        var signed = OfdSigner.Sign(attached, signerKp, signerCert,
            "2026-08-23 10:00:00", seal: seal, isSeal: true, version: 4);

        // 签名有效
        var result = OfdValidator.Verify(signed);
        var detail = result.Error ?? string.Join(" | ",
            result.Signatures.Select(s => $"[{s.SignId}] valid={s.Valid} err={s.Error}"));
        Assert.True(result.AllValid, detail);
        Assert.Equal(1, result.SignatureCount);

        // 签名不破坏既有结构与版本信息
        Assert.NotNull(ReadEntry(signed, "Doc_0/Annots/Annotations.xml"));
        Assert.Single(OfdVersioner.GetVersions(signed));

        // 提取签名前的"初稿"版本：不含注释/附件/签名，可作为独立文档使用
        var draft = OfdVersioner.ExtractVersion(signed, "v1");
        Assert.Null(ReadEntry(draft, "Doc_0/Annots/Annotations.xml"));
        Assert.Null(ReadEntry(draft, "Doc_0/Attachments.xml"));
        Assert.Null(ReadEntry(draft, "Doc_0/Signatures.xml"));
        Assert.Empty(OfdVersioner.GetVersions(draft));

        // 提取出的历史版本可独立完成新一轮签署与验签（自洽）
        var reSigned = OfdSigner.Sign(draft, signerKp, signerCert,
            "2026-08-23 11:00:00", seal: seal, isSeal: true, version: 4);
        var verifyAgain = OfdValidator.Verify(reSigned);
        Assert.True(verifyAgain.AllValid,
            verifyAgain.Error ?? string.Join(" | ", verifyAgain.Signatures.Select(s => s.Error)));
    }
}
