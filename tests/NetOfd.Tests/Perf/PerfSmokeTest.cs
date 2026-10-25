using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using NetOfd.Converter;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Sign;
using NetOfd.Tool;
using Org.BouncyCastle.Crypto;
using Xunit;
using Xunit.Abstractions;

namespace NetOfd.Tests.Perf;

/// <summary>
/// 性能冒烟基准：测量关键操作耗时并断言宽松上限（兼作性能回归护栏）。
/// 运行：dotnet test --filter "FullyQualifiedName~PerfSmokeTest"
/// </summary>
public class PerfSmokeTest
{
    private readonly ITestOutputHelper _output;

    public PerfSmokeTest(ITestOutputHelper output) => _output = output;

    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_perf_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private class TextDrawer : IDrawer
    {
        public string Text { get; set; } = "性能测试";

        public void Draw(DrawContext ctx)
        {
            ctx.FillStyle = "#202040";
            ctx.Font = "4mm SimSun";
            ctx.FillText(Text, 20, 30);
        }
    }

    /// <summary>
    /// 构造 pages 页 OFD 文档。
    /// 注：早期版本在此再追加 60 段流式段落——在旧「无 y 推进」引擎下段落叠画进最后一页而不增页；
    /// 流式引擎 v1 修复后段落会正确溢出新建页面（页数不再恒等于 pages），故段落样本已移除，
    /// 本方法聚焦「N 页文档」的构建/编辑/签名/渲染全链路基准。
    /// </summary>
    private static byte[] BuildMultiPageDoc(int pages, string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            for (int i = 0; i < pages; i++) doc.AddPage();
        }
        return File.ReadAllBytes(path);
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    /// <summary>计时辅助：返回耗时毫秒。</summary>
    private static long Time(Action action)
    {
        var sw = Stopwatch.StartNew();
        action();
        sw.Stop();
        return sw.ElapsedMilliseconds;
    }

    [Fact]
    public void Baseline_KeyOperations_ShouldCompleteWithinBounds()
    {
        var results = new List<string>();

        // ── 1. 构建 20 页文档 ──
        byte[] doc = null!;
        var ms = Time(() => doc = BuildMultiPageDoc(20, "perf_base.ofd"));
        results.Add($"构建20页文档(60段): {ms} ms");
        Assert.True(ms < 30_000, $"文档构建过慢: {ms} ms");

        // ── 2. 编辑链（每次都是完整 zip 往返）──
        var attachFile = Out("perf_attach.txt");
        File.WriteAllBytes(attachFile, new byte[64 * 1024]);
        ms = Time(() =>
        {
            var d = OfdVersioner.AddVersion(doc, "初稿", out _);
            d = OfdAnnotator.AddAnnotation(d, 1,
                new Annotation(10, 10, 40, 16, AnnotType.Path,
                    new TextDrawer { Text = "批注" }));
            d = OfdAttacher.AddAttachment(d, new Attachment("附件", attachFile));
            d = OfdTagger.AddCustomTag(d, "urn:perf:test", Encoding.UTF8.GetBytes("<T/>"));
        });
        results.Add($"编辑链4步(快照/批注/附件/自定义标): {ms} ms");
        Assert.True(ms < 30_000, $"编辑链过慢: {ms} ms");

        // ── 3. SES_V4 签名 + 验签 ──
        var (kp, cert) = MakeKeys("Perf Signer");
        var (skp, scert) = MakeKeys("Perf Seal");
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        var sealDer = SesV4Seal.Build("SEAL-PERF", "v1", "基准单位", SesV4Seal.TypeUnit,
            png, "PNG", 30, 30, scert, skp,
            new DateTime(2024, 1, 1), new DateTime(2024, 1, 1), new DateTime(2034, 1, 1));
        var seal = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(sealDer);

        byte[] signed = null!;
        ms = Time(() => signed = OfdSigner.Sign(doc, kp, cert,
            "2026-08-23 12:00:00", seal: seal, isSeal: true, version: 4));
        results.Add($"SES_V4 签名: {ms} ms");
        Assert.True(ms < 15_000, $"签名过慢: {ms} ms");

        ms = Time(() =>
        {
            var r = OfdValidator.Verify(signed);
            Assert.True(r.AllValid, r.Error);
        });
        results.Add($"验签: {ms} ms");
        Assert.True(ms < 15_000, $"验签过慢: {ms} ms");

        // ── 4. 渲染前 5 页为 PNG ──
        var renderPath = Out("perf_render.ofd");
        File.WriteAllBytes(renderPath, doc);
        ms = Time(() =>
        {
            using var exporter = new ImageExporter(renderPath, Path.Combine(OutDir(), "img"), "png", ppm: 15);
            var paths = exporter.Export();
            Assert.Equal(20, paths.Count);
        });
        results.Add($"渲染20页PNG(15ppm): {ms} ms");
        Assert.True(ms < 60_000, $"渲染过慢: {ms} ms");

        // ── 5. 合并两个 10 页文档（OfdMerger 接收文件路径）──
        var pathA = Out("perf_merge_a.ofd");
        var pathB = Out("perf_merge_b.ofd");
        BuildMultiPageDoc(10, "perf_merge_a.ofd");
        BuildMultiPageDoc(10, "perf_merge_b.ofd");
        var mergedPath = Out("perf_merged.ofd");
        ms = Time(() =>
        {
            using var merger = new OfdMerger(mergedPath);
            merger.Add(pathA).Add(pathB);
        });
        results.Add($"合并2×10页: {ms} ms");
        Assert.True(File.Exists(mergedPath));

        foreach (var line in results) _output.WriteLine($"[PERF] {line}");
    }

