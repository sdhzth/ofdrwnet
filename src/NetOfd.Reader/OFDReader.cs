using NetOfd.Core.BasicType;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.Resources;
using NetOfd.Pkg.Container;
using NetOfd.Reader.Model;
using System.Xml.Linq;

namespace NetOfd.Reader;

/// <summary>
/// OFD 文件读取器——打开并解析 OFD 文件，提供页面访问和内容提取能力。
/// 实现 IDisposable，使用后必须释放。
/// 对应 Java: org.ofdrw.reader.OFDReader
/// 
/// 用法：
/// <code>
/// using var reader = new OFDReader("path/to/document.ofd");
/// int pageCount = reader.GetPageCount();
/// string text = reader.GetPageText(0);
/// </code>
/// </summary>
public class OFDReader : IDisposable
{
    private readonly OFDDir _ofdDir;
    private DocDir? _docDir;
    private Document? _document;
    private ResourceManage? _resMgt;
    private ResourceLocator? _rl;
    private bool _disposed;

    // ── 构造 ─────────────────────────────────────────────────

    /// <summary>从文件路径打开 OFD</summary>
    public OFDReader(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("OFD 文件不存在", filePath);
        _ofdDir = OFDDir.FromFile(filePath);
        Initialize();
    }

    /// <summary>从 Stream 打开 OFD</summary>
    public OFDReader(Stream stream)
    {
        _ofdDir = OFDDir.FromStream(stream);
        Initialize();
    }

    // ── 初始化 ────────────────────────────────────────────────
    private void Initialize()
    {
        var ofdRoot = _ofdDir.GetOfd()
            ?? throw new BadOFDException("OFD 包中找不到 OFD.xml");

        var docBody = ofdRoot.GetFirstDocBody()
            ?? throw new BadOFDException("OFD.xml 中找不到 DocBody");

        // 加载文档目录：以 DocRoot 的首段为准（兼容 Doc_1 等非 0 编号文档）
        var docRootLoc = docBody.DocRoot?.Value ?? "Doc_0/Document.xml";
        var docDirName = ResolveDocDirName(docRootLoc);
        _docDir = _ofdDir.ObtainDocDir(docDirName);

        // 加载 Document.xml
        _document = _docDir.GetDocument()
            ?? throw new BadOFDException("找不到 Document.xml");

        // 初始化资源定位器与资源管理器
        _rl = new ResourceLocator(_ofdDir, _docDir);
        _resMgt = new ResourceManage(_docDir, _document);
    }

    // ── 基本信息 ─────────────────────────────────────────────

    /// <summary>获取文档页面总数</summary>
    public int GetPageCount()
    {
        return _document?.Pages?.Count ?? 0;
    }

    /// <summary>获取 OFD 根元素</summary>
    public OFDRoot GetOfd() => _ofdDir.GetOfd()!;

    /// <summary>获取 Document 根元素</summary>
    public Document GetDocument() => _document!;

    /// <summary>获取资源管理器</summary>
    public ResourceManage GetResourceManager() => _resMgt!;

    /// <summary>获取资源定位器</summary>
    public ResourceLocator GetResourceLocator() => _rl!;

    /// <summary>获取 DocDir 容器</summary>
    public DocDir GetDocDir() => _docDir!;

    /// <summary>
    /// 获取 OFD 虚拟容器（对应 Java: <c>OFDReader.getOFDDir()</c>）。
    /// <para>通过 <see cref="NetOfd.Pkg.Container.OFDDir"/> 可直接访问
    /// <c>GetDocByIndex(int)</c> / <c>ObtainDocDir</c> / <c>ObtainOfd</c> 等
    /// 包级操作（当前 Reader 以首段 DocBody 初始化，如需切换其他文档目录
    /// 请直接用该容器操作）。</para>
    /// </summary>
    public OFDDir GetOfdDir() => _ofdDir;

    // ── 多文档支持（对齐 Java cdDoc / getDoc / cdDefaultDoc） ──

