using NetOfd.Core.BasicType;
using NetOfd.Layout;
using SkiaSharp;

namespace OfdWriter;

/// <summary>
/// 用 .NET NetOfd 生成一份 OFD 文档，用于验证 Java OFDRW 能否读取。
/// 输出：可执行目录下的 dotnet_sample.ofd（2 页）。
/// </summary>
public static class Program
{
    public static void Main()
    {
        string outDir = Path.Combine(AppContext.BaseDirectory, "OfdWriterOutput");
        Directory.CreateDirectory(outDir);

        // 生成一张小图片（蓝色方块）用于嵌入
        string imgPath = Path.Combine(outDir, "logo_dotnet.png");
        GenerateLogoPng(imgPath);

        string outPath = Path.Combine(outDir, "dotnet_sample.ofd");

        using var doc = new OFDDoc(outPath);
        doc.Width = 210;
        doc.Height = 297;

        // ── 第 0 页 ──────────────────────────────────────────
        doc.AddPage();
        var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));

        // 蓝色标题栏
        ctx.FillStyle = "#1F4E79";
        ctx.FillRect(0, 0, 210, 16);
        ctx.FillStyle = "#FFFFFF";
        ctx.Font = "bold 6mm SimSun";
        ctx.FillText("NetOfd 生成示例文档（.NET → Java 解析验证）", 8, 8);

        // 彩色文本：红色 / 绿色 / 蓝色粗体
        ctx.FillStyle = "#FF0000";
        ctx.Font = "5mm SimSun";
        ctx.FillText("红色示例文本", 10, 40);

        ctx.FillStyle = "#008000";
        ctx.FillText("绿色示例文本", 10, 55);

        ctx.FillStyle = "#0000FF";
        ctx.Font = "bold 5mm SimSun";
        ctx.FillText("蓝色粗体示例文本", 10, 70);

        // 红色描边矩形（仅边框）
        ctx.StrokeStyle = "#FF0000";
        ctx.LineWidth = 0.5;
        ctx.StrokeRect(10, 90, 80, 30);
        ctx.FillStyle = "#000000";
        ctx.Font = "3mm SimSun";
        ctx.FillText("边框框", 15, 110);

        // 黄色填充 + 蓝色描边矩形
        ctx.FillStyle = "#FFFF00";
        ctx.FillRect(110, 90, 80, 30);
        ctx.StrokeStyle = "#0000FF";
        ctx.LineWidth = 0.5;
        ctx.StrokeRect(110, 90, 80, 30);
        ctx.FillStyle = "#000000";
        ctx.FillText("黄填充蓝框", 115, 110);

        // 嵌入图片
        ctx.DrawImageFromFile(imgPath, 10, 140, 40, 40);
        ctx.FillStyle = "#000000";
        ctx.Font = "3mm SimSun";
        ctx.FillText("嵌入图片", 55, 165);

        // ── 第 1 页（纯文本，验证多页遍历）──────────────────────
        doc.AddPage();
        var ctx2 = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
        ctx2.FillStyle = "#000000";
        ctx2.Font = "5mm SimSun";
        ctx2.FillText("第二页：仅包含文本，用于校验多页遍历与文本提取。", 15, 30);
        ctx2.FillText("Java OFDRW 应当能够从此 OFD 中正确读取两页并提取全部文字。", 15, 45);

        doc.Close();
        Console.WriteLine($"已生成：{outPath}");
    }

    /// <summary>生成一张 100x100 的蓝色方块 PNG（带文字）。</summary>
    private static void GenerateLogoPng(string path)
    {
        const int s = 100;
        using var bmp = new SKBitmap(s, s);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(SKColors.SteelBlue);
        using var paint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 22,
            IsAntialias = true,
            TextAlign = SKTextAlign.Center
        };
        canvas.DrawText(".NET", s / 2f, s / 2f + 8, paint);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
    }
}
