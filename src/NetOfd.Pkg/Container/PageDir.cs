using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.Resources;

namespace NetOfd.Pkg.Container;

/// <summary>
/// Pages 目录容器，包含所有 Page_N 子目录。
/// 对应 Java: org.ofdrw.pkg.container.PagesDir
/// </summary>
public class PagesDir : VirtualContainer
{
    public PagesDir(string name = "Pages", string? physicalPath = null) : base(name, physicalPath) { }

    /// <summary>获取或创建第 index 个页面目录（0-based）</summary>
    public PageDir ObtainPageDir(int index) => ObtainPageDirByName(PageDir.PageContainerPrefix + index);

    /// <summary>
    /// 按目录名获取或创建页面目录（如 <c>Page_0</c>）。
    /// <para>
    /// 页面在文档中的先后顺序由 Document.xml 的 &lt;Page&gt; 列表决定，
    /// 与物理目录 <c>Page_N</c> 的编号未必一致（虚拟页面可插入到指定页码），
    /// 故需要按目录名（由 BaseLoc 解析而来）定位物理目录。
    /// </para>
    /// </summary>
    /// <param name="dirName">页面目录名</param>
    public PageDir ObtainPageDirByName(string dirName)
    {
        if (DirCache.TryGetValue(dirName, out var existing) && existing is PageDir pd)
            return pd;

        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, dirName) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var pageDir = new PageDir(dirName, childPhys);
        pageDir.Parent = this;
        DirCache[dirName] = pageDir;
        return pageDir;
    }

    /// <summary>获取已创建的页面目录数量</summary>
    public int Count
    {
        get
        {
            int count = DirCache.Count;
            if (PhysicalPath is not null)
            {
                int physCount = Directory.GetDirectories(PhysicalPath).Length;
                return Math.Max(count, physCount);
            }
            return count;
        }
    }

    /// <summary>获取页数（兼容接口）</summary>
    public int GetPagesCount() => Count;

    /// <summary>创建并获取新页面目录</summary>
    public PageDir ObtainNewPage() => ObtainPageDir(Count);

    /// <summary>获取最后一个页面目录</summary>
    public PageDir? GetLatestPage() => Count > 0 ? ObtainPageDir(Count - 1) : null;
}

/// <summary>
/// Page_N 目录容器，包含 Content.xml 和页面资源。
/// 对应 Java: org.ofdrw.pkg.container.PageDir
/// </summary>
public class PageDir : VirtualContainer
{
    /// <summary>页面容器名称前缀（目录名形如 <c>Page_0</c>）</summary>
    public const string PageContainerPrefix = "Page_";

    public const string CONTENT_FILE = "Content.xml";

    /// <summary>页面资源列表文件名（GB/T 48666-2026 表 1：PageRes.xml）。</summary>
    public const string PAGE_RES_FILE = "PageRes.xml";

    /// <summary>页面级资源目录名（<c>Page_N/Res</c>）。</summary>
    public const string RES_DIR = "Res";

    public PageDir(string name, string? physicalPath = null) : base(name, physicalPath) { }

    // ────────────────────────────────────────────────────────
    //  PageRes.xml（页面资源列表）
    // ────────────────────────────────────────────────────────
    /// <summary>写入页面资源列表文件。</summary>
    public void SetPageRes(Res res) => PutFile(PAGE_RES_FILE, res.Element);

    /// <summary>读取页面资源列表文件；不存在返回 null。</summary>
    public Res? GetPageRes()
    {
        var el = GetFile(PAGE_RES_FILE);
        return el is null ? null : new Res(el);
    }

    /// <summary>获取或创建页面资源列表文件。</summary>
    public Res ObtainPageRes()
    {
        var res = GetPageRes();
        if (res is not null) return res;
        res = new Res();
        SetPageRes(res);
        return res;
    }

    /// <summary>
    /// 获取或创建 <c>Page_N/Res</c> 子目录（存放仅本页使用的图像等资源文件）。
    /// 对应 GB/T 48666-2026 表 1「Res 资源文件夹，可分为文档、页面等不同层级」。
    /// </summary>
    public ResDir ObtainRes()
    {
        if (DirCache.TryGetValue(RES_DIR, out var existing) && existing is ResDir rd)
            return rd;

        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, RES_DIR) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var resDir = new ResDir(RES_DIR, childPhys);
        resDir.Parent = this;
        DirCache[RES_DIR] = resDir;
        return resDir;
    }

    /// <summary>设置页面内容</summary>
    public void SetContent(PageObject page)
    {
        PutFile(CONTENT_FILE, page.Element);
    }

    /// <summary>获取页面内容</summary>
    public PageObject? GetContent()
    {
        var el = GetFile(CONTENT_FILE);
        return el is null ? null : new PageObject(el);
    }

    /// <summary>获取或创建页面内容</summary>
    public PageObject ObtainContent()
    {
        var page = GetContent();
        if (page is not null) return page;
        page = new PageObject();
        SetContent(page);
        return page;
    }
}

/// <summary>
/// Res 目录容器，存放字体/图像等资源文件。
/// 对应 Java: org.ofdrw.pkg.container.ResDir
/// </summary>
public class ResDir : VirtualContainer
{
    public ResDir(string name = "Res", string? physicalPath = null) : base(name, physicalPath) { }

    /// <summary>写入资源文件（图片、字体等二进制）</summary>
    public void AddResource(string fileName, byte[] data)
    {
        AddRaw(fileName, data);
    }

    /// <summary>写入资源文件（从路径）</summary>
    public void AddResourceFromFile(string fileName, string filePath)
    {
        AddRaw(fileName, File.ReadAllBytes(filePath));
    }

    /// <summary>获取资源文件字节</summary>
    public byte[]? GetResource(string fileName) => GetRaw(fileName);
}
