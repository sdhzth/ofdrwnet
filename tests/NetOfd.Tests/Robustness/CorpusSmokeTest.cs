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
/// <para>
/// 语料库位置按以下顺序解析，解析不到时本用例跳过（而非失败）——CI 与本地环境差异不应导致红灯：
/// </para>
/// <list type="number">
///   <item>环境变量 <c>NETOFD_CORPUS_ROOT</c></item>
///   <item>从测试程序集目录逐级向上查找同级的 <c>ofdrw-master</c> 目录</item>
///   <item>历史默认路径 <c>E:\电子病历\ofdrw-master</c></item>
/// </list>
/// </summary>
public class CorpusSmokeTest
{
    private readonly ITestOutputHelper _output;

    public CorpusSmokeTest(ITestOutputHelper output) => _output = output;

    /// <summary>语料库根目录解析结果；null 表示当前环境没有语料库。</summary>
    private static string? ResolveCorpusRoot()
    {
        // ① 环境变量优先（CI 用它指向浅克隆的 ofdrw 仓库）
        var env = Environment.GetEnvironmentVariable("NETOFD_CORPUS_ROOT");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
            return Path.GetFullPath(env);

        // ② 从测试程序集目录逐级向上找同级的 ofdrw-master
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "ofdrw-master");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }

        // ③ 历史默认路径
        const string legacy = @"E:\电子病历\ofdrw-master";
        if (Directory.Exists(legacy)) return legacy;

        return null;
    }

    [Fact]
    public void JavaCorpus_OpenRenderExtract_ShouldNotCrash()
    {
        var corpusRoot = ResolveCorpusRoot();
        if (corpusRoot is null)
        {
            _output.WriteLine("[CORPUS] 跳过：当前环境未找到 Java ofdrw 语料库。");
            _output.WriteLine("[CORPUS] 可通过环境变量 NETOFD_CORPUS_ROOT 指定，或将 ofdrw-master 置于仓库同级目录。");
            return;
        }

        var files = Directory.EnumerateFiles(corpusRoot, "*.ofd", SearchOption.AllDirectories)
            .Where(f => !f.Contains("obj", StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.True(files.Count >= 30, $"语料不足: 仅 {files.Count} 个（根目录 {corpusRoot}）");

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
