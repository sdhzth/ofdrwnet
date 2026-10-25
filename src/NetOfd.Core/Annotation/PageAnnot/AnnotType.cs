namespace NetOfd.Core.Annotation.PageAnnot;

/// <summary>
/// 注释类型取值。
/// <para>GB/T 33190-2016 第 15.2 节</para>
/// 对应 Java: org.ofdrw.core.annotation.pageannot.AnnotType
/// </summary>
public enum AnnotType
{
    /// <summary>连接注释</summary>
    Link,

    /// <summary>路径注释，一般为图形对象，比如矩形、多边形、贝塞尔曲线等</summary>
    Path,

    /// <summary>高亮注释</summary>
    Highlight,

    /// <summary>签章注释</summary>
    Stamp,

    /// <summary>水印注释</summary>
    Watermark
}

/// <summary>注释类型解析。</summary>
public static class AnnotTypeParser
{
    /// <summary>解析注释类型字符串（忽略大小写），未知取值抛出 ArgumentException。</summary>
    public static AnnotType Parse(string? str)
    {
        str = str?.Trim() ?? "";
        if (str.Equals("Link", StringComparison.OrdinalIgnoreCase)) return AnnotType.Link;
        if (str.Equals("Path", StringComparison.OrdinalIgnoreCase)) return AnnotType.Path;
        if (str.Equals("Highlight", StringComparison.OrdinalIgnoreCase)) return AnnotType.Highlight;
        if (str.Equals("Stamp", StringComparison.OrdinalIgnoreCase)) return AnnotType.Stamp;
        if (str.Equals("Watermark", StringComparison.OrdinalIgnoreCase)) return AnnotType.Watermark;
        throw new ArgumentException("未知的注释类型取值：" + str);
    }
}
