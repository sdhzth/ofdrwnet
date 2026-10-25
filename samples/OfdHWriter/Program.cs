namespace OfdHWriter;

/// <summary>
/// OfdHWriter —— GB/T 48666—2026 电子病历版式文档（OFD-H）生成器命令行入口。
/// <para>
/// 生成逻辑位于库模块 <c>NetOfd.Emr</c>（<see cref="NetOfd.Emr.OfdHDocumentGenerator"/>），
/// 本程序只负责参数解析、调用库 API、渲染预览图并打印结果。
/// </para>
/// <para>
/// 用法:
///   OfdHWriter &lt;out.ofd&gt; [--work &lt;工作目录&gt;] [--no-sign] [--font &lt;字型路径&gt;] [--no-preview]
/// </para>
/// <para>
/// 生成的文档含：3 页版式内容（住院病案首页 / 入院记录 / 辅助检查与诊疗计划）、
/// 嵌入仿宋字型、心电图附件、质控审核加注、版本快照、自定义标引（机读信息 + 结构化诊疗数据）、
/// SM2withSM3 国密数字签名与 GM/T 0099 防夹带保护。
/// </para>
/// </summary>
public static class Program
{
    public static int Main(string[] args)
    {
        if (args.Length < 1 || args[0] is "-h" or "--help" or "/?")
        {
            PrintUsage();
            return args.Length < 1 ? 1 : 0;
        }

        var outPath = args[0];
        string? workDir = null;
        string? fontPath = null;
        var noSign = false;
        var noPreview = false;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--work" when i + 1 < args.Length:
                    workDir = args[++i];
                    break;
                case "--font" when i + 1 < args.Length:
                    fontPath = args[++i];
                    break;
                case "--no-sign":
                    noSign = true;
                    break;
                case "--no-preview":
                    noPreview = true;
                    break;
                default:
                    Console.Error.WriteLine($"未知参数: {args[i]}");
                    PrintUsage();
                    return 1;
            }
        }

        try
        {
            var result = NetOfd.Emr.OfdHDocumentGenerator.Generate(new NetOfd.Emr.OfdHOptions
            {
                OutputPath = outPath,
                WorkDirectory = workDir,
                FontPath = fontPath,
                SkipSignature = noSign,
            });

            if (!result.Success)
            {
                Console.Error.WriteLine($"签名自检未通过: {result.Error}");
                return 3;
            }

            if (!noPreview)
                RenderPreview(outPath);

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"生成失败: {ex.Message}");
            return 2;
        }
    }

    /// <summary>渲染每页 PNG 预览图（输出目录下的 preview/ 子目录）。</summary>
    private static void RenderPreview(string outPath)
    {
        var fullPath = Path.GetFullPath(outPath);
        var previewDir = Path.Combine(Path.GetDirectoryName(fullPath)!, "preview");
        Directory.CreateDirectory(previewDir);
        using var exporter = new NetOfd.Converter.ImageExporter(fullPath, previewDir, "png", 12);
        var pages = exporter.Export();
        Console.WriteLine();
        Console.WriteLine($"── 渲染预览（{pages.Count} 页）→ {previewDir}");
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            OfdHWriter —— GB/T 48666—2026 电子病历版式文档（OFD-H）生成器

            用法:
              OfdHWriter <out.ofd> [--work <工作目录>] [--font <字型路径>] [--no-sign] [--no-preview]

            参数:
              <out.ofd>        输出文件路径（.ofd）
              --work <目录>    中间产物工作目录（默认：输出目录/_emr_work）
              --font <路径>    待嵌入的中文字型文件（默认按平台自动探测仿宋/中文字型）
              --no-sign        跳过国密签名与防夹带保护（仅调试结构时使用）
              --no-preview     不渲染 PNG 预览图

            退出码:
              0  生成成功且签名自检通过
              1  参数错误
              2  生成过程异常
              3  生成成功但签名自检失败
            """);
    }
}
