using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace NetOfd.Crypto;

/// <summary>zip 包读写助手：以「带前导 / 的容器路径」为键的内存映射。</summary>
internal static class ZipIO
{
    /// <summary>将 zip 内所有条目读入字典，键为 "/"+相对路径（正斜杠）。目录条目（以 / 结尾）被忽略。</summary>
    public static Dictionary<string, byte[]> ReadZip(string path)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var za = ZipFile.OpenRead(path);
        foreach (var entry in za.Entries)
        {
            if (IsDirectoryEntry(entry)) continue;
            using var s = entry.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var key = "/" + entry.FullName.Replace('\\', '/').TrimStart('/');
            map[key] = ms.ToArray();
        }
        return map;
    }

    /// <summary>将 zip 字节读入字典，键为 "/"+相对路径（正斜杠）。目录条目（以 / 结尾）被忽略。</summary>
    public static Dictionary<string, byte[]> ReadBytes(byte[] zipBytes)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var ms = new MemoryStream(zipBytes);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var entry in za.Entries)
        {
            if (IsDirectoryEntry(entry)) continue;
            using var s = entry.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            var key = "/" + entry.FullName.Replace('\\', '/').TrimStart('/');
            map[key] = buf.ToArray();
        }
        return map;
    }

    /// <summary>判断是否为 zip 目录条目（名以 / 结尾）。</summary>
    private static bool IsDirectoryEntry(ZipArchiveEntry entry)
    {
        string name = entry.FullName;
        return name.EndsWith('/') || name.EndsWith('\\')
            || (name.Length == 0 && entry.Length == 0);
    }

    /// <summary>将字典写回 zip 字节（键去除前导 / 作为条目名）。</summary>
    public static byte[] WriteBytes(Dictionary<string, byte[]> map)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var kv in map)
            {
                var name = kv.Key.TrimStart('/');
                if (name.Length == 0) continue;
                var e = za.CreateEntry(name);
                using var s = e.Open();
                s.Write(kv.Value, 0, kv.Value.Length);
            }
        }
        return ms.ToArray();
    }

    /// <summary>将字典写回 zip（键去除前导 / 作为条目名）。</summary>
    public static void WriteZip(Dictionary<string, byte[]> map, string path)
    {
        if (File.Exists(path)) File.Delete(path);
        using var za = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var kv in map)
        {
            var name = kv.Key.TrimStart('/');
            if (name.Length == 0) continue;
            var e = za.CreateEntry(name);
            using var s = e.Open();
            s.Write(kv.Value, 0, kv.Value.Length);
        }
    }

    /// <summary>将 XElement 序列化为 UTF-8（含 XML 声明）字节。</summary>
    public static byte[] ToBytes(XElement el)
    {
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), el);
        var settings = new System.Xml.XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            OmitXmlDeclaration = false
        };
        using var ms = new MemoryStream();
        using (var w = System.Xml.XmlWriter.Create(ms, settings))
        {
            doc.Save(w);
            w.Flush();
        }
        return ms.ToArray();
    }

    /// <summary>将字节解析为 XElement。</summary>
    public static XElement ToXElement(byte[] bytes)
    {
        using var ms = new MemoryStream(bytes);
        return XElement.Load(ms);
    }
}
