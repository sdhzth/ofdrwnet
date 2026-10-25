using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Font;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 已有 OFD 文档的「内容替换器」：按文本映射表替换页面内 TextObject 中的文字，
/// 并按原字体重新计算字间距（DeltaX）。
/// <para>对应 Java: <c>org.ofdrw.layout.DocContentReplace</c>。</para>
/// </summary>
/// <remarks>
/// <b>替换单位</b>：OFD 文本以 TextObject 内的 TextCode 为最小单元，故本实现按
/// 「TextCode 的整段文本与映射键<b>全等</b>」命中后替换（与 Java 一致）。
/// 若原文跨行/跨 TextCode，请以行为单位逐条替换。
/// <para><b>为何要删 CGTransform</b>：CGTransform 的检索码指向字体文件中内嵌的字形，
/// 替换后的新字形在原子集里通常没有对应码位；删除后阅读器回退到 TextCode 的文本内容渲染，
/// 保证替换内容可见。</para>
/// </remarks>
public static class DocContentReplace
{
    // ── 对外 API（字节数组）───────────────────────────────────

    /// <summary>替换整个文档中的文本，返回新的 OFD 字节。</summary>
    /// <param name="inputOfd">原 OFD 字节</param>
    /// <param name="textMap">替换映射：键=原文本，值=新文本</param>
    public static byte[] ReplaceText(byte[] inputOfd, IDictionary<string, string> textMap)
        => ReplaceText(inputOfd, null, textMap, null);

    /// <summary>替换指定页（页码从 1 开始）中的文本，返回新的 OFD 字节。</summary>
    public static byte[] ReplaceText(byte[] inputOfd, int pageNumber, IDictionary<string, string> textMap)
        => ReplaceText(inputOfd, (int?)pageNumber, textMap, null);

    /// <summary>替换整个文档中的文本（可带扩展选项），返回新的 OFD 字节。</summary>
    public static byte[] ReplaceText(byte[] inputOfd, IDictionary<string, string> textMap,
                                     ReplaceTextOptions? options)
        => ReplaceText(inputOfd, null, textMap, options);

    /// <summary>替换指定页（页码从 1 开始）中的文本（可带扩展选项），返回新的 OFD 字节。</summary>
    public static byte[] ReplaceText(byte[] inputOfd, int pageNumber,
                                     IDictionary<string, string> textMap, ReplaceTextOptions? options)
        => ReplaceText(inputOfd, (int?)pageNumber, textMap, options);

