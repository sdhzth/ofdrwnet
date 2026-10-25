using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Reader;
using NetOfd.Sign;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace NetOfd.Tests.Stress;

/// <summary>
/// 随机化压力测试（固定种子，可复现）：混合执行注释/加页/附件/自定义标/版本快照共 30 步，
/// 每步验证文档可解析、页数正确、MaxUnitID 不回退；最终签名+验签仍须通过。
/// 用于捕获编辑器链式调用中的对象 ID 冲突与结构漂移。
/// </summary>
public class StressTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_stress_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private class TinyDrawer : IDrawer
    {
        public string Text { get; set; } = "压";

        public void Draw(DrawContext ctx)
        {
            ctx.FillStyle = "#101010";
            ctx.Font = "3mm SimSun";
            ctx.FillText(Text, 2, 5);
        }
    }

    private static byte[] BuildSampleOfdBytes(int pages)
    {
        var path = Out("stress_base.ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            for (int i = 0; i < pages; i++) doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("压力测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    private static int GetMaxUnitID(byte[] ofd)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read);
        var entry = za.GetEntry("Doc_0/Document.xml") ?? throw new InvalidOperationException("缺少 Document.xml");
        using var s = entry.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        var cdata = XDocument.Parse(Encoding.UTF8.GetString(buf.ToArray())).Descendants()
            .First(e => e.Name.LocalName == "CommonData");
        return int.TryParse(cdata.Attribute("MaxUnitID")?.Value, out var v) ? v : 0;
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    [Fact]
    public void RandomEditChain_30Ops_ShouldStayConsistent()
    {
        // 固定种子保证可复现
        var rng = new Random(20260823);
        const int initialPages = 3;
        var doc = BuildSampleOfdBytes(initialPages);
        int pageCount = initialPages;
        int versionCount = 0;
        int lastMaxId = GetMaxUnitID(doc);

        var attachFile = Out("stress_attach.bin");
        File.WriteAllBytes(attachFile, new byte[1024]);
        var opLog = new List<string>();

        for (int op = 0; op < 30; op++)
        {
            switch (rng.Next(6))
            {
                case 0:
                case 1:
                {
                    var pageNo = rng.Next(pageCount) + 1;
                    opLog.Add($"op{op}: annotate page{pageNo}");
                    doc = OfdAnnotator.AddAnnotation(doc, pageNo,
                        new Annotation(rng.Next(1, 50), rng.Next(1, 50),
                            rng.Next(10, 60), rng.Next(8, 30), AnnotType.Path,
                            new TinyDrawer { Text = $"批{op}" }));
                    break;
                }
                case 2:
                    opLog.Add($"op{op}: appendPage");
                    doc = OfdEditor.AppendPage(doc);
                    pageCount++;
                    break;
                case 3:
                    opLog.Add($"op{op}: attach");
                    doc = OfdAttacher.AddAttachment(doc,
                        new Attachment($"附件{op}", attachFile));
                    break;
                case 4:
                    opLog.Add($"op{op}: customTag");
                    doc = OfdTagger.AddCustomTag(doc, $"urn:stress:{op}",
                        Encoding.UTF8.GetBytes($"<T n=\"{op}\"/>"));
                    break;
                case 5:
                    opLog.Add($"op{op}: version");
                    doc = OfdVersioner.AddVersion(doc, $"版本{op}", out _);
                    versionCount++;
                    break;
            }

            // ── 每步不变量校验 ──
            var curPath = Out("stress_cur.ofd");
            File.WriteAllBytes(curPath, doc);
            using (var reader = new OFDReader(curPath))
            {
                if (reader.GetPageCount() != pageCount)
                    File.WriteAllText(Out("stress_ops.log"), string.Join(Environment.NewLine, opLog));
                Assert.Equal(pageCount, reader.GetPageCount());
            }
            var curMax = GetMaxUnitID(doc);
            Assert.True(curMax >= lastMaxId,
                $"第 {op} 步后 MaxUnitID 回退: {lastMaxId} -> {curMax}");
            lastMaxId = curMax;
        }

        Assert.True(pageCount >= initialPages);

        // ── 终态：全部结构共存且可签署验签 ──
        Assert.Equal(versionCount, OfdVersioner.GetVersions(doc).Count);
        var (kp, cert) = MakeKeys("Stress Signer");
        var signed = OfdSigner.Sign(doc, kp, cert, "2026-08-23 13:00:00");
        var result = OfdValidator.Verify(signed);
        Assert.True(result.AllValid,
            result.Error ?? string.Join(" | ", result.Signatures.Select(s => s.Error)));

        // 签名后的文档仍可继续编辑（提取首个版本自洽）
        var versions = OfdVersioner.GetVersions(signed);
        if (versions.Count > 0)
        {
            var extracted = OfdVersioner.ExtractVersion(signed, versions[0].ID!);
            Assert.NotNull(extracted);
            Assert.Empty(OfdVersioner.GetVersions(extracted));
        }
    }
}