    /// <summary>
    /// 从 DocRoot 值解析文档目录名（如 "/Doc_1/Document.xml" → "Doc_1"），
    /// 与构造初始化共用同一套兼容逻辑（容忍前后缀与反斜杠）。
    /// </summary>
    private static string ResolveDocDirName(string docRootLoc)
    {
        var normalized = docRootLoc.Trim().TrimStart('/').Replace('\\', '/');
        var slash = normalized.IndexOf('/');
        return slash > 0 ? normalized[..slash] : "Doc_0";
    }

    /// <summary>
    /// 切换 Reader 指向第 <paramref name="docIndex"/> 个 DocBody 的文档（0-based，
    /// 按 OFD.xml 中 DocBody 出现顺序计，非目录名编号）。
    /// <para>对应 Java: <c>OFDReader.cdDoc(int)</c>——切换后文档缓存、资源定位器与
    /// 资源管理器全部指向新文档，后续 GetPage*/GetTemplate/ContentExtractor 等操作
    /// 作用在新文档上。签名相关 API（HasSignature 等）默认查第一个 DocBody，与 Java 同。</para>
    /// </summary>
    /// <exception cref="BadOFDException">DocBody 序号越界或目标 Document.xml 缺失</exception>
    public Document CdDoc(int docIndex)
    {
        var docBodies = _ofdDir.GetOfd()?.GetDocBodies();
        var docBody = docBodies is not null && docIndex >= 0 && docIndex < docBodies.Count
            ? docBodies[docIndex]
            : throw new BadOFDException($"OFD.xml 中找不到第 {docIndex} 个 DocBody");

        var docRootLoc = docBody.DocRoot?.Value ?? "Doc_0/Document.xml";
        var docDirName = ResolveDocDirName(docRootLoc);
        _docDir = _ofdDir.ObtainDocDir(docDirName);
        _document = _docDir.GetDocument()
            ?? throw new BadOFDException($"找不到 {docDirName}/Document.xml");
        _rl = new ResourceLocator(_ofdDir, _docDir);
        _resMgt = new ResourceManage(_docDir, _document);
        return _document;
    }

    /// <summary>
    /// 只读获取第 <paramref name="docIndex"/> 个 DocBody 的文档对象，不切换当前文档。
    /// <para>对应 Java: <c>OFDReader.getDoc(int)</c>（save/restore 语义：临时切换解析后
    /// 恢复原文档状态）。</para>
    /// </summary>
    /// <exception cref="BadOFDException">DocBody 序号越界或目标 Document.xml 缺失</exception>
    public Document GetDoc(int docIndex)
    {
        var oldDocDir = _docDir;
        var oldDocument = _document;
        var oldRl = _rl;
        var oldResMgt = _resMgt;
        try
        {
            return CdDoc(docIndex);
        }
        finally
        {
            _docDir = oldDocDir;
            _document = oldDocument;
            _rl = oldRl;
            _resMgt = oldResMgt;
        }
    }

    /// <summary>
    /// 切换 Reader 回默认（第一个）DocBody 文档。
    /// <para>对应 Java: <c>OFDReader.cdDefaultDoc()</c>，等价 <c>CdDoc(0)</c>。</para>
    /// </summary>
    public Document CdDefaultDoc() => CdDoc(0);

    /// <summary>文档是否包含数字签名</summary>
    public bool HasSignature()
    {
        var docBody = _ofdDir.GetOfd()?.GetFirstDocBody();
        return docBody?.Signatures is not null;
    }

    // ── 签名摘要（对齐 Java getDefaultDocSignaturesPath / getDefaultSignatures） ──

    /// <summary>
    /// 获取签名列表文件（Signatures.xml）的绝对路径。
    /// <para>对应 Java: <c>OFDReader.getDefaultDocSignaturesPath()</c>。
    /// 文档未声明签名时返回 null。</para>
    /// </summary>
    public STLoc? GetDefaultDocSignaturesPath()
    {
        var docBody = _ofdDir.GetOfd()?.GetFirstDocBody();
        var loc = docBody?.Signatures;
        if (loc is null)
            return null;
        // 签名列表声明于 OFD.xml（包根），Java 侧 rl.cd("/") 后 toAbsolutePath：
        // 根路径原样返回；相对路径视为相对包根（非文档目录）。
        return loc.IsRootPath ? loc : new STLoc(STLoc.Resolve("/", loc.Value).Value);
    }

