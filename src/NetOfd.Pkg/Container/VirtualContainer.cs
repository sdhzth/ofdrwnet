using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace NetOfd.Pkg.Container;

/// <summary>
/// 虚拟容器 - OFD 包内的内存虚拟文件系统节点。
/// 对应 Java: org.ofdrw.pkg.container.VirtualContainer
/// 
/// 每个 VirtualContainer 代表 OFD ZIP 包内的一个"目录"。
/// - fileCache: 缓存已加载/创建的 XML 文件（XElement）
/// - dirCache:  缓存子目录（VirtualContainer）
/// - rawFiles:  缓存二进制文件（图片、字体等）
/// - physicalPath: 对应磁盘上的临时目录路径（读取时使用）
/// </summary>
public class VirtualContainer : IDisposable
{
    // 标识是否已释放（供子类使用）
    private bool _vcDisposed;
    // ── 容器名称（目录名）───────────────────────────────────
    public string Name { get; }

    // ── 父容器 ──────────────────────────────────────────────
    public VirtualContainer? Parent { get; internal set; }

    // ── 缓存层 ──────────────────────────────────────────────
    /// <summary>XML 文件缓存：文件名 → XElement</summary>
    protected readonly Dictionary<string, XElement> FileCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>子目录缓存：目录名 → VirtualContainer</summary>
    protected readonly Dictionary<string, VirtualContainer> DirCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>二进制文件缓存：文件名 → 字节数组</summary>
    protected readonly Dictionary<string, byte[]> RawFiles = new(StringComparer.OrdinalIgnoreCase);

    // ── 磁盘路径（解包后的临时路径，读取时使用）─────────────
    internal string? PhysicalPath { get; set; }

    public VirtualContainer(string name, string? physicalPath = null)
    {
        Name = name;
        PhysicalPath = physicalPath;
    }

    // ────────────────────────────────────────────────────────
    //  XML 文件操作
    // ────────────────────────────────────────────────────────

    /// <summary>向容器写入 XElement（XML 文件）</summary>
    public void PutFile(string fileName, XElement element)
    {
        FileCache[fileName] = element;
    }

    /// <summary>获取 XML 文件（优先缓存，其次从磁盘读取）</summary>
    public XElement? GetFile(string fileName)
    {
        if (FileCache.TryGetValue(fileName, out var cached))
            return cached;

        if (PhysicalPath is not null)
        {
            string filePath = Path.Combine(PhysicalPath, fileName);
            if (File.Exists(filePath))
            {
                var el = XElement.Load(filePath);
                FileCache[fileName] = el;
                return el;
            }
        }
        return null;
    }

    /// <summary>检查 XML 文件是否存在</summary>
    public bool FileExists(string fileName)
    {
        if (FileCache.ContainsKey(fileName)) return true;
        if (PhysicalPath is not null)
            return File.Exists(Path.Combine(PhysicalPath, fileName));
        return false;
    }

    /// <summary>
    /// 删除容器内文件（XML 缓存与原始字节均移除；物理文件存在则尽力一并删除）。
    /// 对应 Java VirtualContainer.deleteFile（ofdrw-layout areaholder AreaHolderBlocksProcess 使用）。
    /// </summary>
    public void DeleteFile(string fileName)
    {
        FileCache.Remove(fileName);
        RawFiles.Remove(fileName);
        if (PhysicalPath is not null)
        {
            string filePath = Path.Combine(PhysicalPath, fileName);
            try
            {
                if (File.Exists(filePath)) File.Delete(filePath);
            }
            catch (IOException)
            {
                // 临时目录尽力删除，失败静默（缓存层已移除，写入 zip 时不会再输出）
            }
        }
    }

    // ────────────────────────────────────────────────────────
    //  二进制文件操作
    // ────────────────────────────────────────────────────────

    /// <summary>写入二进制文件（图片/字体等）</summary>
    public void AddRaw(string fileName, byte[] data)
    {
        RawFiles[fileName] = data;
    }

