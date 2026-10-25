using System;

namespace NetOfd.Tool;

/// <summary>
/// 文档页面定位（路径 + 页码）。
/// 对应 Java: org.ofdrw.tool.merge.DocPage
/// </summary>
public sealed class DocPage
{
    /// <summary>文档路径</summary>
    public string Path { get; }

    /// <summary>页面索引（从 1 起）</summary>
    public int Index { get; }

    public DocPage(string path, int index)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Index = index;
    }
}