    private static byte[] ReplaceText(byte[] inputOfd, int? pageNumber, IDictionary<string, string> textMap,
                                      ReplaceTextOptions? options)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        ReplaceTextCore(map, pageNumber, textMap, options);
        return ZipMapIO.WriteZipToBytes(map);
    }

    // ── 对外 API（文件路径）───────────────────────────────────

    /// <summary>替换整个文档中的文本并另存，返回被替换的 TextCode 数量。</summary>
    public static int ReplaceText(string inputPath, string outputPath, IDictionary<string, string> textMap)
        => ReplaceText(inputPath, outputPath, null, textMap, null);

    /// <summary>替换指定页（页码从 1 开始）中的文本并另存，返回被替换的 TextCode 数量。</summary>
    public static int ReplaceText(string inputPath, string outputPath, int pageNumber,
                                  IDictionary<string, string> textMap)
        => ReplaceText(inputPath, outputPath, (int?)pageNumber, textMap, null);

    /// <summary>替换整个文档中的文本（可带扩展选项）并另存，返回被替换的 TextCode 数量。</summary>
    public static int ReplaceText(string inputPath, string outputPath,
                                  IDictionary<string, string> textMap, ReplaceTextOptions? options)
        => ReplaceText(inputPath, outputPath, null, textMap, options);

    /// <summary>替换指定页（页码从 1 开始）中的文本（可带扩展选项）并另存，返回被替换的 TextCode 数量。</summary>
    public static int ReplaceText(string inputPath, string outputPath, int pageNumber,
                                  IDictionary<string, string> textMap, ReplaceTextOptions? options)
        => ReplaceText(inputPath, outputPath, (int?)pageNumber, textMap, options);

    private static int ReplaceText(string inputPath, string outputPath, int? pageNumber,
                                   IDictionary<string, string> textMap, ReplaceTextOptions? options)
    {
        if (string.IsNullOrWhiteSpace(inputPath)) throw new ArgumentException("输入路径不能为空", nameof(inputPath));
        if (string.IsNullOrWhiteSpace(outputPath)) throw new ArgumentException("输出路径不能为空", nameof(outputPath));

        var map = ZipMapIO.ReadZip(inputPath);
        var hits = ReplaceTextCore(map, pageNumber, textMap, options);
        ZipMapIO.WriteZip(outputPath, map);
        return hits;
    }

    // ── 核心实现 ──────────────────────────────────────────────

    /// <summary>在 map（OFD 包内文件表）上就地执行替换，返回命中数量。</summary>
    internal static int ReplaceTextCore(Dictionary<string, byte[]> map, int? pageNumber,
                                        IDictionary<string, string> textMap,
                                        ReplaceTextOptions? options = null)
    {
        if (textMap is null) throw new ArgumentNullException(nameof(textMap));
        if (textMap.Count == 0) return 0;
        if (pageNumber is <= 0) throw new ArgumentException("页码（pageNumber）必须从 1 开始", "pageNumber");

        var ctx = OfdEditor.LoadContext(map);
        var pageList = OfdEditor.CollectPages(ctx.Document);
        if (pageNumber.HasValue && pageNumber.Value > pageList.Count)
            throw new ArgumentException($"页码超出范围：文档共 {pageList.Count} 页", "pageNumber");

        var fonts = CollectFonts(map, ctx);
        var fontCache = new Dictionary<string, OFDFont>(StringComparer.OrdinalIgnoreCase);
        try
        {
            int total = 0;
            for (int i = 0; i < pageList.Count; i++)
            {
                if (pageNumber.HasValue && i + 1 != pageNumber.Value) continue;

                var baseLoc = pageList[i].BaseLoc?.Value;
                if (string.IsNullOrEmpty(baseLoc)) continue;
                var contentPath = ZipMapIO.TryResolve(map, baseLoc!, ctx.DocDirName);
                if (contentPath is null) continue;

                var content = ZipMapIO.LoadXml(map, contentPath);
                var hits = ReplaceInTree(content.Root!, textMap, fonts, fontCache, options);
                if (hits > 0)
                {
                    map[contentPath] = ZipMapIO.ToUtf8Bytes(content.Root!);
                    total += hits;
                }
            }
            return total;
        }
        finally
        {
            foreach (var f in fontCache.Values) f.Dispose();
        }
    }

    /// <summary>递归遍历页面内容树，处理其中所有 TextObject。</summary>
    private static int ReplaceInTree(XElement root, IDictionary<string, string> textMap,
                                     Dictionary<string, (string Name, string Family)> fonts,
                                     Dictionary<string, OFDFont> fontCache,
                                     ReplaceTextOptions? options)
    {
        int hits = 0;
        foreach (var e in root.DescendantsAndSelf())
        {
            if (e.Name.LocalName != "TextObject") continue;
            // 过滤器：返回 false 的 TextObject 整体跳过（对齐 Java ExtractorFilter 的筛选语义）
            if (options?.TextObjectFilter is { } filter && !filter(e)) continue;
            hits += ReplaceInTextObject(e, textMap, fonts, fontCache, options);
        }
        return hits;
    }

    /// <summary>处理单个 TextObject：逐 TextCode 命中替换 → 删 CGTransform → 重算 DeltaX。</summary>
    private static int ReplaceInTextObject(XElement textObj, IDictionary<string, string> textMap,
                                           Dictionary<string, (string Name, string Family)> fonts,
                                           Dictionary<string, OFDFont> fontCache,
                                           ReplaceTextOptions? options)
    {
        int hits = 0;

        // 字号（mm）：OFD Size 单位与 Java 布局字号一致，DeltaX 同单位
        var sizeAttr = textObj.Attribute("Size")?.Value;
        double size = sizeAttr is not null
                      && double.TryParse(sizeAttr, NumberStyles.Float, CultureInfo.InvariantCulture, out var s)
            ? s
            : 9.0;

        foreach (var code in textObj.Elements())
        {
            if (code.Name.LocalName != "TextCode") continue;

            var oldText = code.Value;
            if (string.IsNullOrWhiteSpace(oldText)) continue;

            // Java 语义：全等匹配（trim 后非空才参与）
            if (!textMap.TryGetValue(oldText, out var newText)) continue;
            if (newText is null || string.Equals(oldText, newText, StringComparison.Ordinal)) continue;

            // 1) 删除该 TextObject 下的全部 CGTransform（替换后字形码位无效）
            foreach (var child in new List<XElement>(textObj.Elements()))
                if (child.Name.LocalName == "CGTransform")
                    child.Remove();

            // 2) 扩展点：允许调用方为替换后的文本重建 CGTransform（对齐 Java ReplaceTextHandler#handleCgTransform）
            var newCg = options?.CgTransformFactory?.Invoke(textObj, newText);
            if (newCg is not null) textObj.Add(newCg);

            // 3) 按「扩展点指定字体 → 原字体」的优先序取度量字体，重算 DeltaX 并写入新文本
            var font = options?.FontResolver?.Invoke(textObj, newText)
                       ?? ResolveFont(textObj, fonts, fontCache);
            code.Value = newText;
            var deltaX = ComputeDeltaX(newText, size, font);
            var dxAttr = code.Attribute("DeltaX");
            if (deltaX is null)
            {
                // 单字符/空串：不出现 DeltaX 表示绘制点不做偏移（对齐 Java 空数组语义）
                dxAttr?.Remove();
            }
            else
            {
                if (dxAttr is null) code.Add(new XAttribute("DeltaX", deltaX));
                else dxAttr.Value = deltaX;
            }

            hits++;
        }

        return hits;
    }

    /// <summary>
    /// 计算 TextCode 的 DeltaX：相邻文字在 X 方向的推进量序列。
    /// <para>对齐 Java <c>Span#getDeltaX()</c>：长度为 <c>len-1</c>，<c>DeltaX[i] = 字符 i 的字宽</c>；
    /// 末字符后无后续字，故不产出；单字符返回空（不写属性）。</para>
    /// </summary>
    private static string? ComputeDeltaX(string text, double sizeMm, OFDFont? font)
    {
        if (text.Length <= 1) return null;

        var sb = new StringBuilder();
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (i > 0) sb.Append(' ');
            var scale = font?.GetCharWidthScale(text[i]) ?? 1.0d;
            sb.Append(Fmt(scale * sizeMm));
        }
        return sb.ToString();
    }

    /// <summary>数字格式化：定点小数，避免科学计数法（OFD 属性为十进制文本）。</summary>
    private static string Fmt(double v)
    {
        var d = Math.Abs(v) < 1e-9 ? 0d : v;
        return d.ToString("0.####", CultureInfo.InvariantCulture);
    }

    /// <summary>按 TextObject 的 Font 引用取得度量字体；失败时回退 null（按全宽 1.0 估算）。</summary>
    private static OFDFont? ResolveFont(XElement textObj,
                                        Dictionary<string, (string Name, string Family)> fonts,
                                        Dictionary<string, OFDFont> fontCache)
    {
        var fontId = textObj.Attribute("Font")?.Value;
        if (string.IsNullOrWhiteSpace(fontId)) return null;
        if (!fonts.TryGetValue(fontId!, out var desc)) return null;

        var key = $"{desc.Name}|{desc.Family}";
        if (fontCache.TryGetValue(key, out var cached)) return cached;

        var font = new OFDFont(desc.Name, desc.Family);
        fontCache[key] = font;
        return font;
    }

    // ── 字体表收集 ────────────────────────────────────────────

    /// <summary>
    /// 收集文档声明的字体表：<c>Font ID → (FontName, FamilyName)</c>。
    /// <para>字体可声明于 PublicRes.xml / DocumentRes.xml，也可内联在 Document.xml 的 CommonData 下；
    /// 故三处都扫，后写覆盖先写。</para>
    /// </summary>
    private static Dictionary<string, (string Name, string Family)> CollectFonts(
        Dictionary<string, byte[]> map, OfdEditor.DocContext ctx)
    {
        var res = new Dictionary<string, (string, string)>(StringComparer.Ordinal);

        var paths = new List<string>();
        foreach (var loc in ctx.Document.CommonData?.PublicResList ?? new List<STLoc>())
            AddResolved(map, paths, loc?.Value, ctx.DocDirName);
        foreach (var loc in ctx.Document.CommonData?.DocumentResList ?? new List<STLoc>())
            AddResolved(map, paths, loc?.Value, ctx.DocDirName);

        // 兜底：约定文件名（部分工具不写 CommonData 资源列表）
        AddResolved(map, paths, $"{ctx.DocDirName}/PublicRes.xml", ctx.DocDirName);
        AddResolved(map, paths, $"{ctx.DocDirName}/DocumentRes.xml", ctx.DocDirName);

        // 页级资源：个别工具把字体注册在 PageRes 中（Java ResourceManage 同样会解析页级资源）
        var prefix = $"{ctx.DocDirName}/";
        foreach (var key in map.Keys)
        {
            var n = ZipMapIO.Normalize(key);
            if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var fname = n[prefix.Length..];
            if (fname.Contains('/')) continue; // 仅文档根目录下的 PageRes*.xml
            if (fname.StartsWith("PageRes", StringComparison.OrdinalIgnoreCase)
                && fname.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                paths.Add(key);
        }

        // Document.xml 自身（CommonData 内联 Fonts 的情况）
        paths.Add(ctx.DocPath);

        foreach (var path in paths)
        {
            if (!map.ContainsKey(path)) continue;
            XElement root;
            try { root = ZipMapIO.LoadXml(map, path).Root!; }
            catch { continue; }

            foreach (var e in root.DescendantsAndSelf())
            {
                if (e.Name.LocalName != "Font") continue;
                var id = e.Attribute("ID")?.Value;
                if (string.IsNullOrWhiteSpace(id)) continue;
                var name = e.Attribute("FontName")?.Value;
                var family = e.Attribute("FamilyName")?.Value;
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(family)) continue;
                res[id!] = (name ?? family!, family ?? name!);
            }
        }

        return res;
    }

    private static void AddResolved(Dictionary<string, byte[]> map, List<string> sink, string? loc, string baseDir)
    {
        if (string.IsNullOrWhiteSpace(loc)) return;
        var resolved = ZipMapIO.TryResolve(map, loc!, baseDir);
        if (resolved is not null && !sink.Contains(resolved)) sink.Add(resolved);
    }
}

