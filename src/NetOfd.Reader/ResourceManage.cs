using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicType;
using NetOfd.Core.CompositeObj;
using NetOfd.Core.PageDescription.Color.colorSpace;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.Resources;
using NetOfd.Pkg.Container;
using System.Xml.Linq;

namespace NetOfd.Reader;

/// <summary>
/// 资源管理器（只读），解析 OFD 文档中引用的字体、图像、颜色空间等资源。
/// <para>
/// 资源来源覆盖 GB/T 48666-2026 §7.2 a) 规定的三类资源文件：
/// <list type="bullet">
/// <item>公共资源 —— <c>Doc_N/PublicRes.xml</c>（Document.xml → CommonData/PublicRes）；</item>
/// <item>文档资源 —— <c>Doc_N/DocumentRes.xml</c>（CommonData/DocumentRes）；</item>
/// <item>页面资源 —— <c>Doc_N/Pages/Page_M/PageRes.xml</c>（Page_M/Content.xml → PageRes）。</item>
/// </list>
/// 前两类由 CommonData 登记，第三类由各页 Content.xml 登记 —— 三者均会被解析并汇入同一资源池。
/// </para>
/// <para>
/// 对应 Java: org.ofdrw.reader.ResourceManage。Java 版仅加载前两类
/// （源码注释「页面资源，暂时忽略」），本页级资源支持为 .NET 侧补齐；
/// 路径解析语义（BaseLoc 规则）与 Java 的 <c>abs()</c> 及 ofdgo 的
/// <c>resolveResourcePath</c> 保持一致。
/// </para>
/// </summary>
public class ResourceManage
{
    private readonly DocDir _docDir;
    private readonly Document _document;

    private Dictionary<long, CT_Font>? _fontMap;
    private Dictionary<long, CT_MultiMedia>? _multiMediaMap;
    private Dictionary<long, CT_ColorSpace>? _colorSpaceMap;
    private Dictionary<long, CT_DrawParam>? _drawParamMap;
    private Dictionary<long, CT_VectorG>? _compositeMap;

    /// <summary>已枚举的资源文件清单（惰性构建，一次）</summary>
    private List<ResFile>? _resFiles;

    /// <summary>
    /// 多媒体 ID → 媒体文件的包内绝对路径。
    /// <para>键为 <see cref="CT_MultiMedia.ID"/>，值为按 BaseLoc 规则解析出的路径；
    /// Primary 为「资源文件目录/BaseLoc/MediaFile」，Fallback 为「资源文件目录/MediaFile」
    /// （BaseLoc 目录缺失时的宽容回退）。</para>
    /// </summary>
    private readonly Dictionary<long, (string Primary, string Fallback)> _mediaPaths = new();

    public ResourceManage(DocDir docDir, Document document)
    {
        _docDir = docDir;
        _document = document;
    }

    // ── 资源文件枚举 ─────────────────────────────────────────

    /// <summary>
    /// 已加载的资源文件：资源对象 + 所在目录 + BaseLoc（通用存储路径）。
    /// </summary>
    private sealed class ResFile
    {
        public ResFile(Res res, string dirPath, string? baseLoc)
        {
            Res = res;
            DirPath = dirPath;
            BaseLoc = baseLoc;
        }

        /// <summary>资源文件解析结果</summary>
        public Res Res { get; }

        /// <summary>资源文件所在目录（包内绝对路径，以 "/" 开头）</summary>
        public string DirPath { get; }

        /// <summary>Res/@BaseLoc（通用存储路径，可为空）</summary>
        public string? BaseLoc { get; }
    }

    /// <summary>
    /// 枚举文档中声明的全部资源文件：公共资源 + 文档资源 + 页面资源。
    /// </summary>
    private List<ResFile> ResFiles => _resFiles ??= EnumerateResFiles();