    /// <summary>写入二进制文件（从 Stream）</summary>
    public void AddRaw(string fileName, Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        RawFiles[fileName] = ms.ToArray();
    }

    /// <summary>获取二进制文件字节</summary>
    public byte[]? GetRaw(string fileName)
    {
        if (RawFiles.TryGetValue(fileName, out var data))
            return data;

        if (PhysicalPath is not null)
        {
            string filePath = Path.Combine(PhysicalPath, fileName);
            if (File.Exists(filePath))
                return File.ReadAllBytes(filePath);
        }
        return null;
    }

    // ────────────────────────────────────────────────────────
    //  子目录操作
    // ────────────────────────────────────────────────────────

    /// <summary>获取或创建子目录</summary>
    public VirtualContainer ObtainContainer(string dirName)
    {
        if (DirCache.TryGetValue(dirName, out var existing))
            return existing;

        string? childPhysicalPath = PhysicalPath is not null
            ? Path.Combine(PhysicalPath, dirName)
            : null;

        // 只在磁盘上存在时才设置物理路径
        if (childPhysicalPath is not null && !Directory.Exists(childPhysicalPath))
            childPhysicalPath = null;

        var container = CreateChildContainer(dirName, childPhysicalPath);
        container.Parent = this;
        DirCache[dirName] = container;
        return container;
    }

    /// <summary>子类可以重写，以创建具体类型的子容器</summary>
    protected virtual VirtualContainer CreateChildContainer(string name, string? physicalPath)
        => new VirtualContainer(name, physicalPath);

    /// <summary>检查子目录是否存在</summary>
    public bool DirExists(string dirName)
    {
        if (DirCache.ContainsKey(dirName)) return true;
        if (PhysicalPath is not null)
            return Directory.Exists(Path.Combine(PhysicalPath, dirName));
        return false;
    }

    /// <summary>获取子目录（不存在则返回 null）</summary>
    public VirtualContainer? GetContainer(string dirName)
    {
        if (DirCache.TryGetValue(dirName, out var c)) return c;
        if (PhysicalPath is not null)
        {
            string path = Path.Combine(PhysicalPath, dirName);
            if (Directory.Exists(path))
                return ObtainContainer(dirName);
        }
        return null;
    }

    // ────────────────────────────────────────────────────────
    //  获取完整路径
    // ────────────────────────────────────────────────────────

    /// <summary>获取从根容器到当前容器的路径</summary>
    public string GetAbsolutePath()
    {
        if (Parent is null) return "/" + Name;
        string parentPath = Parent.GetAbsolutePath();
        return parentPath.TrimEnd('/') + "/" + Name;
    }

    // ────────────────────────────────────────────────────────
    //  写出到 ZIP
    // ────────────────────────────────────────────────────────

    /// <summary>
    /// 将此容器内的所有内容写入 ZipArchive。
    /// prefix 是在 ZIP 中的路径前缀，如 "" / "Doc_0/" 等。
    /// </summary>
    /// <summary>
    /// 将 XElement 转换为带 ofd: 前缀的 XML 字符串。
    /// 用于生成 ofd.js 兼容的 OFD 文件（ofd.js 期望 ofd: 前缀）。
    /// </summary>
    private static string OfdXmlWithPrefix(XElement source)
    {
        const string ofdNs = "http://www.ofdspec.org/2016";
        // 输出默认命名空间格式（无前缀）
        string xml = source.ToString(SaveOptions.DisableFormatting);
        // 正则替换所有 XML 标签为 ofd: 前缀（开标签和闭标签都要处理）
        // 模式：</?TagName 后面跟空白、> 或 />
        // 捕获组 1 = 可选的 /，捕获组 2 = 标签名
        var regex = new System.Text.RegularExpressions.Regex(
            @"<(/?)([a-zA-Z_][\w\-.]*)(?=\s|>|/)",
            System.Text.RegularExpressions.RegexOptions.Compiled);
        xml = regex.Replace(xml, "<$1ofd:$2");
        // 将默认 xmlns 声明替换为 xmlns:ofd
        xml = xml.Replace("xmlns=\"" + ofdNs + "\"", "xmlns:ofd=\"" + ofdNs + "\"");
        return xml;
    }

