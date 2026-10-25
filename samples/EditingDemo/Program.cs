using System.Text;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Converter;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element.Canvas;

namespace EditingDemo;

/// <summary>
/// 编辑类 API 综合示例：批量编辑会话、水印、注释、附件、版本快照与并行渲染。
/// 输出到当前目录的 EditingDemoOutput/。
/// </summary>
public static class Program
{
    private sealed class StampDrawer : IDrawer
    {
        public void Draw(DrawContext ctx)
        {
            ctx.LineWidth = 0.5;
            ctx.StrokeStyle = "#CC2222";
            ctx.StrokeRect(1, 1, 28, 12);
            ctx.FillStyle = "#CC2222";
            ctx.Font = "4mm SimSun";
            ctx.FillText("已审核", 6, 12);
        }
    }

    public static void Main()
    {
        string outDir = Path.Combine(AppContext.BaseDirectory, "EditingDemoOutput");
        Directory.CreateDirectory(outDir);

        // ── 1. 创建基础文档（3 页） ────────────────────────────
        string basePath = Path.Combine(outDir, "base.ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(basePath))
        {
            for (int i = 0; i < 3; i++) doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("编辑 API 演示文档").SetFontSize(6d));
            doc.AddWatermark(new NetOfd.Layout.Edit.Watermark(NetOfd.Layout.PageLayout.A4())
                .SetValue("内部资料")
                .SetColor("#8899BB"));
        }
        var ofd = File.ReadAllBytes(basePath);
        Console.WriteLine($"基础文档: {basePath}");

        // ── 2. 批量编辑会话：N 次操作仅一次落盘 ──────────────
        string editedPath = Path.Combine(outDir, "edited.ofd");
        using (var session = OfdEditSession.Open(ofd))
        {
            for (int p = 1; p <= session.PageCount; p++)
            {
                session.AddAnnotation(p, new Annotation(
                    120, 15, 30, 14, AnnotType.Path, new StampDrawer()));
            }
            session.AddAttachment(new Attachment("说明文件",
                WriteTextFile(outDir, "readme.txt", "这是随文档附带的说明。")));
            session.AddCustomTag("urn:demo:keyword", Encoding.UTF8.GetBytes("<关键词>演示</关键词>"));
            int v = session.AddVersion("批注定稿");
            Console.WriteLine($"批量编辑完成（{session.PageCount} 页批注），版本序号 {v}");
            File.WriteAllBytes(editedPath, session.Save());   // 显式落盘
        }

        // ── 3. 版本提取：还原无批注的初始状态 ─────────────────
        var versions = OfdVersioner.GetVersions(File.ReadAllBytes(editedPath));
        if (versions.Count > 0)
        {
            string draftPath = Path.Combine(outDir, "draft_v1.ofd");
            File.WriteAllBytes(draftPath,
                OfdVersioner.ExtractVersion(File.ReadAllBytes(editedPath), versions[0].ID!));
            Console.WriteLine($"历史版本 {versions[0].ID} 已提取: {draftPath}");
        }

        // ── 4. 并行渲染为 PNG ─────────────────────────────────
        string imgDir = Path.Combine(outDir, "pages");
        using (var exporter = new ImageExporter(editedPath, imgDir, "png", ppm: 8)
               { ParallelExport = true })
        {
            var pages = exporter.Export();
            Console.WriteLine($"并行渲染 {pages.Count} 页 -> {imgDir}");
        }
    }

    private static string WriteTextFile(string dir, string name, string content)
    {
        string path = Path.Combine(dir, name);
        File.WriteAllText(path, content, Encoding.UTF8);
        return path;
    }
}