    private List<ResFile> EnumerateResFiles()
    {
        var result = new List<ResFile>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ① 公共资源 / 文档资源（登记在 Document.xml 的 CommonData 中）
        var commonData = _document.CommonData;
        if (commonData is not null)
        {
            foreach (var loc in commonData.PublicResList)
                AddResFile(result, seen, DocPkgPath, loc.Value);
            foreach (var loc in commonData.DocumentResList)
                AddResFile(result, seen, DocPkgPath, loc.Value);
        }

        // ② 页面资源（登记在 Page_M/Content.xml 的 <ofd:PageRes> 中，路径相对该页目录）
        var pages = _document.Pages?.GetPages();
        if (pages is not null)
        {
            foreach (var page in pages)
            {
                if (page.BaseLoc is null) continue;

                string contentPath = CombinePkg(DocPkgPath, page.BaseLoc.Value, DocPkgName);
                var (container, fileName) = ResolveInPackage(contentPath);
                if (container is null) continue;

                XElement? contentEl;
                try { contentEl = container.GetFile(fileName); }
                catch { continue; }
                if (contentEl is null) continue;

                PageObject pageObj;
                try { pageObj = new PageObject(contentEl); }
                catch { continue; }

                string pageDir = DirOfPath(contentPath);
                foreach (var resLoc in pageObj.PageResList)
                    AddResFile(result, seen, pageDir, resLoc.Value);
            }
        }

        return result;
    }

    /// <summary>
    /// 加载一个资源文件并加入清单（失败静默跳过，不影响其它资源）。
    /// </summary>
    /// <param name="result">资源文件清单（追加）</param>
    /// <param name="seen">已处理路径集合（去重）</param>
    /// <param name="baseAbs">基准目录（包内绝对路径）</param>
    /// <param name="loc">资源文件声明路径（相对基准目录，或以 "/" 开头的包内绝对路径）</param>
    private void AddResFile(List<ResFile> result, HashSet<string> seen, string baseAbs, string loc)
    {
        if (string.IsNullOrWhiteSpace(loc)) return;

        // 候选 1：按基准目录拼接的标准解；候选 2：仅文件名（兼容「Doc_N/xxx.xml」等非规范写法）
        var candidates = new List<string> { CombinePkg(baseAbs, loc, DocPkgName) };
        string fileNameOnly = Path.GetFileName(loc.Replace('\\', '/'));
        if (fileNameOnly.Length > 0)
        {
            string alt = CombinePkg(baseAbs, fileNameOnly, DocPkgName);
            if (!candidates.Contains(alt, StringComparer.OrdinalIgnoreCase)) candidates.Add(alt);
        }

        foreach (var path in candidates)
        {
            if (!seen.Add(path)) continue;

            var (container, name) = ResolveInPackage(path);
            if (container is null) continue;

            XElement? el;
            try { el = container.GetFile(name); }
            catch { continue; }
            if (el is null) continue;

            try
            {
                var res = new Res(el);
                // 所在目录取「声明的基准目录」——而非解析后的路径，
                // 以便资源内的相对文件路径（MediaFile/FontFile）按声明基准推算。
                result.Add(new ResFile(res, baseAbs, res.BaseLoc?.Value));
            }
            catch
            {
                // 资源文件内容非法时跳过该文件（宽容策略，同 Java loadResFile）
            }
            break;   // 命中即止
        }
    }

    // ── 字体资源 ─────────────────────────────────────────────

    /// <summary>获取所有字体（ID → CT_Font）</summary>
    public Dictionary<long, CT_Font> GetFontMap()
    {
        if (_fontMap is not null) return _fontMap;

        _fontMap = new Dictionary<long, CT_Font>();
        foreach (var resFile in ResFiles)
        {
            foreach (var font in resFile.Res.GetFonts())
            {
                _fontMap[font.ID.Value] = font;
            }
        }

        return _fontMap;
    }

    /// <summary>按 ID 获取字体</summary>
    public CT_Font? GetFont(long id)
    {
        GetFontMap().TryGetValue(id, out var font);
        return font;
    }