    /// <summary>
    /// 获取签名列表（Signatures.xml）中的摘要条目集合。
    /// <para>对应 Java: <c>OFDReader.getDefaultSignatures().getSignatures()</c> 的等价产物
    /// （Java 返回 Signatures 模型对象，本实现返回其摘要条目 List&lt;SignatureInfo&gt;）。
    /// 签名列表文件缺失/无签名时返回空集合；每项含 ID/Type/BaseLoc/Relative。</para>
    /// </summary>
    public List<SignatureInfo> GetDefaultSignatures()
    {
        var res = new List<SignatureInfo>();
        var sigLoc = GetDefaultDocSignaturesPath();
        if (sigLoc is null || !_rl!.Exist(sigLoc))
            return res;

        var el = _rl.Get(sigLoc);
        if (el is null)
            return res;

        // 宽容：按 LocalName 取 <Signature> 摘要子元素（忽略命名空间前缀差异）
        foreach (var s in el.Elements().Where(e => e.Name.LocalName == "Signature"))
        {
            res.Add(new SignatureInfo
            {
                Id = s.Attribute("ID")?.Value ?? string.Empty,
                Type = s.Attribute("Type")?.Value ?? "Seal",
                BaseLoc = s.Attribute("BaseLoc")?.Value ?? string.Empty,
                Relative = s.Attribute("Relative")?.Value,
            });
        }
        return res;
    }

    // ── 页面访问 ─────────────────────────────────────────────

    /// <summary>获取指定页码（0-based）的 Page 对象</summary>
    public PageObject? GetPage(int pageIndex)
    {
        var pages = _document?.Pages?.GetPages();
        if (pages is null || pageIndex < 0 || pageIndex >= pages.Count)
            return null;

        var pageNode = pages[pageIndex];
        if (pageNode.BaseLoc is null) return null;

        // BaseLoc 指向页面内容文件（如 "Pages/Page_0/Content.xml"），
        // 通过资源定位器解析为真实容器文件
        var el = _rl!.Get(pageNode.BaseLoc);
        return el is null ? null : new PageObject(el);
    }

    /// <summary>获取所有页面</summary>
    public IEnumerable<PageObject?> GetPages()
    {
        int count = GetPageCount();
        for (int i = 0; i < count; i++)
            yield return GetPage(i);
    }

    /// <summary>获取指定页码（1-based）的 Page 对象</summary>
    public PageObject GetPageByNum(int pageNum)
    {
        var pageLoc = GetPageAbsLoc(pageNum);
        var el = _rl!.Get(pageLoc);
        return new PageObject(el ?? throw new BadOFDException($"页面文件不存在: {pageLoc}"));
    }

    // ── 页面信息 ─────────────────────────────────────────────

    /// <summary>
    /// 获取页面信息。
    /// </summary>
    /// <param name="pageNum">页码，从 1 开始</param>
    /// <returns>页面信息</returns>
    /// <exception cref="ArgumentException">页码小于 1 或超过最大页码</exception>
    public PageInfo GetPageInfo(int pageNum)
    {
        if (pageNum <= 0)
            throw new ArgumentException("页码(pageNum)不能小于1", nameof(pageNum));

        int index = pageNum - 1;
        var pages = _document?.Pages;
        var pageList = pages?.GetPages() ?? new List<Page>();
        if (index >= pageList.Count)
            throw new ArgumentException($"{pageNum}超过最大页码:{pageList.Count}", nameof(pageNum));

        // 获取页面的路径
        STLoc? pageLoc = pageList[index].BaseLoc
            ?? throw new BadOFDException($"第 {pageNum} 页缺少 BaseLoc");

        var obj = new PageObject(_rl!.Get(pageLoc)
            ?? throw new BadOFDException($"页面文件不存在: {pageLoc}"));

        // 获取页面的容器绝对路径
        pageLoc = _rl.GetAbsTo(pageLoc);
        STBox pageSize = GetPageSize(obj);

        // 加载模板
        var templatePages = new List<TemplatePageEntity>();
        foreach (var item in obj.GetTemplates())
        {
            if (item.TemplateID is null) continue;
            var template = GetTemplate(item.TemplateID.Value.ToString());
            if (template is null) continue;
            template.SetOrder(item.ZOrder);
            templatePages.Add(template);
        }

        // Page_N 目录中的 N
        int n = index;
        string pageNName = pageLoc.Parent().GetFileName().ToLower();
        if (System.Text.RegularExpressions.Regex.IsMatch(pageNName, @"page_\d+"))
        {
            string numStr = pageNName.Replace("page_", "");
            if (int.TryParse(numStr, out int parsed)) n = parsed;
        }

        return new PageInfo()
            .SetIndex(pageNum)
            .SetId(pageList[index].ID)
            .SetObj(obj)
            .SetSize(pageSize.Clone())
            .SetPageAbsLoc(pageLoc)
            .SetTemplates(templatePages)
            .SetPageN(n);
    }

