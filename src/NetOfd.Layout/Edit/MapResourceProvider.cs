using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.Resources;
using NetOfd.Layout.Engine;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 基于包映射的资源管理器：
/// 字体声明写入 PublicRes.xml、图片声明写入 DocumentRes.xml（按需懒创建），
/// 二进制文件写入 Doc_0/Res/ 目录。对齐 .NET 版 OFDDoc 的资源布局约定。
/// 供 <see cref="OfdAnnotator"/>、<see cref="OfdEditor"/> 等映射式编辑器共用。
/// </summary>
internal sealed class MapResourceProvider : IResourceProvider
{
    private const string ResDirName = "Res";

    private readonly Dictionary<string, byte[]> _map;
    private readonly Func<long> _nextId;
    private readonly string _docDir;
    private readonly CTCommonData _commonData;

    private XDocument? _publicResDoc;
    private Res? _publicRes;
    private string? _publicResPath;
    private bool _publicResDirty;

    private XDocument? _documentResDoc;
    private Res? _documentRes;
    private string? _documentResPath;
    private bool _documentResDirty;

    private readonly Dictionary<string, string> _fontCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _mediaCache = new(StringComparer.OrdinalIgnoreCase);

    public MapResourceProvider(Dictionary<string, byte[]> map, string docDir,
        Func<long> nextId, CTCommonData commonData)
    {
        _map = map;
        _docDir = docDir;
        _nextId = nextId;
        _commonData = commonData;
    }

    /// <summary>获取（或注册）字体的引用 ID。</summary>
    public string GetFontId(string fontFamily)
    {
        if (string.IsNullOrEmpty(fontFamily)) fontFamily = "SimSun";
        if (_fontCache.TryGetValue(fontFamily, out var cached)) return cached;

        var pub = EnsurePublicRes();
        foreach (var f in pub.GetFonts())
        {
            if (string.Equals(f.FontName, fontFamily, StringComparison.OrdinalIgnoreCase)
                && f.ID != null)
            {
                var id = f.ID.Value.ToString();
                _fontCache[fontFamily] = id;
                return id;
            }
        }

        var newId = _nextId();
        pub.AddFont(new CT_Font
        {
            ID = new STID(newId),
            FontName = fontFamily,
            FamilyName = fontFamily
        });
        _publicResDirty = true;
        var fontId = newId.ToString();
        _fontCache[fontFamily] = fontId;
        return fontId;
    }

    /// <summary>注册外部字体文件，复制进包内并在 PublicRes 中声明 FontFile。</summary>
    public string AddFont(string familyName, string fontFilePath)
    {
        if (string.IsNullOrEmpty(familyName))
            throw new ArgumentException("字体名称不能为空", nameof(familyName));
        if (string.IsNullOrEmpty(fontFilePath) || !File.Exists(fontFilePath))
            throw new ArgumentException("字体文件不存在", nameof(fontFilePath));

        if (_fontCache.TryGetValue(familyName, out var cached)) return cached;

        var pub = EnsurePublicRes();
        var newId = _nextId();
        var ext = Path.GetExtension(fontFilePath);
        var fileName = $"F{newId}{ext}";
        _map[$"{_docDir}/{ResDirName}/{fileName}"] = File.ReadAllBytes(fontFilePath);

        pub.AddFont(new CT_Font
        {
            ID = new STID(newId),
            FontName = familyName,
            FamilyName = familyName,
            FontFile = new STLoc($"{ResDirName}/{fileName}")
        });
        _publicResDirty = true;
        var fontId = newId.ToString();
        _fontCache[familyName] = fontId;
        return fontId;
    }

    /// <summary>添加媒体资源（图片）：复制进包内并在 DocumentRes 中声明。</summary>
    public string AddMedia(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            throw new ArgumentException("媒体文件不存在", nameof(filePath));

        var key = Path.GetFileName(filePath).ToUpperInvariant();
        if (_mediaCache.TryGetValue(key, out var cached)) return cached;

        var docRes = EnsureDocumentRes();
        var newId = _nextId();
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var fileName = $"{newId}{ext}";
        _map[$"{_docDir}/{ResDirName}/{fileName}"] = File.ReadAllBytes(filePath);

        var mediaId = newId.ToString();
        var exists = docRes.GetMultiMedias().Any(m => m.ID != null && m.ID.Value == newId);
        if (!exists)
        {
            docRes.AddMultiMedia(new CT_MultiMedia
            {
                ID = new STID(newId),
                Type = "Image",
                Format = DeriveFormat(ext),
                MediaFile = new STLoc($"{ResDirName}/{fileName}")
            });
            _documentResDirty = true;
        }
        _mediaCache[key] = mediaId;
        return mediaId;
    }

    public long NewId() => _nextId();

    /// <summary>将修改过的资源声明文件回写至包映射。</summary>
    public void Flush()
    {
        if (_publicResDirty && _publicResDoc != null && _publicResPath != null)
            _map[_publicResPath] = ZipMapIO.ToUtf8Bytes(_publicResDoc.Root!);
        if (_documentResDirty && _documentResDoc != null && _documentResPath != null)
            _map[_documentResPath] = ZipMapIO.ToUtf8Bytes(_documentResDoc.Root!);
    }

    // ── 懒解析/创建资源声明文件 ─────────────────────────────

    private Res EnsurePublicRes()
    {
        if (_publicRes != null) return _publicRes!;
        (_publicResDoc, _publicRes, _publicResPath) =
            ResolveResFile(_commonData.PublicResList,
                (cd, loc) => cd.AddPublicRes(loc), "PublicRes.xml");
        return _publicRes!;
    }

    private Res EnsureDocumentRes()
    {
        if (_documentRes != null) return _documentRes!;
        (_documentResDoc, _documentRes, _documentResPath) =
            ResolveResFile(_commonData.DocumentResList,
                (cd, loc) => cd.AddDocumentRes(loc), "DocumentRes.xml");
        return _documentRes!;
    }

    private (XDocument Doc, Res Res, string Path) ResolveResFile(
        IReadOnlyList<STLoc> locs, Action<CTCommonData, STLoc> register, string defaultFileName)
    {
        // 已声明的资源文件存在则直接复用
        foreach (var loc in locs)
        {
            var found = ZipMapIO.TryResolve(_map, loc.Value, _docDir);
            if (found != null)
            {
                var doc = ZipMapIO.LoadXml(_map, found);
                return (doc, new Res(doc.Root!), found);
            }
        }

        // 不存在则创建（位置沿用首个声明，否则使用默认名）
        var fileName = defaultFileName;
        foreach (var loc in locs)
        {
            var n = ZipMapIO.Normalize(loc.Value);
            if (n.Length > 0)
            {
                fileName = n.Contains('/') ? n[(n.LastIndexOf('/') + 1)..] : n;
                break;
            }
        }
        var path = $"{_docDir}/{fileName}";
        register(_commonData, new STLoc(fileName));
        var newDoc = new XDocument(new XElement(OFDElement.OfdNs + "Res"));
        return (newDoc, new Res(newDoc.Root!), path);
    }

    private static string DeriveFormat(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "PNG",
        ".jpg" or ".jpeg" => "JPEG",
        ".tif" or ".tiff" => "TIFF",
        ".gif" => "GIF",
        ".bmp" => "BMP",
        _ => ext.TrimStart('.').ToUpperInvariant()
    };
}
