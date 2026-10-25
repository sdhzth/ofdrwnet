using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NetOfd.Archive.Convert;

/// <summary>
/// OFD 包可写工作区（zip + XML 层）。
/// <para>与只读的 <see cref="ArchiveContext"/> 对应，供去技术化处理器就地修改包内容。
/// 全部条目以容器绝对路径（"/" 开头，大小写不敏感）为键；XML 条目解析后进入缓存，
/// 处理器拿到的是<b>同一个可变 XElement 实例</b>，多次修改累积生效，打包前统一回写。</para>
/// 对应 Java: org.ofdrw.pkg.container.OFDDir + OFDReader（可写视图）
/// </summary>
public sealed class ArchiveWorkspace : IDisposable
{
    /// <summary>OFD 默认命名空间（GB/T 33190-2016）。</summary>
    public const string OfdNamespace = "http://www.ofdspec.org/2016";

    private readonly Dictionary<string, byte[]> _entries;
    private readonly Dictionary<string, XDocument> _xml;
    private bool _disposed;

    public ArchiveWorkspace(string ofdPath)
    {
        _entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        _xml = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
        LoadZip(File.ReadAllBytes(ofdPath));
    }

    public ArchiveWorkspace(byte[] ofdBytes)
    {
        _entries = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        _xml = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
        LoadZip(ofdBytes);
    }

