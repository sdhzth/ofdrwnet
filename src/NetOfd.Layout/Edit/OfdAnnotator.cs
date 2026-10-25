using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicType;
using NetOfd.Layout.Engine;
using NetOfd.Layout.Element.Canvas;

namespace NetOfd.Layout.Edit;

/// <summary>
/// OFD 注释编辑器：向已有 OFD 文档添加注释（批注）。
/// <para>
/// 采用与 <c>NetOfd.Sign.OfdSigner</c> 一致的映射（Dictionary）方式读写包内文件，
/// 无需虚拟容器即可完成注释结构创建：
/// 注释入口文件（Annotations.xml）、分页注释文件（Page_N/Annot_M.xml）、
/// 文档资源注册（字体/图片）。绘制由 <see cref="DrawContext"/> 完成。
/// </para>
/// 对应 Java: org.ofdrw.layout.edit.AnnotationRender + org.ofdrw.layout.OFDDoc#addAnnotation
/// </summary>
public static class OfdAnnotator
{
    /// <summary>向已有 OFD 字节中的指定页添加注释，返回新的 OFD 字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="pageNumber">页码（从 1 开始）</param>
    /// <param name="annotation">注释对象</param>
    public static byte[] AddAnnotation(byte[] inputOfd, int pageNumber, Annotation annotation)
        => AddAnnotation(inputOfd, pageNumber, new[] { annotation });

    /// <summary>向已有 OFD 字节中的指定页批量添加注释，返回新的 OFD 字节。</summary>
    public static byte[] AddAnnotation(byte[] inputOfd, int pageNumber, IReadOnlyList<Annotation> annotations)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        AddAnnotationCore(map, pageNumber, annotations);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>向已有 OFD 文件的指定页添加注释，输出新的 OFD 文件。</summary>
    public static void AddAnnotation(string inputPath, string outputPath, int pageNumber, Annotation annotation)
        => AddAnnotation(inputPath, outputPath, pageNumber, new[] { annotation });

