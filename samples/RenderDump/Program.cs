using NetOfd.Converter;
using NetOfd.Reader;
using SkiaSharp;

// 像素级比对降级方案（2026-10-23）：
// 本机无 Maven/ofdrw 完整 jar，无法跑 Java 参考渲染做像素级 diff；
// 退而求其次：把有代表性的 Java 官方测试 OFD 渲染成 PNG 落盘，
// 供人工目视检查渲染质量（文字/布局/图像/印章/透明度/路径）。
//
// 用法：dotnet run --project samples/RenderDump -- [--root <ofdrw-master>] [--out <dir>] [--ppm 4]
// 单文件模式：--file <ofd路径> --page <n> [--ppm] [--out] —— 只渲染指定文件指定页（调试用）
// 行为：渲染成功即写 PNG + 汇总落盘 dump_result.txt；任一失败进程退出码非 0。

string root = Arg("--root", @"E:/电子病历/ofdrw-master");
string outDir = Arg("--out", @"E:/电子病历/netofd/render_dump");
double ppm = double.Parse(Arg("--ppm", "4"));
string? singleFile = Array.IndexOf(args, "--file") is int fi and >= 0 ? args[fi + 1] : null;
int singlePage = Array.IndexOf(args, "--page") is int pi and >= 0 ? int.Parse(args[pi + 1]) : 1;

string Arg(string key, string def)
{
    int i = Array.IndexOf(args, key);
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : def;
}

// 代表性样例清单（覆盖各渲染特性：大文档/多页/发票/监制章/JPEG/水印/透明度/横排/路径裁剪/签名/最小样例）
(string file, int page)[] picks =
{
    ("intro-数科.ofd", 1),
    ("doc1File.ofd", 1),
    ("doc1File.ofd", 6),
    ("发票示例.ofd", 1),
    ("发票监制章-数科.ofd", 1),
    ("containsJPEG.ofd", 1),
    ("AddWatermarkAnnot.ofd", 1),
    ("透明度文字.ofd", 1),
    ("文字横向-数科.ofd", 1),
    ("testPathClip.ofd", 1),
    ("DigitalSign.ofd", 1),
    ("hello.ofd", 1),
};

Directory.CreateDirectory(outDir);

// 按文件名定位样例（ofdrw-master 测试资源分布在多个子目录，取第一个匹配）
var all = Directory.EnumerateFiles(root, "*.ofd", SearchOption.AllDirectories)
    .Where(p => p.Replace('\\', '/').Contains("/src/test/resources/"))
    .GroupBy(p => Path.GetFileName(p)!, StringComparer.OrdinalIgnoreCase)
    .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

var sb = new System.Text.StringBuilder();
void Log(string s) { Console.WriteLine(s); sb.AppendLine(s); }

int ok = 0, fail = 0;

