using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.Resources;
using NetOfd.Font;

namespace NetOfd.Layout.Engine;

/// <summary>
/// 公共资源管理器
/// </summary>
public class ResManager : IResourceProvider
{
    private readonly Func<long> _nextId;
    internal readonly Pkg.Container.OFDDir _root;
    internal readonly Pkg.Container.DocDir _docDir;
    private readonly CTCommonData _commonData;
    private readonly Dictionary<string, string> _fontIdMap = new();
    private readonly Dictionary<string, string> _mediaIdMap = new();
    private readonly List<CT_MultiMedia> _multimedias = new();
    private readonly List<CT_Font> _fonts = new();

    public const string DefaultFontId = "F0";

    public ResManager(Pkg.Container.OFDDir root, Pkg.Container.DocDir docDir, Func<long> nextId)
    {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _docDir = docDir ?? throw new ArgumentNullException(nameof(docDir));
        _nextId = nextId ?? throw new ArgumentNullException(nameof(nextId));
        _commonData = new CTCommonData();
    }

    /// <summary>
    /// 注册字体并返回字体ID（字符串形式，如 "3"）
    /// </summary>
    public string GetFontId(string fontFamily)
    {
        if (string.IsNullOrEmpty(fontFamily)) fontFamily = "SimSun";
        var key = fontFamily.ToUpperInvariant();
        if (_fontIdMap.TryGetValue(key, out var existingId)) return existingId;

        // 使用同一个 ID 分配器，确保 CT_Font.ID 与返回的字体引用一致
        var id = _nextId();
        var ctFont = new CT_Font
        {
            FontName = fontFamily,
            FamilyName = fontFamily,
            ID = new STID(id)
        };
        _fonts.Add(ctFont);
        var fontId = $"{id}";
        _fontIdMap[key] = fontId;
        return fontId;
    }

    /// <summary>
    /// 注册外部字体文件并返回字体 ID。
    /// 字体文件将被复制到 Res 目录，并在 PublicRes.xml 中声明 FontFile。
    /// </summary>
    public string AddFont(string familyName, string fontFilePath)
    {
        if (string.IsNullOrEmpty(familyName)) throw new ArgumentException("字体名称不能为空", nameof(familyName));
        if (string.IsNullOrEmpty(fontFilePath) || !File.Exists(fontFilePath))
            throw new ArgumentException("字体文件不存在", nameof(fontFilePath));

        var key = familyName.ToUpperInvariant();
        if (_fontIdMap.TryGetValue(key, out var existingId)) return existingId;

        var id = _nextId();
        var ext = Path.GetExtension(fontFilePath);
        var fontFileName = $"F{id}{ext}";

        var resDir = _docDir.ObtainRes();
        var bytes = File.ReadAllBytes(fontFilePath);
        resDir.AddRaw(fontFileName, bytes);

        var ctFont = new CT_Font
        {
            ID = new STID(id),
            FontName = familyName,
            FamilyName = familyName,
            FontFile = new STLoc($"{Pkg.Container.DocDir.RES_DIR}/{fontFileName}")
        };
        _fonts.Add(ctFont);

        var fontId = $"{id}";
        _fontIdMap[key] = fontId;
        return fontId;
    }

    /// <summary>
    /// 添加媒体资源（图片）并返回媒体ID（数字字符串，与 Content.xml 中
    /// ImageObject 的 ResourceID 完全一致，确保读者能定位到资源声明）。
    /// </summary>
    public string AddMedia(string filePath)
    {
        var key = Path.GetFileName(filePath).ToUpperInvariant();
        if (_mediaIdMap.TryGetValue(key, out var existingId)) return existingId;

        // 复用全局 ID 分配器，确保 CT_MultiMedia.ID 与 ImageObject.ResourceID
        // 均为同一数字 ST_ID（与字体注册方式一致），避免引用错位。
        var id = _nextId();
        var mediaId = $"{id}";
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        var mediaFileName = $"{mediaId}{ext}";

        // 注意：OFDDir 在内存模式下没有物理路径，必须读取字节后写入 ResDir 的
        // 二进制缓存（RawFiles），由 WriteToZip 一并打包进 zip。
        // 早期实现用 File.Copy 到 GetAbsolutePath() 会抛 DirectoryNotFoundException。
        var resDir = _docDir.ObtainRes();
        var bytes = File.ReadAllBytes(filePath);
        resDir.AddRaw(mediaFileName, bytes);

        // 记录多媒体元数据，供 OFDDoc.Flush 时写入 DocumentRes.xml 声明资源。
        _multimedias.Add(new CT_MultiMedia
        {
            ID = new STID(id),
            Type = "Image",
            Format = DeriveFormat(ext),
            // DocumentRes.xml 位于 Doc_0 目录，图像位于 Doc_0/Res/，故相对路径为 Res/xxx
            MediaFile = new STLoc($"{Pkg.Container.DocDir.RES_DIR}/{mediaFileName}")
        });

        _mediaIdMap[key] = mediaId;
        return mediaId;
    }

    /// <summary>分配新的对象ID</summary>
    public long NewId() => _nextId();

    /// <summary>获取所有字体</summary>
    public IReadOnlyList<CT_Font> Fonts => _fonts;

    /// <summary>获取所有已注册的多媒体资源（供 OFDDoc 写出 DocumentRes.xml）</summary>
    public IReadOnlyList<CT_MultiMedia> MultiMedias => _multimedias;

    /// <summary>获取公共数据</summary>
    public CTCommonData CommonData => _commonData;

    /// <summary>由文件扩展名派生 OFD 媒体格式（PNG / JPEG / TIFF ...）</summary>
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
