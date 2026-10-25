using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NetOfd.Tool;

/// <summary>
/// OFD 包（ZIP）与 XML 的读写辅助。
/// 采用与 NetOfd.Sign 一致的 zip + XML 层策略，避免依赖半成品容器层。
/// </summary>
internal static class OfdZip
{
    /// <summary>OFD 规范命名空间</summary>
    public static readonly XNamespace Ns = "http://www.ofdspec.org/2016";

    /// <summary>读取 OFD 包内所有条目为 路径→字节 字典（键统一为正斜杠、去前导 /）。</summary>
    public static Dictionary<string, byte[]> ReadZip(string path)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        using var za = ZipFile.OpenRead(path);
        foreach (var e in za.Entries)
        {
            if (e.FullName.EndsWith("/")) continue;
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            map[NormalizeKey(e.FullName)] = ms.ToArray();
        }
        return map;
    }

    /// <summary>将 路径→字节 字典写入 OFD 包字节流（mimetype 不压缩且置于首位以兼容规范）。</summary>
    public static byte[] WriteZip(Dictionary<string, byte[]> entries)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            if (!entries.ContainsKey("mimetype"))
            {
                var me = za.CreateEntry("mimetype", CompressionLevel.NoCompression);
                using var ws = me.Open();
                var mb = Encoding.ASCII.GetBytes("application/ofd");
                ws.Write(mb, 0, mb.Length);
            }
            foreach (var kv in entries)
            {
                var key = NormalizeKey(kv.Key);
                if (key.Equals("mimetype", StringComparison.OrdinalIgnoreCase)) continue;
                var e = za.CreateEntry(key);
                using var s = e.Open();
                s.Write(kv.Value, 0, kv.Value.Length);
            }
        }
        return ms.ToArray();
    }

    /// <summary>从字典中读取并返回根 XElement（不存在或为空返回 null）。</summary>
    public static XElement? TryLoad(Dictionary<string, byte[]> map, string key)
    {
        if (!map.TryGetValue(NormalizeKey(key), out var bytes) || bytes == null || bytes.Length == 0)
            return null;
        using var ms = new MemoryStream(bytes);
        return XElement.Load(ms);
    }

    /// <summary>将 XElement 序列化为 UTF-8（含 XML 声明）字节。</summary>
    public static byte[] ToBytes(XElement el)
    {
        var doc = new XDocument(new XDeclaration("1.0", "UTF-8", null), el);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = true,
            OmitXmlDeclaration = false
        };
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings))
        {
            doc.Save(w);
            w.Flush();
        }
        return ms.ToArray();
    }

    /// <summary>归一化包内路径键：去反斜杠、去前导 /。</summary>
    public static string NormalizeKey(string name)
    {
        var n = name.Replace('\\', '/').TrimStart('/');
        return n;
    }
}