// 单文件调试模式
if (singleFile != null)
{
    Directory.CreateDirectory(outDir);
    string pngPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(singleFile)}_p{singlePage}.png");
    try
    {
        using var maker = new OfdImageMaker(singleFile);
        Log($"单文件模式: {singleFile} 共{maker.PageCount}页 PPM={ppm}");

        // 注释树诊断：--annots 时打印 GetAnnotationEntities 解析结果后退出
        if (args.Contains("--annots"))
        {
            using var r = new OFDReader(singleFile);
            var ents = r.GetAnnotationEntities();
            Log($"注释实体数: {ents.Count}");
            foreach (var ent in ents)
            {
                Log($"  PageId={ent.PageId} Annots={ent.Annots?.Count ?? 0}");
                foreach (var an in ent.Annots ?? new List<NetOfd.Core.Annotation.PageAnnot.Annot>())
                {
                    var ap = an.Appearance;
                    var b = ap?.Boundary;
                    Log($"    Annot ID={an.ID} Type={an.Type} Visible={an.Visible} Appearance={(ap is null ? "null" : $"Boundary={b?.TopLeftX},{b?.TopLeftY},{b?.Width},{b?.Height} 子元素={string.Join(",", ap!.Element.Elements().Select(e => e.Name.LocalName))}")}");
                }
            }
            Environment.Exit(0);
        }

        using var bmp = maker.MakePage(singlePage - 1, new ConverterOptions { PPM = ppm });
        if (bmp == null) { Log("[FAIL] 渲染返回 null"); Environment.Exit(1); }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);

        // 可选裁剪（mm，页坐标）：--crop x,y,w,h
        int cropIdx = Array.IndexOf(args, "--crop");
        if (cropIdx >= 0 && cropIdx + 1 < args.Length)
        {
            var parts = args[cropIdx + 1].Split(',');
            if (parts.Length == 4 &&
                double.TryParse(parts[0], out var cx) && double.TryParse(parts[1], out var cy) &&
                double.TryParse(parts[2], out var cw) && double.TryParse(parts[3], out var ch))
            {
                int ix = (int)Math.Max(0, Math.Round(cx * ppm));
                int iy = (int)Math.Max(0, Math.Round(cy * ppm));
                int iw = (int)Math.Min(bmp.Width - ix, Math.Round(cw * ppm));
                int ih = (int)Math.Min(bmp.Height - iy, Math.Round(ch * ppm));
                if (iw > 0 && ih > 0)
                {
                    using var sub = img.Subset(new SKRectI(ix, iy, ix + iw, iy + ih));
                    if (sub is null) { Log("[WARN] 裁剪失败，输出整页"); }
                    else
                    {
                        using var subData = sub.Encode(SKEncodedImageFormat.Png, 100);
                        string cropPath = Path.Combine(outDir, $"{Path.GetFileNameWithoutExtension(singleFile)}_p{singlePage}_crop.png");
                        using (var fs = File.Create(cropPath)) subData.SaveTo(fs);
                        Log($"[OK] 裁剪 {cropPath} {iw}x{ih}");
                        Environment.Exit(0);
                    }
                }
            }
            Log("[WARN] --crop 参数解析失败，输出整页");
        }

        using (var fs = File.Create(pngPath)) data.SaveTo(fs);
        Log($"[OK] {pngPath} {bmp.Width}x{bmp.Height}");
        Environment.Exit(0);
    }
    catch (Exception ex)
    {
        Log($"[FAIL] {ex.GetType().Name}: {ex.Message}");
        Environment.Exit(1);
    }
}

Directory.CreateDirectory(outDir);

Log($"RenderDump: 输出目录={outDir} PPM={ppm} 样例数={picks.Length}");
foreach (var (file, page) in picks)
{
    string? path = all.TryGetValue(file, out var p) ? p : null;
    if (path == null) { Log($"[MISS] {file} —— ofdrw-master 中未找到"); fail++; continue; }
    string pngName = $"{Path.GetFileNameWithoutExtension(file)}_p{page}.png";
    string pngPath = Path.Combine(outDir, pngName);
    try
    {
        using var maker = new OfdImageMaker(path);
        if (page < 1 || page > maker.PageCount)
        {
            Log($"[FAIL] {file} 第{page}页超出范围（共{maker.PageCount}页）");
            fail++;
            continue;
        }
        using var bmp = maker.MakePage(page - 1, new ConverterOptions { PPM = ppm });
        if (bmp == null) { Log($"[FAIL] {file} 第{page}页渲染返回 null"); fail++; continue; }
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        long len;
        using (var fs = File.Create(pngPath))
        {
            data.SaveTo(fs);
            len = fs.Length;
        }
        var kb = len / 1024.0;
        Log($"[OK]   {pngName,-40} {bmp.Width}x{bmp.Height} {kb:F0} KB");
        ok++;
    }
    catch (Exception ex)
    {
        var msg = ex.Message.Replace("\r", " ").Replace("\n", " ");
        if (msg.Length > 160) msg = msg[..160];
        Log($"[FAIL] {file} {ex.GetType().Name}: {msg}");
        fail++;
    }
}
Log($"\n完成: OK={ok} FAIL={fail} 共 {picks.Length}");

string resultPath = Path.Combine(outDir, "dump_result.txt");
File.WriteAllText(resultPath, sb.ToString());
Log($"结果已写入 {resultPath}");

Environment.Exit(fail > 0 ? 1 : 0);
