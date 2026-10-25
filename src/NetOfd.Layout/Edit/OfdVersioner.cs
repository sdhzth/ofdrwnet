using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicType;
using NetOfd.Core.Versions;

namespace NetOfd.Layout.Edit;

/// <summary>
/// OFD 版本编辑器：为已有 OFD 文档创建版本快照、读取版本信息、提取历史版本。
/// <para>
/// 采用与 <see cref="OfdAnnotator"/> 一致的映射（Dictionary）方式读写包内文件：
/// 快照将当前文档目录完整复制到 Doc_N/V_M/，版本描述文件（DocVersion.xml）
/// 记录该版本的文件列表与入口，版本入口登记于 OFD.xml 的 DocBody/Versions。
/// </para>
/// 对应 Java: org.ofdrw.core.versions（GB/T 33190-2016 第 19 章）
/// </summary>
public static class OfdVersioner
{
    /// <summary>向已有 OFD 字节追加当前文档状态的版本快照，返回新的 OFD 字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="versionName">版本名称（如“初稿”）</param>
    /// <param name="index">输出版本号</param>
    public static byte[] AddVersion(byte[] inputOfd, string versionName, out int index)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        index = AddVersionCore(map, versionName);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>获取已有 OFD 的全部版本描述入口。</summary>
    public static IReadOnlyList<NetOfd.Core.Versions.Version> GetVersions(byte[] inputOfd)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        return GetVersionsCore(map);
    }

    /// <summary>加载指定版本的版本描述文件（DocVersion）。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="versionId">版本标识</param>
    public static DocVersion LoadDocVersion(byte[] inputOfd, string versionId)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        var (_, _, _, docVersion) = ResolveEntry(map, versionId);
        return docVersion;
    }

    /// <summary>提取指定版本为独立 OFD 文档字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="versionId">版本标识</param>
    public static byte[] ExtractVersion(byte[] inputOfd, string versionId)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        var extracted = ExtractVersionCore(map, versionId);
        return ZipMapIO.WriteZipToBytes(extracted);
    }

    // ── 核心实现 ─────────────────────────────────────────────────

    internal static int AddVersionCore(Dictionary<string, byte[]> map, string versionName)
    {
        if (string.IsNullOrWhiteSpace(versionName))
            throw new ArgumentException("版本名称（versionName）不能为空", nameof(versionName));

        // ── 1. 解析文档上下文 ─────────────────────────────────────
        var (ofdRoot, _, docDirName) = LoadContext(map);
        var docBody = ofdRoot.ObtainDocBody();

        // ── 2. 计算版本号并复制当前文档目录 ───────────────────────
        var index = NextVersionIndex(docBody);
        var snapPrefix = $"{docDirName}/V_{index}/";
        var fileList = new FileList();
        var fileId = 0;
        foreach (var kv in map.ToArray())
        {
            if (!kv.Key.StartsWith(docDirName + "/", StringComparison.Ordinal)) continue;
            // 跳过既有快照与版本描述目录，仅快照活动文档
            var rel = kv.Key.Substring(docDirName.Length + 1);
            if (rel.StartsWith("V_", StringComparison.Ordinal)) continue;
            if (rel.StartsWith("Versions/", StringComparison.Ordinal)) continue;

            var newKey = snapPrefix + rel;
            map[newKey] = kv.Value;
            // 文件标识必须是 XML NCName（首字符为字母/下划线），纯数字 ID（如 "1"）会被严格阅读器判为非法；
            // 文件路径用包根绝对写法（前导 "/"），避免相对路径按描述文件所在目录二次拼接。
            fileList.AddFile($"file{++fileId}", new STLoc("/" + newKey));
        }
        if (fileId == 0)
            throw new InvalidOperationException($"文档目录 {docDirName} 为空，无法创建版本快照");

        // ── 3. 写入版本描述文件（DocVersion.xml）──────────────────
        var versionId = $"v{index}";
        var verDir = $"{docDirName}/Versions/V_{index}";
        var docVersion = new DocVersion(versionId, fileList)
        {
            Name = versionName,
            CreationDate = DateOnly.FromDateTime(DateTime.Now),
            // DocRoot 同样是包内路径，须用包根绝对写法（前导 "/"），
            // 否则严格阅读器会按描述文件所在目录（Doc_0/Versions/V_N/）二次拼接导致找不到文件。
            DocRoot = new STLoc("/" + snapPrefix + "Document.xml")
        };
        var verFilePath = $"{verDir}/DocVersion.xml";
        map[verFilePath] = ZipMapIO.ToUtf8Bytes(docVersion.Element);

        // ── 4. 登记版本入口（新版本置为默认，其余取消）───────────
        var versionsModel = docBody.ObtainVersions();
        foreach (var old in versionsModel.GetVersions())
            old.Current = false;
        versionsModel.AddVersion(new NetOfd.Core.Versions.Version(versionId, index, new STLoc(verFilePath))
        {
            Current = true
        });
        map["OFD.xml"] = ZipMapIO.ToUtf8Bytes(ofdRoot.Element);
        return index;
    }

    internal static IReadOnlyList<NetOfd.Core.Versions.Version> GetVersionsCore(Dictionary<string, byte[]> map)
    {
        var (ofdRoot, _, _) = LoadContext(map);
        return ofdRoot.GetFirstDocBody()?.Versions?.GetVersions()
               ?? new List<NetOfd.Core.Versions.Version>();
    }

    internal static Dictionary<string, byte[]> ExtractVersionCore(Dictionary<string, byte[]> map, string versionId)
    {
        // ── 1. 定位版本入口与描述文件 ─────────────────────────────
        var (_, _, _, docVersion) = ResolveEntry(map, versionId);
        var (_, _, docDirName) = LoadContext(map);

        // ── 2. 按文件列表抽取该版本的文件（剥离快照目录段 V_N）────
        var output = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var fileList = docVersion.FileList
            ?? throw new InvalidOperationException($"版本 {versionId} 缺少文件列表（FileList）");
        foreach (var f in fileList.GetFiles())
        {
            var found = ZipMapIO.TryResolve(map, f.File.Value, "")
                ?? throw new InvalidOperationException($"版本文件不存在：{f.File.Value}");
            output[StripSnapshotSegment(found, docDirName)] = map[found];
        }
        if (output.Count == 0)
            throw new InvalidOperationException($"版本 {versionId} 文件列表为空");

        // ── 3. 重建 OFD.xml：沿用源文档元数据，指向该版本入口 ────
        var (ofdRoot, _, _) = LoadContext(map);
        var srcDocInfo = ofdRoot.GetFirstDocBody()?.DocInfo;
        var newBody = new DocBody();
        if (srcDocInfo != null)
            newBody.DocInfo = new DocInfo(srcDocInfo.Element);
        newBody.DocRoot = new STLoc(StripSnapshotSegment(
            ZipMapIO.Normalize(docVersion.DocRoot?.Value ?? "Doc_0/Document.xml"), docDirName));
        output["OFD.xml"] = ZipMapIO.ToUtf8Bytes(
            new XElement(OFDElement.OfdNs + "OFD", newBody.Element));
        return output;
    }

    /// <summary>剥离路径中的快照目录段：Doc_0/V_1/Pages/... → Doc_0/Pages/...；非快照路径原样返回。</summary>
    private static string StripSnapshotSegment(string path, string docDirName)
    {
        var prefix = $"{docDirName}/V_";
        if (!path.StartsWith(prefix, StringComparison.Ordinal)) return path;
        var slash = path.IndexOf('/', prefix.Length);
        return slash < 0 ? path : $"{docDirName}/{path[(slash + 1)..]}";
    }

    // ── 内部辅助 ─────────────────────────────────────────────────

    private static (string VerFilePath, string VerId, int Index, DocVersion DocVersion) ResolveEntry(
        Dictionary<string, byte[]> map, string versionId)
    {
        if (string.IsNullOrWhiteSpace(versionId))
            throw new ArgumentException("版本标识（versionId）不能为空", nameof(versionId));

        var (ofdRoot, _, _) = LoadContext(map);
        var entry = ofdRoot.GetFirstDocBody()?.Versions?.GetVersions()
            .FirstOrDefault(v => string.Equals(v.ID, versionId, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"未找到版本：{versionId}");

        var baseLoc = entry.BaseLoc?.Value
            ?? throw new InvalidOperationException($"版本 {versionId} 缺少 BaseLoc 属性");
        var verFilePath = ZipMapIO.TryResolve(map, baseLoc, "")
            ?? throw new InvalidOperationException($"版本描述文件不存在：{baseLoc}");
        var docVersion = new DocVersion(ZipMapIO.LoadXml(map, verFilePath).Root!);
        return (verFilePath, versionId, entry.Index ?? 0, docVersion);
    }

    private static (OFDRoot OfdRoot, string DocPath, string DocDirName) LoadContext(
        Dictionary<string, byte[]> map)
    {
        if (!map.TryGetValue("OFD.xml", out var ofdBytes))
            throw new InvalidOperationException("错误的OFD结构，缺少 OFD.xml 文件");
        var ofdRoot = new OFDRoot(XDocument.Parse(System.Text.Encoding.UTF8.GetString(ofdBytes)).Root!);
        var docLoc = ofdRoot.GetFirstDocBody()?.DocRoot?.Value ?? "Doc_0/Document.xml";
        var docPath = ZipMapIO.Normalize(docLoc);
        if (!map.TryGetValue(docPath, out _))
            throw new InvalidOperationException($"错误的OFD结构，缺少文档描述文件 {docPath}");
        return (ofdRoot, docPath, ZipMapIO.ParentDir(docPath));
    }

    private static int NextVersionIndex(DocBody docBody)
    {
        var max = 0;
        foreach (var v in docBody.Versions?.GetVersions() ?? new List<NetOfd.Core.Versions.Version>())
        {
            if (v.Index is { } i && i > max) max = i;
        }
        return max + 1;
    }
}