    // ── 图像资源 ─────────────────────────────────────────────

    /// <summary>获取所有多媒体资源（ID → CT_MultiMedia）</summary>
    public Dictionary<long, CT_MultiMedia> GetMultiMediaMap()
    {
        if (_multiMediaMap is not null) return _multiMediaMap;

        _multiMediaMap = new Dictionary<long, CT_MultiMedia>();
        foreach (var resFile in ResFiles)
        {
            foreach (var mm in resFile.Res.GetMultiMedias())
            {
                _multiMediaMap[mm.ID.Value] = mm;

                // 记录媒体文件的解析路径（供 GetImageData 取字节）
                if (mm.MediaFile is not null)
                {
                    string primary = ResolveResFilePath(resFile.DirPath, resFile.BaseLoc, mm.MediaFile.Value);
                    string fallback = ResolveResFilePath(resFile.DirPath, null, mm.MediaFile.Value);
                    if (primary.Length > 0)
                        _mediaPaths[mm.ID.Value] = (primary, fallback);
                }
            }
        }

        return _multiMediaMap;
    }

    /// <summary>按 ID 获取多媒体资源</summary>
    public CT_MultiMedia? GetMultiMedia(long id)
    {
        GetMultiMediaMap().TryGetValue(id, out var mm);
        return mm;
    }

    /// <summary>
    /// 获取媒体文件在包内的绝对路径（已按 Res/@BaseLoc 规则解析）。
    /// <para>资源未登记或未声明 MediaFile 时返回 null。</para>
    /// </summary>
    public string? GetMediaFilePath(long id)
    {
        GetMultiMediaMap();
        return _mediaPaths.TryGetValue(id, out var paths) ? paths.Primary : null;
    }

    /// <summary>获取图像资源的二进制数据</summary>
    public byte[]? GetImageData(CT_MultiMedia mm)
    {
        if (mm.MediaFile is null) return null;

        // ① 按解析出的包内绝对路径取字节
        //    （支持页级 Page_M/Res/ 资源，以及文档级 BaseLoc="Res" 组合）
        GetMultiMediaMap();
        if (_mediaPaths.TryGetValue(mm.ID.Value, out var paths))
        {
            var bytes = GetRawByPath(paths.Primary);
            if (bytes is not null) return bytes;

            // BaseLoc 指向的目录缺失时回退到「资源文件目录/MediaFile」（宽容策略）
            if (!string.Equals(paths.Fallback, paths.Primary, StringComparison.Ordinal))
            {
                bytes = GetRawByPath(paths.Fallback);
                if (bytes is not null) return bytes;
            }
        }

        // ② 回退：文档级 Res 目录（保持历史行为）
        var resDir = _docDir.ObtainRes();
        return resDir.GetResource(mm.MediaFile.GetFileName());
    }

    // ── 颜色空间资源 ─────────────────────────────────────────

    /// <summary>获取所有颜色空间（ID → CT_ColorSpace）</summary>
    public Dictionary<long, CT_ColorSpace> GetColorSpaceMap()
    {
        if (_colorSpaceMap is not null) return _colorSpaceMap;

        _colorSpaceMap = new Dictionary<long, CT_ColorSpace>();
        foreach (var resFile in ResFiles)
        {
            foreach (var cs in resFile.Res.GetColorSpaces())
            {
                if (cs.ID is not null)
                    _colorSpaceMap[(long)cs.ID] = cs;
            }
        }

        return _colorSpaceMap;
    }

    /// <summary>
    /// 按 ID 获取颜色空间；ID 为 null 时返回 null（调用方按默认 RGB 处理）。
    /// </summary>
    public CT_ColorSpace? GetColorSpace(STRefID? id)
    {
        if (id is null) return null;
        GetColorSpaceMap().TryGetValue((long)id, out var cs);
        return cs;
    }