    /// <summary>向已有 OFD 文件的指定页批量添加注释，输出新的 OFD 文件。</summary>
    public static void AddAnnotation(string inputPath, string outputPath, int pageNumber, IReadOnlyList<Annotation> annotations)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        AddAnnotationCore(map, pageNumber, annotations);
        ZipMapIO.WriteZip(outputPath, map);
    }

    internal static void AddAnnotationCore(Dictionary<string, byte[]> map, int pageNumber, IReadOnlyList<Annotation> annotations)
    {
        if (pageNumber < 1)
            throw new ArgumentException("页码（pageNumber）必须从 1 开始", nameof(pageNumber));
        if (annotations is null || annotations.Count == 0)
            throw new ArgumentException("注释列表不能为空", nameof(annotations));

        // ── 1. 解析 OFD.xml，定位文档目录 ─────────────────────────
        if (!map.TryGetValue("OFD.xml", out var ofdBytes))
            throw new InvalidOperationException("错误的OFD结构，缺少 OFD.xml 文件");
        var ofdRoot = new OFDRoot(XDocument.Parse(System.Text.Encoding.UTF8.GetString(ofdBytes)).Root!);
        var docBody = ofdRoot.GetFirstDocBody();
        var docLoc = docBody?.DocRoot?.Value ?? "Doc_0/Document.xml";
        var docPath = ZipMapIO.Normalize(docLoc);
        var docDirName = ZipMapIO.ParentDir(docPath);

        // ── 2. 加载 Document.xml 与页树 ───────────────────────────
        if (!map.TryGetValue(docPath, out var docBytes))
            throw new InvalidOperationException($"错误的OFD结构，缺少文档描述文件 {docPath}");
        var document = new Document(XDocument.Parse(System.Text.Encoding.UTF8.GetString(docBytes)).Root!);
        var pages = document.Pages ?? throw new InvalidOperationException("文档缺少页面树（Pages），无法定位注释目标页");
        var pageList = CollectPages(pages);
        if (pageNumber > pageList.Count)
            throw new ArgumentException($"页码超出范围：文档共 {pageList.Count} 页", nameof(pageNumber));
        var page = pageList[pageNumber - 1];
        var pageId = page.ID ?? throw new InvalidOperationException("目标页面缺少 ID 属性，无法添加注释");
        var pageIdStr = pageId.Value.ToString();

        // ── 3. 初始化最大对象 ID ──────────────────────────────────
        long maxUnitId =
            docBody?.DocInfo?.MaxUnitID?.Value
            ?? document.CommonData?.MaxUnitID?.Value
            ?? 0;

        var commonData = document.ObtainCommonData();

        // ── 4. 初始化注释入口文件（对齐 Java AnnotationRender.init）──
        string annotsDir;
        string annPath;
        Annotations annModel;
        var annLocText = document.Annotations?.Value;
        var resolvedAnn = annLocText is null ? null : ZipMapIO.TryResolve(map, annLocText!, "");
        if (resolvedAnn != null)
        {
            annPath = resolvedAnn;
            annModel = new Annotations(ZipMapIO.LoadXml(map, annPath).Root!);
            // 注释列表文件所在目录即为注释容器目录（兼容不规范路径）
            annotsDir = ZipMapIO.ParentDir(annPath);
        }
        else
        {
            annotsDir = $"{docDirName}/Annots";
            annPath = $"{annotsDir}/Annotations.xml";
            annModel = new Annotations(new XElement(OFDElement.OfdNs + "Annotations"));
            // 绝对路径注册：相对形式（Doc_0/Annots/Annotations.xml）会被规范阅读器以
            // Document.xml 所在目录（Doc_0）为基准解析成 Doc_0/Doc_0/... 而失败。
            document.Annotations = new STLoc("/" + annPath);
        }

        // 资源提供器（字体 → PublicRes.xml；图片 → DocumentRes.xml；均按需懒创建）
        var provider = new MapResourceProvider(map, docDirName, () => ++maxUnitId, commonData);

        foreach (var annotation in annotations)
        {
            if (annotation is null) continue;

            // ── 5. 定位该页的分页注释文件（存在则追加，否则新建）──────
            AnnPage? record = annModel.GetByPageId(pageIdStr);
            PageAnnot pageAnnot;
            string targetPath;
            var existingLoc = record?.FileLoc?.Value is string fl ? ZipMapIO.TryResolve(map, fl, "") : null;
            if (existingLoc != null && LooksLikePageAnnot(map[existingLoc]))
            {
                // 已有分页注释文件：追加到同一文件
                targetPath = existingLoc;
                pageAnnot = new PageAnnot(ZipMapIO.LoadXml(map, existingLoc).Root!);
            }
            else
            {
                // 不存在或损坏：创建新文件 Annot_M.xml 并更新条目位置
                var nextIndex = NextAnnotIndex(map, annotsDir, pageNumber);
                targetPath = $"{annotsDir}/Page_{pageNumber}/Annot_{nextIndex}.xml";
                if (record == null)
                {
                    record = new AnnPage { PageID = STRefID.Parse(pageIdStr) };
                    annModel.AddPage(record);
                }
                // 绝对路径：AnnPage.FileLoc 以 Annotations.xml 所在目录为基准解析，绝对形式消除歧义
                record.FileLoc = new STLoc("/" + targetPath);
                pageAnnot = new PageAnnot();
            }

            // ── 6. 构建注释对象并绘制外观 ─────────────────────────────
            var annot = annotation.Build(++maxUnitId);
            var appearance = annot.Appearance
                ?? throw new InvalidOperationException("注释对象缺少外观（Appearance）");
            var box = appearance.Boundary!.Clone();
            box.TopLeftX = 0d;
            box.TopLeftY = 0d;
            var ctx = new DrawContext(appearance, box, () => ++maxUnitId, provider);
            annotation.Drawer.Draw(ctx);
            provider.Flush();

            pageAnnot.AddAnnot(annot);
            map[targetPath] = ZipMapIO.ToUtf8Bytes(pageAnnot.Element);
        }

        // ── 7. 回写各 XML 文件与最大对象 ID ───────────────────────
        map[annPath] = ZipMapIO.ToUtf8Bytes(annModel.Element);
        commonData.MaxUnitID = new STID(maxUnitId + 1);
        map[docPath] = ZipMapIO.ToUtf8Bytes(document.Element);
        ofdRoot.ObtainDocBody().ObtainDocInfo().MaxUnitID = new STID(maxUnitId);
        map["OFD.xml"] = ZipMapIO.ToUtf8Bytes(ofdRoot.Element);
    }

    // ── 页树遍历 ─────────────────────────────────────────────────

    private static List<Page> CollectPages(Pages pages)
    {
        var res = new List<Page>();
        Collect(pages.Element, res, 0);
        return res;
    }

    private static void Collect(XElement parent, List<Page> sink, int depth)
    {
        if (depth > 32) return; // 防御异常深嵌套
        foreach (var e in parent.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "Page":
                    sink.Add(new Page(e));
                    break;
                case "Pages":
                    Collect(e, sink, depth + 1);
                    break;
            }
        }
    }

    private static int NextAnnotIndex(Dictionary<string, byte[]> map, string annotsDir, int pageNumber)
    {
        var prefix = $"{annotsDir}/Page_{pageNumber}/";
        int max = 0;
        foreach (var key in map.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var m = System.Text.RegularExpressions.Regex.Match(
                key.Substring(prefix.Length), @"^Annot_(\d+)\.xml$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var idx) && idx > max)
                max = idx;
        }
        return max + 1;
    }

    /// <summary>粗略判断字节是否为可解析的 XML（避免把二进制误当分页注释文件加载）。</summary>
    private static bool LooksLikePageAnnot(byte[] bytes)
    {
        try
        {
            return XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes)).Root?.Name.LocalName == "PageAnnot";
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }
    }

    // ── XML 序列化辅助 ───────────────────────────────────────────

    /// <summary>将 XElement 序列化为 UTF-8（无 BOM）且带 XML 声明的字节。</summary>
    public static byte[] ToUtf8Bytes(XElement root) => ZipMapIO.ToUtf8Bytes(root);
}
