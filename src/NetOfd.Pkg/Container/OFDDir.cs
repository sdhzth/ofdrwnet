using System.IO.Compression;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.OFD;

namespace NetOfd.Pkg.Container;

/// <summary>
/// OFD 包的根容器，对应 OFD ZIP 包的根目录。
/// 提供：打开 OFD 文件、创建新 OFD、以及最终打包（jar）功能。
/// 对应 Java: org.ofdrw.pkg.container.OFDDir
/// </summary>
public class OFDDir : VirtualContainer, IDisposable
{
    public const string OFD_FILE = "OFD.xml";

    // ── 临时目录（解包时使用）───────────────────────────────
    private readonly string? _tempDir;
    private bool _disposedValue;

    public OFDDir() : base("ROOT") { }

    private OFDDir(string tempDir) : base("ROOT", tempDir)
    {
        _tempDir = tempDir;
    }

    // ────────────────────────────────────────────────────────
    //  工厂方法
    // ────────────────────────────────────────────────────────

    /// <summary>创建新的空 OFD 包（内存中）</summary>
    public static OFDDir NewOFD()
    {
        return new OFDDir();
    }

    /// <summary>从 OFD 文件解包到临时目录，并返回 OFDDir</summary>
    public static OFDDir FromFile(string ofdFilePath)
    {
        if (!File.Exists(ofdFilePath))
            throw new FileNotFoundException("OFD 文件不存在", ofdFilePath);

        string tempDir = Path.Combine(Path.GetTempPath(), "netofd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        // 走 ZipUtil 而非 ZipFile.ExtractToDirectory：
        // 增加 Zip Slip 路径穿越防护、异常结尾容错、条目名编码可配置（见 ZipUtil 文档）。
        ZipUtil.UnZipFiles(ofdFilePath, tempDir);

        return new OFDDir(tempDir);
    }

    /// <summary>从 Stream 解包 OFD（流式读取）</summary>
    public static OFDDir FromStream(Stream stream)
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "netofd_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        // 同 FromFile：统一走 ZipUtil 以获得路径穿越防护与异常结尾容错。
        ZipUtil.UnZipFiles(stream, tempDir);

        return new OFDDir(tempDir);
    }

    // ────────────────────────────────────────────────────────
    //  OFD.xml 操作
    // ────────────────────────────────────────────────────────

    /// <summary>设置 OFD.xml 根元素</summary>
    public OFDDir SetOfd(OFDRoot ofd)
    {
        PutFile(OFD_FILE, ofd.Element);
        return this;
    }

    /// <summary>获取 OFD.xml 根元素</summary>
    public OFDRoot? GetOfd()
    {
        var el = GetFile(OFD_FILE);
        return el is null ? null : new OFDRoot(el);
    }

    /// <summary>获取或创建 OFD.xml</summary>
    public OFDRoot ObtainOfd()
    {
        var ofd = GetOfd();
        if (ofd is not null) return ofd;
        ofd = new OFDRoot();
        SetOfd(ofd);
        return ofd;
    }

    // ────────────────────────────────────────────────────────
    //  文档子目录操作
    // ────────────────────────────────────────────────────────

    /// <summary>获取第 index 个文档目录（0-based），默认 Doc_0；优先从缓存返回</summary>
    public DocDir GetDocByIndex(int index = 0)
    {
        string dirName = $"Doc_{index}";
        // 先查缓存，有就直接返回（ObtainDocDir 已缓存的实例）
        if (DirCache.TryGetValue(dirName, out var cached) && cached is DocDir dd)
            return dd;
        // 缓存里没有则新建并放入缓存
        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, dirName) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var docDir = new DocDir(dirName, childPhys);
        docDir.Parent = this;
        DirCache[dirName] = docDir;
        return docDir;
    }

    /// <summary>获取或创建第一个（也是最新的）文档目录</summary>
    public DocDir ObtainDoc()
    {
        // 默认始终使用 Doc_0
        return GetDocByIndex(0);
    }

    /// <summary>将子目录注册为 DocDir</summary>
    public DocDir ObtainDocDir(string dirName = "Doc_0")
    {
        var existing = DirCache.TryGetValue(dirName, out var c) ? c : null;
        if (existing is DocDir dd) return dd;

        string? childPhys = PhysicalPath is not null
            ? Path.Combine(PhysicalPath, dirName)
            : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;

        var docDir = new DocDir(dirName, childPhys);
        docDir.Parent = this;
        DirCache[dirName] = docDir;
        return docDir;
    }

    // ────────────────────────────────────────────────────────
    //  打包（jar）
    // ────────────────────────────────────────────────────────

    /// <summary>将 OFD 包打包为 ZIP 文件并写出到指定路径</summary>
    public void Jar(string outputPath)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        Jar(fs);
    }

    /// <summary>将 OFD 包打包为 ZIP 并写出到 Stream</summary>
    public void Jar(Stream outputStream)
    {
        using var zip = new ZipArchive(outputStream, ZipArchiveMode.Create, leaveOpen: true);
        WriteToZip(zip, "");
    }

    // ────────────────────────────────────────────────────────
    //  Dispose
    // ────────────────────────────────────────────────────────
    protected override void Dispose(bool disposing)
    {
        if (!_disposedValue)
        {
            if (disposing && _tempDir is not null && Directory.Exists(_tempDir))
            {
                try { Directory.Delete(_tempDir, recursive: true); } catch { }
            }
            _disposedValue = true;
        }
        base.Dispose(disposing);
    }
}