    /// <summary>
    /// 写出前补全缓存：将 <see cref="PhysicalPath"/> 磁盘目录中尚未进入内存缓存
    /// （FileCache/RawFiles/DirCache）的文件与子目录补载入缓存。
    /// <para>
    /// 适用场景：从既有 OFD 解包（FromFile/FromStream 仅 ExtractToDirectory + 记录 PhysicalPath，
    /// 不预填内存缓存）后、再 <c>Jar</c> 写回——若不补全，被 Flush 流程未触达的文件
    /// （如根级 OFD.xml、既有字体/图片资源）会丢失。已存在于缓存的条目（被修改/新写入者）
    /// 优先，不回退到磁盘旧版本。
    /// </para>
    /// </summary>
    private void HydrateFromPhysical()
    {
        if (PhysicalPath is null || !Directory.Exists(PhysicalPath)) return;

        foreach (var filePath in Directory.EnumerateFiles(PhysicalPath))
        {
            string name = Path.GetFileName(filePath);
            if (FileCache.ContainsKey(name) || RawFiles.ContainsKey(name)) continue;
            // OFD 元数据文件均为 .xml；其余（字体/图像/附件/签名值）按二进制加载
            if (name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                GetFile(name);   // GetFile 会把元素写入 FileCache
            else
                // ⚠️ 必须用 AddRaw 而非 GetRaw：GetRaw 只从磁盘读、不写缓存，
                // 而 WriteToZip 只遍历 RawFiles —— 用 GetRaw 会导致编辑模式保存时
                // 字体/图像/附件/签名值等二进制资源被整体丢弃（实测踩过）。
                AddRaw(name, File.ReadAllBytes(filePath));
        }

        foreach (var dirPath in Directory.EnumerateDirectories(PhysicalPath))
        {
            string name = Path.GetFileName(dirPath);
            if (DirCache.ContainsKey(name)) continue;
            ObtainContainer(name); // 递归：子容器 PhysicalPath 同步指向磁盘目录
        }
    }

    internal void WriteToZip(ZipArchive zip, string prefix)
    {
        // 先补全磁盘上尚未进入缓存的文件/子目录（编辑模式从既有 OFD 续写时必需）
        HydrateFromPhysical();

        // OFD 标准命名空间
        var ofdNs = XNamespace.Get("http://www.ofdspec.org/2016");

        // 写 XML 文件
        foreach (var (fileName, element) in FileCache)
        {
            string entryPath = prefix + fileName;
            var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
            using var stream = entry.Open();
            using var writer = new StreamWriter(stream, new UTF8Encoding(false));
            // 写 XML 声明 + 内容
            writer.Write("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");

            // ofd.js 期望所有 OFD XML 标签带 ofd: 前缀
            // 对 OFD 命名空间（而非 xmlns 声明根）的文件，转换为前缀格式
            if (element.Name.Namespace == ofdNs && element.Name.LocalName != "OFD")
            {
                writer.Write(OfdXmlWithPrefix(element));
            }
            else
            {
                writer.Write(element.ToString(SaveOptions.DisableFormatting));
            }
        }

        // 写二进制文件
        foreach (var (fileName, data) in RawFiles)
        {
            string entryPath = prefix + fileName;
            var entry = zip.CreateEntry(entryPath, CompressionLevel.Optimal);
            using var stream = entry.Open();
            stream.Write(data, 0, data.Length);
        }

        // 写子目录
        foreach (var (dirName, child) in DirCache)
        {
            child.WriteToZip(zip, prefix + dirName + "/");
        }
    }

    // ────────────────────────────────────────────────────────
    //  IDisposable
    // ────────────────────────────────────────────────────────
    protected virtual void Dispose(bool disposing)
    {
        if (!_vcDisposed)
        {
            if (disposing)
            {
                FileCache.Clear();
                DirCache.Clear();
                RawFiles.Clear();
            }
            _vcDisposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
