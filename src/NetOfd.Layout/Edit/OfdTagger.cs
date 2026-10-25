using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicType;
using NetOfd.Core.CustomTags;

namespace NetOfd.Layout.Edit;

/// <summary>
/// OFD 自定义标引编辑器：向已有 OFD 文档添加或读取自定义标引。
/// <para>
/// 采用与 <see cref="OfdAnnotator"/> 一致的映射（Dictionary）方式读写包内文件：
/// 标引文件写入 Doc_0/Tags/Custom_N.xml，索引写入 Doc_0/CustomTags.xml，
/// 并在 Document.xml 中登记标引列表入口。
/// </para>
/// 对应 Java: org.ofdrw.core.customTags（GB/T 33190-2016 第 16 章）
/// </summary>
public static class OfdTagger
{
    /// <summary>向已有 OFD 字节添加自定义标引，返回新的 OFD 字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="typeId">自定义标引内容节点使用的类型标识（必填）</param>
    /// <param name="tagFileContent">标引文件内容（XML 字节）</param>
    /// <param name="nameSpace">命名空间（可选）</param>
    /// <param name="schemaLoc">Schema 文件位置（可选）</param>
    public static byte[] AddCustomTag(byte[] inputOfd, string typeId, byte[] tagFileContent,
        string? nameSpace = null, string? schemaLoc = null)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        AddCustomTagCore(map, typeId, tagFileContent, nameSpace, schemaLoc);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>向已有 OFD 文件添加自定义标引，输出新的 OFD 文件。</summary>
    public static void AddCustomTag(string inputPath, string outputPath, string typeId, string tagFilePath,
        string? nameSpace = null, string? schemaLoc = null)
    {
        if (string.IsNullOrWhiteSpace(tagFilePath) || !File.Exists(tagFilePath))
            throw new ArgumentException("标引文件不存在", nameof(tagFilePath));
        var map = ZipMapIO.ReadZip(inputPath);
        AddCustomTagCore(map, typeId, File.ReadAllBytes(tagFilePath), nameSpace, schemaLoc);
        ZipMapIO.WriteZip(outputPath, map);
    }

    /// <summary>读取已有 OFD 中的全部自定义标引及其文件内容。</summary>
    public static IReadOnlyList<(CustomTag Tag, byte[] Content)> GetCustomTags(byte[] inputOfd)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        return GetCustomTagsCore(map);
    }

    // ── 核心实现 ─────────────────────────────────────────────────

    internal static void AddCustomTagCore(Dictionary<string, byte[]> map, string typeId, byte[] tagFileContent,
        string? nameSpace, string? schemaLoc)
    {
        if (string.IsNullOrWhiteSpace(typeId))
            throw new ArgumentException("类型标识（TypeID）不能为空", nameof(typeId));
        if (tagFileContent is null || tagFileContent.Length == 0)
            throw new ArgumentException("标引文件内容不能为空", nameof(tagFileContent));

        // ── 1. 解析文档上下文 ─────────────────────────────────────
        var (ofdRoot, docPath, docDirName, document) = LoadContext(map);

        // ── 2. 获取或创建标引索引文件 ─────────────────────────────
        var (tagsPath, tagsModel) = ObtainCustomTags(map, docDirName, document);

        // ── 3. 写入标引文件并登记入口 ─────────────────────────────
        var nextIndex = NextTagIndex(map, docDirName);
        var fileLoc = $"{docDirName}/Tags/Custom_{nextIndex}.xml";
        map[fileLoc] = tagFileContent;

        tagsModel.AddCustomTag(new CustomTag
        {
            TypeID = typeId,
            NameSpace = nameSpace,
            SchemaLoc = schemaLoc is null ? null : new STLoc(schemaLoc),
            // 绝对路径：FileLoc 以 CustomTags.xml 所在目录为基准解析，绝对形式消除歧义
            FileLoc = new STLoc("/" + fileLoc)
        });

        // ── 4. 回写 ───────────────────────────────────────────────
        map[tagsPath] = ZipMapIO.ToUtf8Bytes(tagsModel.Element);
        map[docPath] = ZipMapIO.ToUtf8Bytes(document.Element);
    }

    internal static IReadOnlyList<(CustomTag Tag, byte[] Content)> GetCustomTagsCore(Dictionary<string, byte[]> map)
    {
        var (_, _, docDirName, document) = LoadContext(map);
        var result = new List<(CustomTag, byte[])>();

        var tagsLocText = document.CustomTags?.Value;
        var tagsPath = tagsLocText is null ? null : ZipMapIO.TryResolve(map, tagsLocText!, "");
        if (tagsPath is null) return result;

        var tagsModel = new CustomTags(ZipMapIO.LoadXml(map, tagsPath).Root!);
        foreach (var tag in tagsModel.GetCustomTags())
        {
            var loc = tag.FileLoc.Value;
            var found = ZipMapIO.TryResolve(map, loc, docDirName);
            if (found is null) continue;
            result.Add((tag, map[found]));
        }
        return result;
    }

    // ── 内部辅助 ─────────────────────────────────────────────────

    private static (OFDRoot OfdRoot, string DocPath, string DocDirName, Document Document) LoadContext(
        Dictionary<string, byte[]> map)
    {
        if (!map.TryGetValue("OFD.xml", out var ofdBytes))
            throw new InvalidOperationException("错误的OFD结构，缺少 OFD.xml 文件");
        var ofdRoot = new OFDRoot(XDocument.Parse(System.Text.Encoding.UTF8.GetString(ofdBytes)).Root!);
        var docLoc = ofdRoot.GetFirstDocBody()?.DocRoot?.Value ?? "Doc_0/Document.xml";
        var docPath = ZipMapIO.Normalize(docLoc);
        if (!map.TryGetValue(docPath, out var docBytes))
            throw new InvalidOperationException($"错误的OFD结构，缺少文档描述文件 {docPath}");
        var document = new Document(XDocument.Parse(System.Text.Encoding.UTF8.GetString(docBytes)).Root!);
        return (ofdRoot, docPath, ZipMapIO.ParentDir(docPath), document);
    }

    /// <summary>获取标引索引文件，不存在则创建并在 Document.xml 登记。</summary>
    private static (string Path, CustomTags Model) ObtainCustomTags(
        Dictionary<string, byte[]> map, string docDirName, Document document)
    {
        var tagsLocText = document.CustomTags?.Value;
        var found = tagsLocText is null ? null : ZipMapIO.TryResolve(map, tagsLocText!, "");
        if (found != null)
            return (found, new CustomTags(ZipMapIO.LoadXml(map, found).Root!));

        var path = $"{docDirName}/CustomTags.xml";
        var model = new CustomTags(new XElement(OFDElement.OfdNs + "CustomTags"));
        // 绝对路径注册：Document.xml 中的 ST_Loc 若写成相对形式（Doc_0/CustomTags.xml），
        // 规范阅读器以 Document.xml 所在目录（Doc_0）为基准会解析为 Doc_0/Doc_0/... 而失败。
        document.CustomTags = new STLoc("/" + path);
        return (path, model);
    }

    private static int NextTagIndex(Dictionary<string, byte[]> map, string docDirName)
    {
        var prefix = $"{docDirName}/Tags/";
        int max = 0;
        foreach (var key in map.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var m = System.Text.RegularExpressions.Regex.Match(
                key.Substring(prefix.Length), @"^Custom_(\d+)\.xml$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var idx) && idx > max)
                max = idx;
        }
        return max + 1;
    }
}