    /// <summary>获取页面信息集合</summary>
    public List<PageInfo> GetPageList()
    {
        int numberOfPages = GetPageCount();
        var res = new List<PageInfo>(numberOfPages);
        for (int i = 0; i < numberOfPages; i++)
            res.Add(GetPageInfo(i + 1));
        return res;
    }

    /// <summary>
    /// 解析页面模板对象。
    /// </summary>
    /// <param name="id">模板 ID</param>
    /// <returns>模板实体；模板不存在时返回 null</returns>
    public TemplatePageEntity? GetTemplate(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        var commonData = _document?.CommonData;
        if (commonData is null) return null;

        foreach (var item in commonData.GetTemplatePages())
        {
            if (item.ID is null || !id.Equals(item.ID.Value.ToString()))
                continue;
            var loc = item.BaseLoc;
            if (loc is null) break;
            var el = _rl!.Get(loc);
            if (el is null) break;
            return new TemplatePageEntity(item, new PageObject(el));
        }
        return null;
    }

    /// <summary>
    /// 获取制定页码页面文件的在文档中的绝对路径（以 "/" 开头）。
    /// </summary>
    /// <param name="pageNum">页码，从 1 起</param>
    /// <returns>页面文件的绝对路径</returns>
    public STLoc GetPageAbsLoc(int pageNum)
    {
        if (pageNum <= 0)
            throw new ArgumentException("页码(pageNum)不能小于1", nameof(pageNum));

        int index = pageNum - 1;
        var pages = _document?.Pages;
        var pageList = pages?.GetPages() ?? new List<Page>();
        if (index >= pageList.Count)
            throw new ArgumentException($"{pageNum}超过最大页码:{pageList.Count}", nameof(pageNum));

        var pageLoc = pageList[index].BaseLoc
            ?? throw new BadOFDException($"第 {pageNum} 页缺少 BaseLoc");
        return _rl!.GetAbsTo(pageLoc);
    }

    /// <summary>获取页面的对象 ID</summary>
    public STID? GetPageObjectId(int pageNum)
    {
        if (pageNum <= 0)
            throw new ArgumentException("页码(pageNum)不能小于1", nameof(pageNum));

        int index = pageNum - 1;
        var pages = _document?.Pages;
        var pageList = pages?.GetPages() ?? new List<Page>();
        if (index >= pageList.Count)
            throw new ArgumentException($"{pageNum}超过最大页码:{pageList.Count}", nameof(pageNum));

        var page = pageList[index];
        return page.ID;
    }

    /// <summary>
    /// 获取页面区域。
    /// <para>页面存在时优先返回页面自身区域，其次为模板区域（取绘制顺序最大者），
    /// 最后为文档默认区域；找不到时返回 A4 区域。</para>
    /// </summary>
    /// <param name="num">页码，从 1 起；页码不存在时返回文档默认页面区域</param>
    public CT_PageArea GetPageArea(int num)
    {
        var page = GetPageByNumSafe(num);
        return GetPageArea(page);
    }

    /// <summary>
    /// 获取页面物理大小（默认 A4）。
    /// </summary>
    public STBox GetPageSize(int num)
    {
        var page = GetPageByNumSafe(num);
        return GetPageSize(page);
    }