    /// <summary>渲染耗时分解：光栅化与编码占比。信息性输出，无断言上限。
    /// 实测 PNG 编码占大头（约为光栅化的 30 倍）；追求速度可用 imageType:"jpg"。</summary>
    [Fact]
    public void Render_CostBreakdown()
    {
        var doc = BuildMultiPageDoc(5, "perf_breakdown.ofd");
        var ofdPath = Out("perf_breakdown.ofd");

        using var maker = new OfdImageMaker(ofdPath);
        var opts = new ConverterOptions { PPM = 15, AntiAlias = true };

        long makeMs = 0, pngMs = 0, jpgMs = 0;
        var bitmaps = new List<SkiaSharp.SKBitmap>();
        for (int i = 0; i < 5; i++)
        {
            var sw = Stopwatch.StartNew();
            bitmaps.Add(maker.MakePage(i, opts)!);
            sw.Stop();
            makeMs += sw.ElapsedMilliseconds;
        }

        foreach (var bmp in bitmaps)
        {
            using var img = SkiaSharp.SKImage.FromBitmap(bmp);

            var sw = Stopwatch.StartNew();
            using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100)) _ = data.ToArray();
            sw.Stop();
            pngMs += sw.ElapsedMilliseconds;

            sw.Restart();
            using (var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Jpeg, 90)) _ = data.ToArray();
            sw.Stop();
            jpgMs += sw.ElapsedMilliseconds;

            bmp.Dispose();
        }

        _output.WriteLine($"[PERF] 5页@15ppm 光栅化: {makeMs} ms | PNG编码: {pngMs} ms | JPEG(90)编码: {jpgMs} ms");
    }

    /// <summary>并行导出 vs 顺序导出耗时对比（信息性），并校验结果一致性。</summary>
    [Fact]
    public void Render_ParallelExport_Speedup()
    {
        var doc = BuildMultiPageDoc(10, "perf_par.ofd");
        File.WriteAllBytes(Out("perf_par.ofd"), doc);

        long seqMs, parMs;
        int seqCount, parCount;

        var sw = Stopwatch.StartNew();
        using (var ex = new ImageExporter(Out("perf_par.ofd"), Path.Combine(OutDir(), "par_seq"), "png", 15))
            seqCount = ex.Export().Count;
        sw.Stop();
        seqMs = sw.ElapsedMilliseconds;

        sw.Restart();
        using (var ex = new ImageExporter(Out("perf_par.ofd"), Path.Combine(OutDir(), "par_par"), "png", 15)
               { ParallelExport = true })
            parCount = ex.Export().Count;
        sw.Stop();
        parMs = sw.ElapsedMilliseconds;

        Assert.Equal(seqCount, parCount);
        _output.WriteLine($"[PERF] 10页PNG导出 顺序: {seqMs} ms | 并行: {parMs} ms | 加速比 {(double)seqMs / Math.Max(1, parMs):F1}x");
    }
}