    private void LoadZip(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in zip.Entries)
        {
            if (entry.FullName.EndsWith("/")) continue;
            using var buf = new MemoryStream();
            using (var s = entry.Open()) s.CopyTo(buf);
            _entries[ArchiveContext.Normalize(entry.FullName)] = buf.ToArray();
        }
    }

    /// <summary>丢弃当前全部内容，从新的 OFD 字节重新装载（解密后重载场景）。</summary>
    public void ReloadFrom(byte[] ofdBytes)
    {
        _entries.Clear();
        _xml.Clear();
        LoadZip(ofdBytes);
    }

    // ---- 条目读写 ----

    /// <summary>全部条目路径快照（可在遍历中安全删除）。</summary>
    public IReadOnlyList<string> Keys => _entries.Keys.ToList();

    public bool Exists(string loc) => _entries.ContainsKey(ArchiveContext.Normalize(loc));

    /// <summary>取条目字节（若该条目已被解析并修改，先回写再返回）。</summary>
    public byte[]? GetBytes(string loc)
    {
        var key = ArchiveContext.Normalize(loc);
        if (_xml.TryGetValue(key, out var doc)) _entries[key] = Serialize(doc);
        return _entries.TryGetValue(key, out var b) ? b : null;
    }

    /// <summary>取条目 UTF-8 文本。</summary>
    public string? GetText(string loc)
    {
        var b = GetBytes(loc);
        return b == null ? null : Encoding.UTF8.GetString(b);
    }

    /// <summary>写入条目字节（同时失效对应 XML 缓存）。</summary>
    public void SetBytes(string loc, byte[] data)
    {
        var key = ArchiveContext.Normalize(loc);
        _xml.Remove(key);
        _entries[key] = data;
    }

    /// <summary>删除条目；返回是否存在过。</summary>
    public bool Delete(string loc)
    {
        var key = ArchiveContext.Normalize(loc);
        _xml.Remove(key);
        return _entries.Remove(key);
    }

    /// <summary>递归删除目录下全部条目；返回删除数量。</summary>
    public int DeleteDirectory(string dirLoc)
    {
        var prefix = ArchiveContext.Normalize(dirLoc).TrimEnd('/') + "/";
        if (prefix == "/") return 0; // 拒绝清空整个包
        var victims = _entries.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();
        foreach (var k in victims) { _entries.Remove(k); _xml.Remove(k); }
        return victims.Count;
    }

    // ---- XML 访问（可变、带缓存） ----

    /// <summary>解析并缓存 XML，返回<b>可变</b>根元素；不存在或解析失败返回 null。</summary>
    public XElement? Xml(string loc)
    {
        var key = ArchiveContext.Normalize(loc);
        if (_xml.TryGetValue(key, out var cached)) return cached.Root;
        if (!_entries.TryGetValue(key, out var bytes)) return null;
        XDocument doc;
        try { doc = XDocument.Parse(Encoding.UTF8.GetString(bytes), LoadOptions.PreserveWhitespace); }
        catch (Exception) { return null; }
        _xml[key] = doc;
        return doc.Root;
    }

    /// <summary>以给定根元素登记（或替换）一个 XML 条目。</summary>
    public XElement SetXml(string loc, XElement root)
    {
        var key = ArchiveContext.Normalize(loc);
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), root);
        _xml[key] = doc;
        _entries[key] = Serialize(doc);
        return root;
    }

    /// <summary>将全部 XML 缓存回写到条目字节。</summary>
    public void Flush()
    {
        foreach (var kv in _xml) _entries[kv.Key] = Serialize(kv.Value);
    }

    private static byte[] Serialize(XDocument doc)
    {
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = false,
            OmitXmlDeclaration = false,
        };
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings)) doc.Save(w);
        return ms.ToArray();
    }

    // ---- 打包输出 ----

    /// <summary>回写并打包为 OFD 字节。</summary>
    public byte[] ToBytes()
    {
        Flush();
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            // OFD.xml 必须是首个条目（GB/T 33190-2016 7.1）
            foreach (var key in _entries.Keys.OrderBy(k =>
                         k.Equals("/OFD.xml", StringComparison.OrdinalIgnoreCase) ? 0 : 1)
                     .ThenBy(k => k, StringComparer.Ordinal))
            {
                var entry = zip.CreateEntry(key.TrimStart('/'), CompressionLevel.Optimal);
                using var s = entry.Open();
                var data = _entries[key];
                s.Write(data, 0, data.Length);
            }
        }
        return ms.ToArray();
    }

    /// <summary>回写并打包保存到文件。</summary>
    public void SaveTo(string path)
    {
        var dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
        File.WriteAllBytes(path, ToBytes());
    }

    // ---- 结构导航（返回可变元素；均已物化，可在遍历中安全修改） ----

    public XElement? OfdRoot => Xml("/OFD.xml");

    public IReadOnlyList<XElement> DocBodies()
        => OfdRoot?.LocalElems("DocBody").ToList() ?? new List<XElement>();

    /// <summary>每个文档的 (容器路径, Document.xml 根)。</summary>
    public IReadOnlyList<(string Path, XElement Root)> Documents()
    {
        var list = new List<(string, XElement)>();
        foreach (var body in DocBodies())
        {
            var docRoot = body.LocalElem("DocRoot")?.Value.Trim();
            if (string.IsNullOrEmpty(docRoot)) continue;
            var path = ArchiveContext.Normalize(docRoot!);
            var root = Xml(path);
            if (root != null) list.Add((path, root));
        }
        return list;
    }

    /// <summary>每个页面的 (容器路径, Content.xml 根)。</summary>
    public IReadOnlyList<(string Path, XElement Root)> Pages()
    {
        var list = new List<(string, XElement)>();
        foreach (var (docPath, doc) in Documents())
        {
            var docDir = ArchiveContext.DirOf(docPath);
            foreach (var page in doc.LocalElems("Pages").SelectMany(p => p.LocalElems("Page")))
            {
                var baseLoc = page.AttrTrim("BaseLoc");
                if (string.IsNullOrEmpty(baseLoc)) continue;
                var pagePath = ArchiveContext.Combine(docDir, baseLoc!);
                var root = Xml(pagePath);
                if (root != null) list.Add((pagePath, root));
            }
        }
        return list;
    }

    /// <summary>全部资源文件 (PublicRes / DocumentRes / PageRes*.xml) 的 (容器路径, Res 根)。</summary>
    public IReadOnlyList<(string Path, XElement Root)> ResourceFiles()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<(string, XElement)>();
        void Add(string? path)
        {
            if (string.IsNullOrEmpty(path)) return;
            var norm = ArchiveContext.Normalize(path!);
            if (!seen.Add(norm)) return;
            var root = Xml(norm);
            if (root != null) list.Add((norm, root));
        }

        foreach (var (docPath, doc) in Documents())
        {
            var docDir = ArchiveContext.DirOf(docPath);
            var cd = doc.LocalElem("CommonData");
            if (cd != null)
            {
                foreach (var name in new[] { "PublicRes", "DocumentRes" })
                    foreach (var e in cd.LocalElems(name))
                    {
                        var loc = e.Value.Trim();
                        if (!string.IsNullOrEmpty(loc)) Add(ArchiveContext.Combine(docDir, loc));
                    }
            }
            foreach (var key in Keys)
            {
                if (!key.StartsWith(docDir + "/", StringComparison.OrdinalIgnoreCase)) continue;
                var fname = Path.GetFileName(key);
                if (fname.StartsWith("PageRes", StringComparison.OrdinalIgnoreCase)
                    && fname.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                    Add(key);
            }
        }
        return list;
    }

    /// <summary>解析资源文件内的相对 loc，遵循 Res 根节点的 BaseLoc。</summary>
    public string? ResolveResPath(string resFilePath, XElement resRoot, string loc)
    {
        if (string.IsNullOrEmpty(loc)) return null;
        if (loc.StartsWith("/")) return ArchiveContext.Normalize(loc);
        var dir = ArchiveContext.DirOf(resFilePath);
        var baseLoc = resRoot.AttrTrim("BaseLoc");
        if (!string.IsNullOrEmpty(baseLoc))
        {
            var withBase = ArchiveContext.Combine(ArchiveContext.Combine(dir, baseLoc!), loc);
            if (Exists(withBase)) return withBase;
        }
        return ArchiveContext.Combine(dir, loc);
    }

    /// <summary>取元素所属命名空间（无命名空间时回退到 OFD 默认命名空间）。</summary>
    public static XNamespace NsOf(XElement e)
        => e.Name.Namespace == XNamespace.None ? XNamespace.None : e.Name.Namespace;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _entries.Clear();
        _xml.Clear();
    }
}
