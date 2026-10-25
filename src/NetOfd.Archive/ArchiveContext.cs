using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Archive;

/// <summary>
/// OFD 包访问上下文（zip+XML 层，不依赖容器 API）。
/// <para>以容器相对路径（"/" 开头）为键存放全部条目字节（大小写不敏感）；
/// 提供按 local-name 忽略命名空间的 XML 导航，以及原始字节取回（字体/图像魔数检测）。</para>
/// 对应 Java: org.ofdrw.reader.OFDReader + org.ofdrw.pkg.container.OFDDir（只读视图）
/// </summary>
public sealed class ArchiveContext : IDisposable
{
    private readonly Dictionary<string, byte[]> _entries;
    private bool _disposed;

    public ArchiveContext(string ofdPath)
    {
        _entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var zip = ZipFile.OpenRead(ofdPath);
        Load(zip.Entries);
    }

    public ArchiveContext(byte[] ofdBytes)
    {
        _entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var ms = new MemoryStream(ofdBytes);
        using var zip = new ZipArchive(ms);
        Load(zip.Entries);
    }

    private void Load(IEnumerable<ZipArchiveEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (entry.FullName.EndsWith("/")) continue;
            using var ms = new MemoryStream();
            entry.Open().CopyTo(ms);
            _entries["/" + entry.FullName.Replace('\\', '/')] = ms.ToArray();
        }
    }

    /// <summary>返回条目原始字节；不存在返回 null。</summary>
    public byte[]? GetBytes(string loc)
    {
        if (loc == null) return null;
        var key = Normalize(loc);
        return _entries.TryGetValue(key, out var b) ? b : null;
    }

    /// <summary>按 loc 解析 XML；不存在或解析失败返回 null。</summary>
    public XElement? Xml(string loc)
    {
        var b = GetBytes(loc);
        if (b == null) return null;
        try { return XElement.Parse(System.Text.Encoding.UTF8.GetString(b)); }
        catch (Exception) { return null; }
    }

    /// <summary>所有 .xml 条目的 (路径, 文本)。</summary>
    public IEnumerable<(string Path, string Text)> XmlTexts()
    {
        foreach (var kv in _entries)
        {
            if (!kv.Key.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            string text;
            try { text = System.Text.Encoding.UTF8.GetString(kv.Value); }
            catch (Exception) { continue; }
            yield return (kv.Key, text);
        }
    }

    /// <summary>OFD.xml 根元素。</summary>
    public XElement? OfdRoot => Xml("/OFD.xml");

    /// <summary>所有 DocBody 元素（跨文档）。</summary>
    public IEnumerable<XElement> DocBodies()
        => OfdRoot?.LocalElems("DocBody") ?? Enumerable.Empty<XElement>();

    /// <summary>每个文档的 (相对路径, Document.xml 根)。</summary>
    public IEnumerable<(string Path, XElement Root)> Documents()
    {
        foreach (var body in DocBodies())
        {
            var docRoot = body.LocalElem("DocRoot")?.Value.Trim();
            if (string.IsNullOrEmpty(docRoot)) continue;
            var root = Xml(docRoot);
            if (root != null) yield return (Normalize(docRoot), root);
        }
    }

    /// <summary>每个页面的 (相对路径, Content.xml 根)。</summary>
    public IEnumerable<(string Path, XElement Root)> Pages()
    {
        foreach (var (docPath, doc) in Documents())
        {
            var docDir = DirOf(docPath);
            foreach (var page in doc.LocalElems("Pages").SelectMany(p => p.LocalElems("Page")))
            {
                var baseLoc = page.AttrTrim("BaseLoc");
                if (string.IsNullOrEmpty(baseLoc)) continue;
                var pagePath = Combine(docDir, baseLoc);
                var root = Xml(pagePath);
                if (root != null) yield return (pagePath, root);
            }
        }
    }

    /// <summary>所有资源文件 (PublicRes / DocumentRes / PageRes*.xml) 的 (路径, 根)。</summary>
    public IEnumerable<(string Path, XElement Root)> ResourceFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<(string, XElement)>();
        void Add(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var norm = Normalize(path);
            if (!seen.Add(norm)) return;
            var root = Xml(norm);
            if (root != null) result.Add((norm, root));
        }

        foreach (var (docPath, doc) in Documents())
        {
            var docDir = DirOf(docPath);
            var cd = doc.LocalElem("CommonData");
            if (cd != null)
            {
                foreach (var name in new[] { "PublicRes", "DocumentRes" })
                {
                    var loc = cd.LocalElem(name)?.Value.Trim();
                    if (!string.IsNullOrEmpty(loc)) Add(Combine(docDir, loc));
                }
            }
            // PageRes 文件：doc 目录下所有以 PageRes 开头、.xml 结尾的文件
            foreach (var key in _entries.Keys)
            {
                if (!key.StartsWith(docDir + "/", StringComparison.OrdinalIgnoreCase)) continue;
                var fname = Path.GetFileName(key);
                if (fname.StartsWith("PageRes", StringComparison.OrdinalIgnoreCase)
                    && fname.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    Add(key);
            }
        }
        return result;
    }

    /// <summary>Signs/Signatures.xml 根（取首个 DocBody 声明的签名入口）。</summary>
    public XElement? Signatures()
    {
        foreach (var body in DocBodies())
        {
            var loc = body.LocalElem("Signatures")?.Value.Trim();
            if (!string.IsNullOrEmpty(loc))
            {
                var root = Xml(loc);
                if (root != null) return root;
            }
        }
        return null;
    }

    /// <summary>相对某文件解析子路径（如资源文件内的 MediaFile/FontFile loc）。</summary>
    public byte[]? ResolveBytes(string baseFilePath, string loc)
    {
        if (string.IsNullOrEmpty(loc)) return null;
        var full = loc.StartsWith("/")
            ? Normalize(loc)
            : Normalize(Combine(DirOf(baseFilePath), loc));
        return GetBytes(full);
    }

    /// <summary>
    /// 相对资源文件解析路径，遵循 Res 根节点的 BaseLoc（GB/T 33190-2016 7.9）。
    /// <para>优先按 &lt;资源文件目录&gt;/&lt;BaseLoc&gt;/loc 命中，未命中回退到 &lt;资源文件目录&gt;/loc。</para>
    /// </summary>
    public string? ResolveResPath(string resFilePath, XElement resRoot, string loc)
    {
        if (string.IsNullOrEmpty(loc)) return null;
        if (loc.StartsWith("/")) return Normalize(loc);
        var dir = DirOf(resFilePath);
        var baseLoc = resRoot.AttrTrim("BaseLoc");
        if (!string.IsNullOrEmpty(baseLoc))
        {
            var withBase = Combine(Combine(dir, baseLoc!), loc);
            if (_entries.ContainsKey(withBase)) return withBase;
        }
        return Combine(dir, loc);
    }

    /// <summary>相对资源文件取字节，遵循 Res 根节点的 BaseLoc。</summary>
    public byte[]? ResolveResBytes(string resFilePath, XElement resRoot, string loc)
    {
        var full = ResolveResPath(resFilePath, resRoot, loc);
        return full == null ? null : GetBytes(full);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _entries.Clear();
    }

    // ---- 路径工具 ----

    internal static string Normalize(string loc)
        => "/" + loc.TrimStart('/').Replace('\\', '/');

    internal static string DirOf(string path)
    {
        var i = path.LastIndexOf('/');
        if (i <= 0) return "/";
        return path.Substring(0, i);
    }

    internal static string Combine(string dir, string rel)
        => Normalize(rel.StartsWith("/") ? rel : dir.TrimEnd('/') + "/" + rel);
}