    /// <summary>
    /// 获取页面区域。
    /// <para>页面为 null 时返回文档默认页面区域。</para>
    /// </summary>
    public CT_PageArea GetPageArea(PageObject? page)
    {
        CT_PageArea? res = null;
        if (page is not null)
        {
            if (page.Area is not null)
                return page.Area;

            CT_PageArea? tplArea = null;
            int biggestOrder = -1;
            foreach (var item in page.GetTemplates())
            {
                if (item.TemplateID is null) continue;
                var template = GetTemplate(item.TemplateID.Value.ToString());
                if (template?.Page is null) continue;
                var area = template.Page.Area;
                int order = (int)item.ZOrder;

                if (area is not null && order > biggestOrder && area.PhysicalBox is not null)
                {
                    tplArea = area;
                    biggestOrder = order;
                }
            }
            res = tplArea;
        }

        // 若不存在则通过解析文档中的默认参数获取
        if (res is null)
            res = _document?.CommonData?.PageArea;

        // 无法找到区域时使用 A4，防止后续解析空引用
        if (res is null)
            return new CT_PageArea { PhysicalBox = new STBox(0, 0, 210, 297) };
        return res;
    }

    /// <summary>获取页面物理大小（null 表示文档默认页面大小）</summary>
    public STBox GetPageSize(PageObject? page)
    {
        CT_PageArea? pageArea = GetPageArea(page);
        if (pageArea?.PhysicalBox is null)
            return new STBox(0, 0, 210, 297);
        return pageArea.PhysicalBox;
    }

    // 页码无效时返回 null（用于 GetPageArea/GetPageSize 的容错语义）
    private PageObject? GetPageByNumSafe(int pageNum)
    {
        if (pageNum <= 0) return null;
        int index = pageNum - 1;
        var pageList = _document?.Pages?.GetPages() ?? new List<Page>();
        if (index >= pageList.Count) return null;
        var pageLoc = pageList[index].BaseLoc;
        if (pageLoc is null) return null;
        var el = _rl!.Get(pageLoc);
        return el is null ? null : new PageObject(el);
    }

    // ── 内容提取 ─────────────────────────────────────────────