/// <summary>
/// 文档内容替换的扩展选项。
/// <para>对应 Java <c>DocContentReplace.ReplaceTextHandler</c> 的两个扩展点
/// （<c>handleNewFont</c> / <c>handleCgTransform</c>），另加一个 TextObject 过滤器
/// （对应 Java 通过 <c>ExtractorFilter</c> 限定参与抽取范围的能力）。</para>
/// </summary>
public sealed class ReplaceTextOptions
{
    /// <summary>
    /// TextObject 过滤器：返回 <c>false</c> 表示该文本对象整体不参与替换（含其中的全部 TextCode）。
    /// <para>可用于只替换特定区域/特定字号的文字。</para>
    /// </summary>
    public Func<XElement, bool>? TextObjectFilter { get; set; }

    /// <summary>
    /// 自定义新文本使用的度量字体，用于重算 DeltaX。
    /// <para>参数为（所属 TextObject 元素, 替换后的新文本）；返回 <c>null</c> 时回退到原文引用的字体。
    /// 常见用法：替换文字改用另一种字体（如拉丁字体）时，按新字体的字宽重算间距。</para>
    /// </summary>
    public Func<XElement, string, OFDFont?>? FontResolver { get; set; }

    /// <summary>
    /// 为替换后的文本重建 CGTransform 元素；返回 <c>null</c> 表示不生成（默认行为：删除原有 CGTransform）。
    /// <para>参数为（所属 TextObject 元素, 替换后的新文本）。返回的 XElement 应使用与文档一致的
    /// 命名空间（可从 <c>TextObject.Name.Namespace</c> 取得）。</para>
    /// </summary>
    public Func<XElement, string, XElement?>? CgTransformFactory { get; set; }
}