    // ── 绘制参数资源（DrawParam：透明度 / 可见性 / 混合模式）──

    /// <summary>获取所有绘制参数（ID → CT_DrawParam）</summary>
    public Dictionary<long, CT_DrawParam> GetDrawParamMap()
    {
        if (_drawParamMap is not null) return _drawParamMap;

        _drawParamMap = new Dictionary<long, CT_DrawParam>();
        foreach (var resFile in ResFiles)
        {
            foreach (var dp in resFile.Res.GetDrawParams())
            {
                if (long.TryParse(dp.Id, out var id))
                    _drawParamMap[id] = dp;
            }
        }

        return _drawParamMap;
    }

    /// <summary>
    /// 按 ID 获取绘制参数；ID 为 null 时返回 null。
    /// </summary>
    public CT_DrawParam? GetDrawParam(STRefID? id)
    {
        if (id is null) return null;
        GetDrawParamMap().TryGetValue((long)id, out var dp);
        return dp;
    }

    /// <summary>
    /// 按 ID 解析绘制参数并沿 ParentID 链合并继承属性（GB/T 33190 §8.1.2）。
    /// 返回的合并结果不写入资源池。
    /// </summary>
    public CT_DrawParam? GetDrawParamResolved(STRefID? id)
        => DrawParamResolver.Resolve(GetDrawParamMap().Values, id);

    // ── 矢量图像资源（CompositeGraphicUnit：复合对象引用）─────

    /// <summary>获取所有矢量图像资源（ID → CT_VectorG）</summary>
    public Dictionary<long, CT_VectorG> GetCompositeGraphicUnitMap()
    {
        if (_compositeMap is not null) return _compositeMap;

        _compositeMap = new Dictionary<long, CT_VectorG>();
        foreach (var resFile in ResFiles)
        {
            foreach (var vg in resFile.Res.GetCompositeGraphicUnits())
            {
                if (vg.ID is not null)
                    _compositeMap[(long)vg.ID] = vg;
            }
        }

        return _compositeMap;
    }

    /// <summary>
    /// 按 ID 获取矢量图像资源（复合对象 <c>CompositeObject</c> 经 <c>ResourceID</c> 引用）；
    /// ID 为 null 或不存在时返回 null。
    /// </summary>
    public CT_VectorG? GetCompositeGraphicUnit(STRefID? id)
    {
        if (id is null) return null;
        GetCompositeGraphicUnitMap().TryGetValue((long)id, out var vg);
        return vg;
    }

    // ── 包内路径工具 ─────────────────────────────────────────

    /// <summary>
    /// 文档目录在包内的绝对路径（如 <c>/Doc_0</c>）；
    /// 未挂载于包根时为空串（此时所有路径以文档目录为基准）。
    /// </summary>
    private string DocPkgPath
    {
        get
        {
            if (_docDir.Parent is null) return string.Empty;
            string abs = _docDir.GetAbsolutePath();     // 形如 "/ROOT/Doc_0"
            const string rootPrefix = "/ROOT";
            return abs.StartsWith(rootPrefix, StringComparison.Ordinal)
                ? abs[rootPrefix.Length..]
                : abs;
        }
    }

    /// <summary>文档目录名（如 <c>Doc_0</c>）；未挂载于包根时为空串。</summary>
    private string DocPkgName
    {
        get
        {
            string p = DocPkgPath.TrimEnd('/');
            int idx = p.LastIndexOf('/');
            return idx < 0 ? p : p[(idx + 1)..];
        }
    }

    /// <summary>规范化包内路径（消除 "." 与 ".."，统一以 "/" 开头）。</summary>
    private static string NormalizeAbs(string path)
    {
        var parts = new List<string>();
        foreach (var seg in path.Replace('\\', '/').Split('/'))
        {
            if (seg.Length == 0 || seg == ".") continue;
            if (seg == "..")
            {
                if (parts.Count > 0) parts.RemoveAt(parts.Count - 1);
                continue;
            }
            parts.Add(seg);
        }
        return "/" + string.Join("/", parts);
    }

