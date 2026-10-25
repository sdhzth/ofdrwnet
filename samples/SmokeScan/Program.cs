using NetOfd.Reader;
using NetOfd.Layout;
using NetOfd.Pkg;
using NetOfd.Converter;
using SkiaSharp;

// 批量冒烟：扫描 Java ofdrw-master 全部测试资源 *.ofd
//  默认：解析+抽文本   --render：额外渲染每样本前 maxPages 页并做非空白检查
// 增强（2026-09-17）：汇总写入 smoke_result.txt + 失败时进程退出码非 0（CI 门禁友好）
string root = args.Length >= 2 && args[0] == "--root" ? args[1]
    : @"E:/电子病历/ofdrw-master";
bool doRender = args.Contains("--render");
int maxPages = 2;

// 汇总缓冲：同时输出到控制台与结果文件，规避某些环境下 dotnet stdout 被吞
var sb = new System.Text.StringBuilder();
void Log(string s) { Console.WriteLine(s); sb.AppendLine(s); }

var files = Directory.EnumerateFiles(root, "*.ofd", SearchOption.AllDirectories)
    .Where(p => p.Replace('\\', '/').Contains("/src/test/resources/"))
    .DistinctBy(p => Path.GetFileName(p))
    .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
    .ToList();

Log($"共 {files.Count} 个去重样本，渲染模式={doRender}，开始…\n");
int ok = 0, fail = 0;
foreach (var f in files)
{
    var name = Path.GetFileName(f);
    Console.Out.Flush();
    try
    {
        using var reader = new OFDReader(f);
        int pages = reader.GetPageCount();
        if (doRender)
        {
            using var maker = new OfdImageMaker(f);
            int to = Math.Min(pages, maxPages);
            for (int i = 0; i < to; i++)
            {
                var bmp = maker.MakePage(i);
                if (bmp == null) { Log($"[FAIL] {name,-38} 第{i}页渲染返回 null"); fail++; continue; }
                bool blank = IsBlank(bmp);
                Log($"[OK]   {name,-38} 页{i + 1}/{pages} {bmp.Width}x{bmp.Height} {(blank ? "blank" : "content")}");
                bmp.Dispose();
            }
        }
        else
        {
            var texts = NetOfd.Reader.ContentExtractor.ExtractAllPagesText(reader.GetPages());
            string joined = string.Join("", texts);
            string snippet = joined.Length > 0 ? joined[..Math.Min(50, joined.Length)].Replace("\n", "⏎") : "(空)";
            Log($"[OK]   {name,-38} 页={pages,-3} 文本={joined.Length,-6} 片段: {snippet}");
        }
        ok++;
    }
    catch (Exception ex)
    {
        var msg = ex.Message.Replace("\r", " ").Replace("\n", " ");
        if (msg.Length > 140) msg = msg[..140];
        Log($"[FAIL] {name,-38} {ex.GetType().Name}: {msg}");
        fail++;
    }
}
Log($"\n完成: OK={ok} FAIL={fail} 共 {files.Count}");
Console.Out.Flush();

string resultPath = "smoke_result.txt";
File.WriteAllText(resultPath, sb.ToString());
Log($"结果已写入 {Path.GetFullPath(resultPath)}");

// CI 门禁：有失败则非零退出
Environment.Exit(fail > 0 ? 1 : 0);

// 全量步长扫描：复制像素后按大步长抽样，避免小字内容漏检
static bool IsBlank(SKBitmap bmp)
{
    int w = bmp.Width, h = bmp.Height;
    if (w <= 0 || h <= 0) return true;
    var px = bmp.Bytes;
    if (px == null || px.Length < w * h * 4) return true;
    int stride = 7;
    for (int y = 0; y < h; y += stride)
    {
        int rowBase = y * w * 4;
        for (int x = 0; x < w; x += stride)
        {
            int i = rowBase + x * 4;
            byte a = px[i + 3];
            if (a == 0) continue;
            byte c0 = px[i], c1 = px[i + 1], c2 = px[i + 2];
            if (c0 < 248 || c1 < 248 || c2 < 248) return false;
        }
    }
    return true;
}
