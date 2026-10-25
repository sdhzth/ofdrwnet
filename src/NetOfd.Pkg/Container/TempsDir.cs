using System.Globalization;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicType;

namespace NetOfd.Pkg.Container;

/// <summary>
/// 模板文件存放目录（Doc_N/Temps 目录）。
/// <para>
/// 模板页用于描述文档中重复出现的页面元素（如页眉页脚），
/// 由 Document.xml 的 TemplatePage 引用，文件名形如 <c>Temp_0.xml</c>。
/// </para>
/// 对应 Java: org.ofdrw.pkg.container.TempsDir
/// </summary>
public class TempsDir : VirtualContainer
{
    /// <summary>模板文件名称前缀</summary>
    public const string TempFilePrefix = "Temp_";

    /// <summary>已缓存的最大模板索引；null 表示尚未扫描（Java 以 -1 表示）</summary>
    private int? _maxTempIndex;

    public TempsDir(string name = "Temps", string? physicalPath = null) : base(name, physicalPath) { }

    /// <summary>向目录中加入二进制模板资源文件（原文件不存在时静默忽略）。</summary>
    public TempsDir AddFile(string fileName, byte[] data)
    {
        if (data is null) return this;
        AddRaw(fileName, data);
        return this;
    }

    /// <summary>
    /// 以指定文件名加入模板页面，返回该模板在容器内的绝对路径。
    /// 对应 Java: <c>TempsDir.add(String fileName, Page page)</c>
    /// </summary>
    public STLoc Add(string fileName, Page page)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        PutFile(fileName, page.Element);
        return new STLoc(GetAbsolutePath()).Cat(fileName);
    }

    /// <summary>
    /// 加入模板页面（文件名自动分配为 <c>Temp_N.xml</c>），返回容器内绝对路径。
    /// 对应 Java: <c>TempsDir.add(Page page)</c>
    /// </summary>
    public STLoc Add(Page page)
    {
        if (page is null) throw new ArgumentNullException(nameof(page));
        int index = GetMaxTempIndex() + 1;
        _maxTempIndex = index;
        string fileName = string.Format(CultureInfo.InvariantCulture, "{0}{1}.xml", TempFilePrefix, index);
        return Add(fileName, page);
    }

    /// <summary>按文件名获取模板页面（不存在返回 null）。</summary>
    public Page? Get(string fileName)
    {
        var el = GetFile(fileName);
        return el is null ? null : new Page(el);
    }

    /// <summary>
    /// 获取当前容器内最大的模板文件索引号；容器中无模板文件时返回 -1。
    /// <para>结果会被缓存，首次调用后由 <see cref="Add(Page)"/> 维护自增。</para>
    /// </summary>
    public int GetMaxTempIndex()
    {
        if (_maxTempIndex is not null) return _maxTempIndex.Value;

        int max = -1;
        if (PhysicalPath is not null && Directory.Exists(PhysicalPath))
        {
            foreach (var file in Directory.EnumerateFiles(PhysicalPath))
            {
                string fileName = Path.GetFileName(file);
                if (!fileName.StartsWith(TempFilePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string numStr = fileName[TempFilePrefix.Length..].Split('.')[0];
                if (int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
                    && n > max)
                {
                    max = n;
                }
            }
        }

        _maxTempIndex = max;
        return max;
    }
}
