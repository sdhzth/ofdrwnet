using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 包映射（Dictionary）读写辅助：zip 解包/打包、路径解析、XML 序列化。
/// 供 <see cref="OfdAnnotator"/>、<see cref="OfdAttacher"/> 等映射式编辑器共用。
/// </summary>
internal static class ZipMapIO
{
    // ── 路径辅助 ─────────────────────────────────────────────────

    public static string Normalize(string p) =>
        (p ?? "").Trim().TrimStart('/').Replace('\\', '/');

    public static string ParentDir(string path)
    {
        var n = Normalize(path);
        var idx = n.LastIndexOf('/');
        return idx <= 0 ? "Doc_0" : n[..idx];
    }

    /// <summary>在包映射中解析资源位置（支持绝对与相对文档目录两种写法）。</summary>
    public static string? TryResolve(Dictionary<string, byte[]> map, string loc, string baseDir)
    {
        var n = Normalize(loc);
        if (n.Length == 0) return null;
        if (map.TryGetValue(n, out _)) return n;
        if (baseDir.Length > 0)
        {
            var alt = $"{baseDir}/{n}";
            if (map.TryGetValue(alt, out _)) return alt;
        }
        return null;
    }

    // ── XML 序列化 ───────────────────────────────────────────────

    public static XDocument LoadXml(Dictionary<string, byte[]> map, string path) =>
        XDocument.Parse(Encoding.UTF8.GetString(map[path]));

    /// <summary>
    /// 将 XElement 序列化为 UTF-8（无 BOM）且带 XML 声明的字节。
    /// <para>
    /// GB/T 33190-2016 §7.1 / GB/T 48666-2026 §6.2：包内各 XML 文档的根节点应声明
    /// <c>defaults</c> 前缀形式的 OFD 命名空间。此处是映射式编辑器产出 XML 的<b>唯一出口</b>，
    /// 故在此统一收口，保证编辑器写出的 OFD 与容器层产物一致。
    /// </para>
    /// <para>
    /// 注入在<b>已序列化的文本</b>上完成——LINQ-to-XML 追加命名空间声明会把元素前缀
    /// 改写为 <c>defaults</c> 前缀形式（违反「标识符宜为 ofd」），文本层注入是唯一可行路径。
    /// </para>
    /// </summary>
    public static byte[] ToUtf8Bytes(XElement root)
    {
        var doc = new XDocument(root);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            OmitXmlDeclaration = false,
            Indent = false,
            NewLineHandling = NewLineHandling.None
        };
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings))
            doc.Save(w);
        return NetOfd.Core.OFDElement.NormalizeOfdXmlBytes(ms.ToArray(), NetOfd.Core.OFDElement.OfdNs.NamespaceName);
    }

    // ── zip 读写 ─────────────────────────────────────────────────

    public static Dictionary<string, byte[]> ReadZip(string path)
    {
        try
        {
            var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using var za = ZipFile.OpenRead(path);
            foreach (var e in za.Entries)
            {
                using var s = e.Open();
                map[e.FullName] = ReadAll(s);
            }
            return map;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or FileNotFoundException)
        {
            throw new InvalidOperationException($"无法读取 OFD 文件「{path}」：不是有效的 ZIP/OFD 数据", ex);
        }
    }

    public static Dictionary<string, byte[]> ReadZip(byte[] bytes)
    {
        try
        {
            var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using var ms = new MemoryStream(bytes ?? throw new ArgumentException("输入字节为空（null）"));
            if (bytes.Length == 0) throw new InvalidDataException("输入为空字节数组");
            using var za = new ZipArchive(ms, ZipArchiveMode.Read);
            foreach (var e in za.Entries)
            {
                using var s = e.Open();
                map[e.FullName] = ReadAll(s);
            }
            return map;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            throw new InvalidOperationException("无法读取 OFD 数据：不是有效的 ZIP/OFD 字节流", ex);
        }
    }

    public static void WriteZip(string path, Dictionary<string, byte[]> map)
    {
        if (File.Exists(path)) File.Delete(path);
        using var za = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var kv in map)
            WriteEntry(za, kv.Key, kv.Value);
    }

    public static byte[] WriteZipToBytes(Dictionary<string, byte[]> map)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var kv in map)
                WriteEntry(za, kv.Key, kv.Value);
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive za, string name, byte[] content)
    {
        var e = za.CreateEntry(name);
        using var s = e.Open();
        s.Write(content, 0, content.Length);
    }

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}
