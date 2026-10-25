using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NetOfd.Converter;
using NetOfd.Reader;
using Xunit;
using Xunit.Abstractions;

namespace NetOfd.Tests.Robustness;

/// <summary>
/// 真实文档语料库冒烟：把 Java ofdrw 全部测试 OFD（约 60+ 个，含签名/加密/注释/归档等）
/// 逐个执行 打开 → 渲染首页 → 全文提取，捕捉崩溃级缺陷。
/// 通过率阈值 80%（个别语料为故意损坏/加密样本，允许合理失败，但失败明细必须全部打印）。
/// </summary>
public class CorpusSmokeTest
{
    private readonly ITestOutputHelper _output;

    public CorpusSmokeTest(ITestOutputHelper output) => _output = output;

    private const string CorpusRoot = @"E:\电子病历\ofdrw-master";

    [Fact]
    public void JavaCorpus_OpenRenderExtract_ShouldNotCrash()
    {
        var files = Directory.EnumerateFiles(CorpusRoot, "*.ofd", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(files.Count >= 30, $"语料不足: 仅 {files.Count} 个");

        int ok = 0;
        var failures = new List<string>();
        var sw = Stopwatch.StartNew();

        foreach (var f in files)
        {
            try
            {
                using (var reader = new OFDReader(f))
                {
                    if (reader.GetPageCount() > 0)
                    {
                        using var maker = new OfdImageMaker(f);
                        using var bmp = maker.MakePage(0, new ConverterOptions { PPM = 4 });
                        Assert.NotNull(bmp);

                        var texts = ContentExtractor.ExtractAllPagesText(reader.GetPages().ToList());
                        _ = texts.Count;
                    }
                }
                ok++;
            }
            catch (Exception ex)
            {
                var msg = ex.Message.Replace("\r", " ").Replace("\n", " ");
                if (msg.Length > 120) msg = msg[..120];
                failures.Add($"{Path.GetFileName(f)} [{ex.GetType().Name}] {msg}");
            }
        }
        sw.Stop();

        double rate = (double)ok / files.Count;
        _output.WriteLine($"[CORPUS] 通过 {ok}/{files.Count} ({rate:P0})，耗时 {sw.ElapsedMilliseconds} ms");
        foreach (var line in failures)
            _output.WriteLine($"[CORPUS-FAIL] {line}");

        Assert.True(rate >= 0.8,
            $"语料通过率过低 {ok}/{files.Count}\n{string.Join("\n", failures)}");
    }
}
