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
/// 媒体资源落位策略（GB/T 48666-2026 §7.2 a）。
/// <para>
/// 标准原文：「被文档页面内容使用的字型、绘制参数和在多个页面中共同使用的资源<b>应</b>在
/// 文档资源列表（DocumentRes.xml）文件中注册，仅在一个页面内使用的资源<b>一般</b>在页面
/// 资源列表（Page_N/PageRes.xml）文件中注册。」
/// —— 前半句为强制条款，后半句为推荐性条款。
/// </para>
/// </summary>
public enum MediaPlacement
{
    /// <summary>
    /// 自动（默认）：仅被单个页面引用的资源下沉到 <c>Page_N/PageRes.xml</c> + <c>Page_N/Res/</c>；
    /// 多页共用、或引用范围无法判定的资源登记到文档级 <c>DocumentRes.xml</c> + <c>Doc_0/Res/</c>。
    /// </summary>
    Auto = 0,

    /// <summary>
    /// 全部登记到文档级 <c>DocumentRes.xml</c>（Java ofdrw 生成侧基线行为，
    /// <c>ResManager#addMultiMedia</c> 一律 <c>docRes()</c>）。
    /// 需要与旧产物逐字节一致、或下游阅读器不支持页级资源时使用。
    /// </summary>
    DocumentOnly = 1,
}

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

    /// <summary>
    /// 媒体资源落位策略。默认 <see cref="MediaPlacement.Auto"/>：仅单页引用的资源
    /// 下沉到 <c>Page_N/PageRes.xml</c>（GB/T 48666-2026 §7.2 a 推荐性条款）。
    /// 置为 <see cref="MediaPlacement.DocumentOnly"/> 可回退到 Java ofdrw 基线行为。
    /// </summary>
    public MediaPlacement Placement { get; set; } = MediaPlacement.Auto;

    /// <summary>
    /// 当前渲染页的绝对 loc（形如 <c>/Doc_0/Pages/Page_2/Content.xml</c>），由
    /// <c>OFDDoc</c> 在建页/渲染页时同步写入。为 <c>null</c> 表示当前不处于页面
    /// 渲染上下文（如仅创建绘制上下文），此时注册的资源一律按文档级处理。
    /// </summary>
    public string? CurrentPageLoc { get; set; }

    /// <summary>
    /// 媒体资源暂存条目：字节与引用页登记在册，落盘位置延后到
    /// <see cref="PlaceMediaResources"/> 时按引用范围决定（文档级 / 页级）。
    /// </summary>
    private sealed class MediaEntry
    {
        public required CT_MultiMedia Media;
        public required string FileName;
        public required byte[] Bytes;

        /// <summary>引用过该资源的页面 loc 集合；多于 1 个即判定为多页共用。</summary>
        public readonly HashSet<string> PageLocs = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>曾在页面上下文之外被引用（引用范围不可判定）→ 强制文档级。</summary>
        public bool UnknownPage;

        /// <summary>已完成落盘（Flush 可能多次触发，避免重复登记）。</summary>
        public bool Placed;
    }

    private readonly Dictionary<string, MediaEntry> _mediaEntries = new(StringComparer.Ordinal);

    /// <summary>
    /// 资源文件序号分配器（按资源类型前缀分桶）。
    /// <para>
    /// GB/T 48666-2026 表 1：Res 下的资源文件命名为 <c>Font_M.ttf</c> / <c>Image_M.png</c> /
    /// <c>Audio_M.mp3</c> / <c>Video_M.mp4</c> / <c>VectorG.xml</c>，其中 M 为<b>资源序号</b>。
    /// </para>
    /// <para>
    /// ⚠️ M 须按<b>同类型内连续编号</b>，不可直接复用全局对象 ID（MaxUnitID）：
    /// 全局 ID 会被图元等其它对象占用而出现跳号（如仅一张图却得到 <c>Image_8.png</c>），
    /// 与标准「资源序号」语义不符。
    /// </para>
    /// <para>
    /// 注意：此序号仅用于<b>资源文件名</b>；资源在 XML 中的 <c>ID</c>（ST_ID）仍取全局
    /// 对象 ID 分配器，二者互不影响——文件名不需要与 ST_ID 数值相同。
    /// </para>
    /// </summary>
    private readonly Dictionary<string, int> _resSeq = new();

    /// <summary>分配下一个资源序号（按前缀分桶，从 1 开始）。</summary>
    private int NextResSeq(string prefix)
    {
        _resSeq.TryGetValue(prefix, out int cur);
        cur++;
        _resSeq[prefix] = cur;
        return cur;
    }

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
        // 字型资源按 GB/T 48666-2026 表 1 规范命名：Font_M.ttf
        // （原文：Res 下的字型、图像、复合图元、音频、视频、三维模型等资源文件）
        // M 为字型资源序号（按类型连续编号），不取全局对象 ID（后者会跳号）。
        var fontFileName = $"Font_{NextResSeq("Font")}{ext}";

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
        if (_mediaIdMap.TryGetValue(key, out var existingId))
        {
            // 同一文件被再次引用：登记引用页。
            // 若出现在 ≥2 个页面（或有一次在页面上下文之外），则判定为「多页共用」
            // 而留在文档级 —— 这是 §7.2 a) 的强制条款，优先于页级下沉的推荐性条款。
            if (_mediaEntries.TryGetValue(existingId, out var dup))
            {
                if (CurrentPageLoc is null) dup.UnknownPage = true;
                else dup.PageLocs.Add(CurrentPageLoc);
            }
            return existingId;
        }

        // 复用全局 ID 分配器，确保 CT_MultiMedia.ID 与 ImageObject.ResourceID
        // 均为同一数字 ST_ID（与字体注册方式一致），避免引用错位。
        var id = _nextId();
        var mediaId = $"{id}";
        var ext = Path.GetExtension(filePath).ToLowerInvariant();
        // 资源文件按 GB/T 48666-2026 表 1 规范命名：Image_M.png / Audio_M.mp3 / Video_M.mp4
        // （原文：Res 下的字型、图像、复合图元、音频、视频、三维模型等资源文件）
        // M 为该类型的资源序号（按类型连续编号，从 1 开始），
        // 不取全局对象 ID——全局 ID 会被图元等其它对象占用而跳号，
        // 与标准「资源序号」语义不符（如仅一张图却得 Image_8.png）。
        var prefix = MediaFilePrefix(ext);
        var fileName = $"{prefix}{NextResSeq(prefix)}{ext}";

        // 注意：OFDDir 在内存模式下没有物理路径，必须读取字节后写入 ResDir 的
        // 二进制缓存（RawFiles），由 WriteToZip 一并打包进 zip。
        // 早期实现用 File.Copy 到 GetAbsolutePath() 会抛 DirectoryNotFoundException。
        //
        // ⚠️ 此处【不再立即写 Doc_0/Res】：字节暂存于 MediaEntry，落盘位置延后到
        // PlaceMediaResources 时按「引用范围」决定 —— 仅单页引用的下沉到
        // Page_N/Res + Page_N/PageRes.xml（GB/T 48666-2026 §7.2 a）。
        var bytes = File.ReadAllBytes(filePath);

        var mm = new CT_MultiMedia
        {
            ID = new STID(id),
            Type = "Image",
            Format = DeriveFormat(ext)
            // MediaFile 在 PlaceMediaResources 落盘时按最终位置填写
        };

        var entry = new MediaEntry { Media = mm, FileName = fileName, Bytes = bytes };
        if (CurrentPageLoc is null) entry.UnknownPage = true;
        else entry.PageLocs.Add(CurrentPageLoc);

        _multimedias.Add(mm);
        _mediaEntries[mediaId] = entry;
        _mediaIdMap[key] = mediaId;
        return mediaId;
    }

    /// <summary>
    /// 把 <see cref="AddMedia"/> 暂存的媒体资源按引用范围落盘并登记资源声明。
    /// <list type="bullet">
    ///   <item><description>仅被单个页面引用 → <c>Page_N/Res/{file}</c> + <c>Page_N/PageRes.xml</c>，
    ///   并在 <c>Page_N/Content.xml</c> 写入 <c>&lt;ofd:PageRes&gt;PageRes.xml&lt;/ofd:PageRes&gt;</c>
    ///   （GB/T 48666-2026 §7.2 a 推荐性条款、表 1）。</description></item>
    ///   <item><description>多页共用 / 引用范围未知 / <see cref="Placement"/> 为
    ///   <see cref="MediaPlacement.DocumentOnly"/> → <c>Doc_0/Res/{file}</c> + <c>DocumentRes.xml</c>
    ///   （§7.2 a 强制条款）。</description></item>
    /// </list>
    /// <para>
    /// MediaFile 一律写成 <c>Res/{file}</c> 的相对形式：OFD 规定相对路径以「声明该资源的
    /// 资源文件所在目录」为基准（Java ofdrw <c>ResourceManage#abs</c>、ofdgo
    /// <c>resolveResourcePath</c> 同语义），故文档级解析为 <c>Doc_0/Res/{file}</c>、
    /// 页级解析为 <c>Page_N/Res/{file}</c>，两种层级写法完全同构。
    /// </para>
    /// </summary>
    /// <param name="docDir">文档目录容器。</param>
    public void PlaceMediaResources(Pkg.Container.DocDir docDir)
    {
        if (_mediaEntries.Count == 0) return;

        bool needDocumentRes = false;

        foreach (var entry in _mediaEntries.Values)
        {
            if (entry.Placed) continue;

            var mm = entry.Media;
            mm.MediaFile = new STLoc($"{Pkg.Container.DocDir.RES_DIR}/{entry.FileName}");

            var pageDir = Placement == MediaPlacement.Auto
                ? ResolveSingleOwnerPageDir(docDir, entry)
                : null;

            if (pageDir is not null)
            {
                // ── 页级：Page_N/Res + Page_N/PageRes.xml ──
                pageDir.ObtainRes().AddResource(entry.FileName, entry.Bytes);

                var pageRes = pageDir.ObtainPageRes();
                bool already = pageRes.GetMultiMedias().Any(m => m.ID?.Value == mm.ID?.Value);
                if (!already) pageRes.AddMultiMedia(mm);
                // PutFile 持有的是同一 XElement 引用，ObtainPageRes 已挂接；此处无需回写。

                // 在 Content.xml 的 <Page> 中声明 <ofd:PageRes>（保序插到 <Content> 之前）
                var pageObj = pageDir.ObtainContent();
                pageObj.AddPageRes(new STLoc(Pkg.Container.PageDir.PAGE_RES_FILE));
                pageDir.SetContent(pageObj);
            }
            else
            {
                // ── 文档级：Doc_0/Res + DocumentRes.xml ──
                docDir.ObtainRes().AddResource(entry.FileName, entry.Bytes);
                needDocumentRes = true;
            }

            entry.Placed = true;
            if (pageDir is null) _pendingDocumentRes.Add(mm);
        }

        if (needDocumentRes)
        {
            var documentRes = docDir.ObtainDocumentRes();
            foreach (var mm in _pendingDocumentRes)
            {
                // 去重：已声明过同 ID 的跳过（编辑模式下 DocumentRes 可能已有该资源）
                bool exists = documentRes.GetMultiMedias().Any(m => m.ID?.Value == mm.ID?.Value);
                if (!exists) documentRes.AddMultiMedia(mm);
            }
            _pendingDocumentRes.Clear();
        }
    }

    /// <summary>等待登记到 DocumentRes.xml 的媒体（按注册顺序）。</summary>
    private readonly List<CT_MultiMedia> _pendingDocumentRes = new();

    /// <summary>
    /// 判定资源是否「仅被单个页面引用」并解析出该页目录；否则返回 null（走文档级）。
    /// </summary>
    private static Pkg.Container.PageDir? ResolveSingleOwnerPageDir(
        Pkg.Container.DocDir docDir, MediaEntry entry)
    {
        // 引用范围未知（曾在页面上下文之外注册）→ 不冒险下沉
        if (entry.UnknownPage) return null;
        if (entry.PageLocs.Count != 1) return null;

        var loc = entry.PageLocs.First();
        if (string.IsNullOrEmpty(loc)) return null;

        // loc 形如 "/Doc_0/Pages/Page_2/Content.xml"：取 "Pages" 之后的一段为页目录名。
        // 模板页渲染期 loc 为 "/Doc_0/Tpl_0/Content.xml"，不含 "Pages" → 返回 null（文档级）。
        var parts = loc.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        int idx = Array.FindIndex(parts, p => p.Equals("Pages", StringComparison.OrdinalIgnoreCase));
        if (idx < 0 || idx + 1 >= parts.Length) return null;

        var dirName = parts[idx + 1];
        if (!dirName.StartsWith(Pkg.Container.PageDir.PageContainerPrefix, StringComparison.OrdinalIgnoreCase))
            return null;

        return docDir.ObtainPages().ObtainPageDirByName(dirName);
    }

    /// <summary>
    /// 由扩展名推导资源文件名前缀（GB/T 48666-2026 表 1 规范命名）。
    /// </summary>
    private static string MediaFilePrefix(string ext) => ext.ToLowerInvariant() switch
    {
        ".png" => "Image_",
        ".jpg" or ".jpeg" => "Image_",
        ".bmp" => "Image_",
        ".tif" or ".tiff" => "Image_",
        ".mp3" => "Audio_",
        ".mp4" => "Video_",
        ".xml" => "VectorG_",
        _ => "Res_",
    };

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
