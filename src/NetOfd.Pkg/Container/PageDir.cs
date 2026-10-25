using NetOfd.Core.BasicStructure.PageObj;

namespace NetOfd.Pkg.Container;

/// <summary>
/// Pages 目录容器，包含所有 Page_N 子目录。
/// 对应 Java: org.ofdrw.pkg.container.PagesDir
/// </summary>
public class PagesDir : VirtualContainer
{
    public PagesDir(string name = "Pages", string? physicalPath = null) : base(name, physicalPath) { }

    /// <summary>获取或创建第 index 个页面目录（0-based）</summary>
    public PageDir ObtainPageDir(int index)
    {
        string dirName = PageDir.PageContainerPrefix + index;
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

    public PageDir(string name, string? physicalPath = null) : base(name, physicalPath) { }

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
