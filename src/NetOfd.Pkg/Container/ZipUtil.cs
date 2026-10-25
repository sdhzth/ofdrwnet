using System;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace NetOfd.Pkg.Container;

/// <summary>
/// OFD 包（ZIP）解压工具。
/// 对应 Java: org.ofdrw.reader.ZipUtil
/// <para>
/// 与 <see cref="ZipFile.ExtractToDirectory(string, string)"/> 相比，本工具提供三项
/// OFD 实战必需的防护（Java 版均具备，.NET BCL 版缺失）：
/// <list type="number">
/// <item><b>Zip Slip 路径穿越防护</b>：拒绝 <c>../</c> 形式的条目名，防止写穿解压根目录；</item>
/// <item><b>异常结尾容错</b>：部分 OFD 生成器产出的压缩包结尾不完整，读取到 <c>EndOfStreamException</c>
/// / <c>InvalidDataException</c> 时<b>忽略并保持已解压内容</b>，而不是整包失败；</item>
/// <item><b>条目名编码可配置</b>：默认 UTF-8；当条目未置 UTF-8 标志位（bit 11）而实际为 GBK 时，
/// 设 <see cref="DefaultEncoding"/> 为 GBK 即可正确解码中文条目名。</item>
/// </list>
/// </para>
/// </summary>
public static class ZipUtil
{
    /// <summary>
    /// 解压默认字符集，默认 UTF-8。
    /// <para>若压缩包条目设置了 UTF-8 标志位，.NET 仍优先按 UTF-8 解码，
    /// 因此设为 GBK 只会影响未置位（多为老旧国产工具生成）的条目，是安全的回退。</para>
    /// </summary>
    public static Encoding DefaultEncoding { get; set; } = Encoding.UTF8;

    /// <summary>解压到指定目录（按文件路径）。</summary>
    /// <param name="zipPath">待解压的压缩包路径</param>
    /// <param name="descDir">解压目标目录</param>
    /// <exception cref="IOException">压缩包不存在，或条目名非法（路径穿越）</exception>
    public static void UnZipFiles(string zipPath, string descDir)
    {
        if (!File.Exists(zipPath))
            throw new IOException("解压文件不存在: " + zipPath);

        using var fs = File.OpenRead(zipPath);
        UnZipFiles(fs, descDir);
    }

    /// <summary>解压到指定目录（按流）。</summary>
    /// <param name="src">压缩包数据流（需可读取）</param>
    /// <param name="descDir">解压目标目录</param>
    /// <exception cref="IOException">条目名非法（路径穿越）</exception>
    public static void UnZipFiles(Stream src, string descDir)
    {
        if (src is null) throw new ArgumentNullException(nameof(src));
        if (string.IsNullOrWhiteSpace(descDir)) throw new ArgumentException("解压目标目录不能为空", nameof(descDir));

        // 规范化解压根目录（去结尾分隔符），用于后续路径穿越判定
        var root = Path.GetFullPath(descDir);
        Directory.CreateDirectory(root);

        using var za = new ZipArchive(src, ZipArchiveMode.Read, leaveOpen: true, entryNameEncoding: DefaultEncoding);

        foreach (var entry in za.Entries)
        {
            string name = entry.FullName;
            if (string.IsNullOrEmpty(name)) continue;

            // ── 1. 路径穿越防护（Zip Slip）──────────────────────
            string dest;
            try
            {
                dest = Path.GetFullPath(Path.Combine(root, name.Replace('\\', '/')));
            }
            catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
            {
                throw new IOException($"不合法的路径：{name}", e);
            }

            if (!IsUnderRoot(dest, root))
                throw new IOException($"不合法的路径：{name}");

            // ── 2. 目录条目 ────────────────────────────────────
            if (name.EndsWith("/", StringComparison.Ordinal))
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            // ── 3. 文件条目（异常结尾容错包裹）─────────────────
            var parent = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);

            try
            {
                entry.ExtractToFile(dest, overwrite: true);
            }
            catch (Exception e) when (IsTruncatedTail(e))
            {
                // 部分 OFD 压缩包结尾不完整：忽略异常结尾，保留已解压内容继续可用。
                break;
            }
        }
    }

    /// <summary>判断目标路径是否位于解压根目录之内（含根自身）。</summary>
    private static bool IsUnderRoot(string dest, string root)
    {
        var cmp = StringComparison.Ordinal;
        if (dest.Equals(root, cmp)) return true;

        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            ? root
            : root + Path.DirectorySeparatorChar;
        return dest.StartsWith(prefix, cmp);
    }

    /// <summary>
    /// 是否为"压缩包结尾不完整"类异常。
    /// <para>对应 Java 版捕获 <c>EOFException</c> 的处理：忽略异常结尾以期继续处理该 OFD 文件。</para>
    /// </summary>
    private static bool IsTruncatedTail(Exception e)
    {
        for (Exception? cur = e; cur is not null; cur = cur.InnerException)
        {
            if (cur is EndOfStreamException or InvalidDataException) return true;
        }
        return false;
    }
}
