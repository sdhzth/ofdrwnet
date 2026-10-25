using NetOfd.Core.BasicType;
using NetOfd.Pkg.Container;
using System.Xml.Linq;

namespace NetOfd.Reader;

/// <summary>
/// 资源定位器——将 OFD 标准路径（ST_Loc）解析为容器内的实际资源。
/// <para>
/// 支持两种路径形式：
/// <list type="bullet">
/// <item>绝对路径（以 <c>/</c> 开头，例如 <c>/Doc_0/Pages/Page_0/Content.xml</c>），从 OFD 包根目录解析；</item>
/// <item>相对路径（例如 <c>Pages/Page_0/Content.xml</c>），相对当前文档目录（Doc_0）解析。</item>
/// </list>
/// </para>
/// 对应 Java: org.ofdrw.reader.ResourceLocator（.NET 简化版：单文档场景，无目录切换）
/// </summary>
public class ResourceLocator
{
    private readonly VirtualContainer _root;
    private readonly DocDir _docDir;

    public ResourceLocator(VirtualContainer root, DocDir docDir)
    {
        _root = root;
        _docDir = docDir;
    }

    /// <summary>当前文档目录的绝对路径（例如 "/Doc_0"）</summary>
    public string CurrentDirPath
    {
        get
        {
            // 根容器名为 "ROOT"，应省略，使路径从文档根开始（如 "/Doc_0"）
            string abs = _docDir.GetAbsolutePath();
            const string rootPrefix = "/ROOT";
            return abs.StartsWith(rootPrefix) ? abs[rootPrefix.Length..] : abs;
        }
    }

    /// <summary>
    /// 解析路径获取 XML 文件元素；文件不存在或无法解析时返回 null。
    /// </summary>
    public XElement? Get(STLoc loc)
    {
        var container = ResolveContainer(loc, out string fileName);
        return container?.GetFile(fileName);
    }

    /// <summary>
    /// 解析路径获取原始二进制资源（图像等）；不存在时返回 null。
    /// </summary>
    public byte[]? GetRaw(STLoc loc)
    {
        var container = ResolveContainer(loc, out string fileName);
        return container?.GetRaw(fileName);
    }

    /// <summary>判断路径对应的文件是否存在</summary>
    public bool Exist(STLoc loc)
    {
        var container = ResolveContainer(loc, out string fileName);
        if (container is null) return false;
        return container.FileExists(fileName) || container.GetRaw(fileName) is not null;
    }

    /// <summary>
    /// 将相对路径转换为文档中的绝对路径（以 "/" 开头，指向具体文件）。
    /// </summary>
    public string ToAbsolutePath(STLoc loc)
    {
        if (loc.IsRootPath) return loc.Value;
        string combined = STLoc.Resolve(CurrentDirPath + "/", loc.Value).Value;
        return combined.StartsWith('/') ? combined : "/" + combined;
    }

    /// <summary>
    /// 将路径解析为绝对路径并包装为 ST_Loc。
    /// </summary>
    public STLoc GetAbsTo(STLoc loc) => new(ToAbsolutePath(loc));

    // 解析路径定位到最终文件所在的容器
    private VirtualContainer? ResolveContainer(STLoc loc, out string fileName)
    {
        fileName = string.Empty;
        var parts = loc.Parts();
        if (parts.Length == 0) return null;

        VirtualContainer current = loc.IsRootPath ? _root : _docDir;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var child = current.GetContainer(parts[i]);
            if (child is null) return null;
            current = child;
        }
        fileName = parts[^1];
        return current;
    }
}