    /// <summary>
    /// 以基准目录拼接包内绝对路径：
    /// <list type="bullet">
    /// <item>loc 以 "/" 开头 → 视为包内绝对路径，直接采用；</item>
    /// <item>loc 以文档目录名开头（如 <c>Doc_0/PublicRes.xml</c>）→ 视为包根相对路径，
    ///       避免与基准目录拼接后出现 <c>Doc_0/Doc_0/…</c> 前缀重复；</item>
    /// <item>其余 → 相对基准目录拼接。</item>
    /// </list>
    /// </summary>
    private static string CombinePkg(string baseAbs, string loc, string docName)
    {
        string p = (loc ?? string.Empty).Trim().Replace('\\', '/');
        if (p.Length == 0) return NormalizeAbs(baseAbs);
        if (p.StartsWith('/')) return NormalizeAbs(p);

        if (docName.Length > 0 &&
            (p.Equals(docName, StringComparison.OrdinalIgnoreCase) ||
             p.StartsWith(docName + "/", StringComparison.OrdinalIgnoreCase)))
        {
            return NormalizeAbs("/" + p);
        }

        return NormalizeAbs(baseAbs.TrimEnd('/') + "/" + p);
    }

    /// <summary>取路径所在目录（包内绝对路径，以 "/" 结尾或为 "/"）。</summary>
    private static string DirOfPath(string path)
    {
        string p = path.Replace('\\', '/').TrimEnd('/');
        int idx = p.LastIndexOf('/');
        return idx < 0 ? "/" : p[..(idx + 1)];
    }

    /// <summary>
    /// 解析资源文件内声明的文件路径（GB/T 33190-2016 §7.9 BaseLoc 规则）：
    /// <list type="bullet">
    /// <item>声明为绝对路径（以 "/" 开头）→ 直接采用；</item>
    /// <item>BaseLoc 为绝对路径 → 以 BaseLoc 为基准；</item>
    /// <item>BaseLoc 为相对路径 → 以「资源文件目录/BaseLoc」为基准；</item>
    /// <item>无 BaseLoc → 以资源文件所在目录为基准。</item>
    /// </list>
    /// 对齐 Java ofdrw <c>ResourceManage#abs</c> 与 ofdgo <c>resolveResourcePath</c>。
    /// </summary>
    private static string ResolveResFilePath(string resDir, string? baseLoc, string loc)
    {
        string p = (loc ?? string.Empty).Trim().Replace('\\', '/');
        if (p.Length == 0) return string.Empty;
        if (p.StartsWith('/')) return NormalizeAbs(p);

        string dir = resDir;
        if (!string.IsNullOrWhiteSpace(baseLoc))
        {
            string b = baseLoc.Trim().Replace('\\', '/');
            dir = b.StartsWith('/')
                ? NormalizeAbs(b)
                : NormalizeAbs(dir.TrimEnd('/') + "/" + b);
        }
        return NormalizeAbs(dir.TrimEnd('/') + "/" + p);
    }

    /// <summary>
    /// 将包内绝对路径解析为「容器 + 文件名」。
    /// <para>挂载于包根时以包根为基准，否则以文档目录为基准。</para>
    /// </summary>
    private (VirtualContainer? Container, string FileName) ResolveInPackage(string absPath)
    {
        var parts = absPath.TrimStart('/').Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return (null, string.Empty);

        VirtualContainer current = _docDir.Parent ?? _docDir;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var child = current.GetContainer(parts[i]);
            if (child is null) return (null, string.Empty);
            current = child;
        }
        return (current, parts[^1]);
    }

    /// <summary>按包内绝对路径读取二进制内容（图像等）。</summary>
    private byte[]? GetRawByPath(string absPath)
    {
        var (container, fileName) = ResolveInPackage(absPath);
        return container?.GetRaw(fileName);
    }
}
