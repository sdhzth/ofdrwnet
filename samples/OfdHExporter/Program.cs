using NetOfd.Emr;

namespace OfdHExporter;

/// <summary>
/// OfdHExporter —— OFD-H 电子病历结构化数据导出器。
/// <para>
/// 用法:
///   OfdHExporter &lt;in.ofd&gt; [--out &lt;输出目录&gt;] [--format json|xml|text|all]
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

        var inPath = args[0];
        string? outDir = null;
        var format = "all";

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--out" when i + 1 < args.Length:
                    outDir = args[++i];
                    break;
                case "--format" when i + 1 < args.Length:
                    format = args[++i].ToLowerInvariant();
                    break;
                default:
                    Console.Error.WriteLine($"未知参数: {args[i]}");
                    PrintUsage();
                    return 1;
            }
        }

        if (!File.Exists(inPath))
        {
            Console.Error.WriteLine($"文件不存在: {inPath}");
            return 1;
        }

        try
        {
            var result = EmrStructuredExporter.Export(inPath);
            if (!result.Found)
            {
                Console.Error.WriteLine("该 OFD 未包含结构化数据标引（CustomTags）。");
                return 3;
            }

            Console.WriteLine(EmrStructuredExporter.ToText(result));

            if (outDir is not null)
            {
                Directory.CreateDirectory(outDir);
                var baseName = Path.GetFileNameWithoutExtension(inPath);

                if (format is "json" or "all")
                {
                    var p = Path.Combine(outDir, baseName + "_结构化数据.json");
                    File.WriteAllBytes(p, EmrStructuredExporter.ToJson(result));
                    Console.WriteLine($"[写出] {p}");
                }
                if (format is "xml" or "all")
                {
                    if (result.SharedDocumentXml is not null)
                    {
                        var p = Path.Combine(outDir, baseName + "_WS_T500共享文档.xml");
                        File.WriteAllBytes(p, result.SharedDocumentXml);
                        Console.WriteLine($"[写出] {p}");
                    }
                    else
                    {
                        Console.WriteLine("[跳过] 未找到 WS/T 500 共享文档标引");
                    }
                }
                if (format is "text" or "all")
                {
                    var p = Path.Combine(outDir, baseName + "_结构化数据.txt");
                    File.WriteAllText(p, EmrStructuredExporter.ToText(result), new System.Text.UTF8Encoding(false));
                    Console.WriteLine($"[写出] {p}");
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"导出失败: {ex.Message}");
            return 2;
        }
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            OfdHExporter —— OFD-H 电子病历结构化数据导出器

            用法:
              OfdHExporter <in.ofd> [--out <输出目录>] [--format json|xml|text|all]

            参数:
              <in.ofd>             输入 OFD 文件路径
              --out <目录>         导出目录（不指定则仅在控制台输出）
              --format <格式>      导出格式：json（WS 445 数据元）/ xml（WS/T 500 共享文档）
                                   / text（可读文本）/ all（默认，全部导出）

            退出码:
              0  导出成功
              1  参数错误
              2  导出过程异常
              3  文件中未找到结构化数据标引
            """);
    }
}