    /// <summary>提取整个文档的所有文本（按页）</summary>
    public string GetAllText()
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < GetPageCount(); i++)
        {
            sb.Append(GetPageText(i));
            sb.AppendLine();
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>提取指定页面的文本（0-based）</summary>
    public string GetPageText(int pageIndex)
    {
        var page = GetPage(pageIndex);
        if (page is null) return string.Empty;
        return ContentExtractor.ExtractPageText(page);
    }

    // ── 附件 ─────────────────────────────────────────────────

    /// <summary>
    /// 获取所有附件对象（只读）。
    /// </summary>
    /// <returns>附件数组；没有附件时返回空数组</returns>
    public List<NetOfd.Core.Attachment.CTAttachment> GetAttachmentList()
    {
        var res = new List<NetOfd.Core.Attachment.CTAttachment>();
        var attachmentsLoc = _document?.Attachments;
        if (attachmentsLoc is null || !_rl!.Exist(attachmentsLoc))
            return res;

        var el = _rl.Get(attachmentsLoc);
        if (el is null)
            return res;

        var attachments = new NetOfd.Core.Attachment.Attachments(el);
        string parent = attachmentsLoc.Parent().Value;
        foreach (var item in attachments.GetAttachments())
        {
            // 将附件文件路径解析为绝对路径，避免外部读取时工作路径不正确
            if (item.FileLoc is not null)
                item.FileLoc = new STLoc(STLoc.Resolve(parent, item.FileLoc.Value).Value);
            res.Add(item);
        }
        return res;
    }

    /// <summary>按名称获取附件对象；不存在时返回 null</summary>
    public NetOfd.Core.Attachment.CTAttachment? GetAttachment(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return GetAttachmentList().FirstOrDefault(a => a.Name == name);
    }

    /// <summary>
    /// 按名称获取附件文件内容。
    /// </summary>
    /// <param name="name">附件名称</param>
    /// <returns>附件文件字节；附件不存在时返回 null</returns>
    public byte[]? GetAttachmentFile(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var attachment = GetAttachment(name);
        if (attachment?.FileLoc is null) return null;
        return _rl!.GetRaw(attachment.FileLoc);
    }

    // ── 注解（对齐 Java getAnnotations / getAnnotationEntities） ──────

    /// <summary>
    /// 获取注解入口文件对象（Annotations.xml 根元素）。
    /// <para>对应 Java: <c>OFDReader.getAnnotations()</c>。文档无注解时返回 null。</para>
    /// <para>注：Java 版从 OFD 容器根切换工作目录读取；NetOfd 的 ResourceLocator 相对路径
    /// 恒以文档目录（Doc_N）为基准，故无需 cd，直接按 Document.Annotations 定位解析。</para>
    /// </summary>
    public Annotations? GetAnnotations()
    {
        var annLoc = _document?.Annotations;
        if (annLoc is null)
            return null;
        var el = GetXmlFlexible(annLoc);
        return el is null ? null : new Annotations(el);
    }

    /// <summary>
    /// 兼容多种路径写法获取包内 XML：先按原样解析，失败则剥去 <c>Doc_N</c> 前缀重试。
    /// <para>
    /// ST_Loc 相对路径的基准是「所在文件目录」，但真实产物中 Annotations/FileLoc 等
    /// 入口常写成带文档目录前缀的形式（如 <c>Doc_0/Annots/Annotations.xml</c>，
    /// netofd 的 OfdAnnotator 即此写法；Java ofdrw 亦大量如此）。资源定位器以当前
    /// Doc_N 为基准，故此类路径需剥前缀后再解析——读侧容错，兼容两种合法产物。
    /// </para>
    /// </summary>
    private XElement? GetXmlFlexible(STLoc loc)
    {
        if (_rl!.Exist(loc)) return _rl.Get(loc);

        var parts = loc.Parts();
        if (parts.Length > 1 && parts[0].StartsWith("Doc_", StringComparison.OrdinalIgnoreCase))
        {
            var stripped = new STLoc(string.Join('/', parts.Skip(1)));
            if (_rl.Exist(stripped)) return _rl.Get(stripped);
        }
        return null;
    }

    /// <summary>
    /// 获取默认文档所有页的注释实体（尽力而为：单个分页注释文件加载失败时忽略该页）。
    /// <para>对应 Java: <c>OFDReader.getAnnotationEntities()</c>，实体 = (页面ID, 该页 Annot 列表)。
    /// 页面 ID 为字符串（ST_RefID）。注解文件不存在时返回空集合。</para>
    /// </summary>
    public List<AnnotionEntity> GetAnnotationEntities()
    {
        var res = new List<AnnotionEntity>();
        var annLoc = _document?.Annotations;
        if (annLoc is null)
            return res;

        var annEl = GetXmlFlexible(annLoc);
        if (annEl is null)
            return res;

        var annotations = new Annotations(annEl);
        // FileLoc 为相对 Annotations.xml 所在目录的位置（Java cd(annInfosLoc.parent()) 后解析）
        var baseDir = annLoc.Parent();
        foreach (var page in annotations.GetPages())
        {
            var fileLoc = page.FileLoc;
            if (fileLoc is null)
                continue;
            try
            {
                // 将 FileLoc 解析到文档根定位（对齐附件 GetAttachmentList 的 Resolve 手法）；
                // 入口既可能是相对形式（Pages/Page_0/Annotation.xml），也可能是带 Doc_N 前缀的
                // 形式（Doc_0/Annots/Page_1/Annot_1.xml），由 GetXmlFlexible 统一容错。
                var resolved = new STLoc(STLoc.Resolve(baseDir.Value, fileLoc.Value).Value);
                var pageAnnotEl = GetXmlFlexible(resolved) ?? GetXmlFlexible(fileLoc);
                if (pageAnnotEl is null)
                    continue;
                var pageAnnot = new PageAnnot(pageAnnotEl);
                res.Add(new AnnotionEntity(
                    page.PageID?.Value.ToString() ?? string.Empty,
                    pageAnnot.GetAnnots()));
            }
            catch (Exception)
            {
                // 忽略无法加载的注释文件，尽力而为（同 Java）
            }
        }
        return res;
    }

    // ── IDisposable ──────────────────────────────────────────
    public void Dispose()
    {
        if (!_disposed)
        {
            _ofdDir.Dispose();
            _disposed = true;
        }
        GC.SuppressFinalize(this);
    }
}
