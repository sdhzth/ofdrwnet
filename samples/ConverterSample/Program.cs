using System;
using System.IO;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Core.BasicType;
using NetOfd.Converter;

// 工作区输出目录（便于直接预览/交付）
var workDir = @"C:\Users\Administrator\WorkBuddy\2026-08-08-07-15-44";
var outDir = Path.Combine(workDir, "demo_output");
Directory.CreateDirectory(outDir);
var ofdPath = Path.Combine(outDir, "demo.ofd");

using (var doc = new OFDDoc(ofdPath))
{
    // 第 1 页：通过布局引擎写入文字
    doc.Add(new Paragraph().AddText("NetOfd 图像导出示例 (OFD → PNG)").SetFontSize(16d));
    doc.Add(new Paragraph().AddText("这是通过 .NET 移植版 ofdrw-converter 渲染的 OFD 页面。").SetFontSize(10d));
    doc.Add(new Paragraph().AddText("中文与 English 混排渲染验证 / 医疗文书样例。").SetFontSize(8d));

    // 第 2 页：通过 DrawContext 绘制矢量图形
    doc.AddPage();
    var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
    // 红色填充矩形
    ctx.FillStyle = "#ff0000";
    ctx.FillRect(20, 20, 80, 40);
    // 蓝色描边矩形
    ctx.StrokeStyle = "#0000ff";
    ctx.LineWidth = 1;
    ctx.StrokeRect(20, 80, 80, 40);
    // 黑色文字
    ctx.FillStyle = "#000000";
    ctx.Font = "14mm SimSun";
    ctx.FillText("红色填充 + 蓝色描边矩形", 20, 150);
}

var imgDir = Path.Combine(outDir, "images");
using var exporter = new ImageExporter(ofdPath, imgDir, "png", ppm: 15);
var paths = exporter.Export();

Console.WriteLine($"OFD 文件: {ofdPath}");
foreach (var p in paths)
    Console.WriteLine($"渲染图像: {p}");

// 复制到工作区根目录便于 present_files 预览
for (int i = 0; i < paths.Count; i++)
{
    var dest = Path.Combine(workDir, $"demo_page{i}.png");
    File.Copy(paths[i], dest, overwrite: true);
    Console.WriteLine($"已复制: {dest}");
}
Console.WriteLine("OK");
