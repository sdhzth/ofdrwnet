namespace NetOfd.Core.BasicStructure.Doc.VPreferences;

/// <summary>
/// 标题栏显示模式（7.5 表 9 视图首选项）。
/// <para>
/// 默认值为 <see cref="FileName"/>；当设置为 <see cref="DocTitle"/> 但不存在 Title 属性时，
/// 按 <see cref="FileName"/> 处理。
/// </para>
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.TabDisplay
/// </summary>
public enum TabDisplay
{
    /// <summary>显示文件名称（默认值）</summary>
    FileName,
    /// <summary>呈现元数据中的 Title 属性</summary>
    DocTitle
}

/// <summary>TabDisplay 解析扩展（对齐 Java TabDisplay.getInstance）。</summary>
public static class TabDisplayExtensions
{
    /// <summary>
    /// 根据名称获取标题栏显示模式；null / 空串返回 <see cref="TabDisplay.FileName"/>，
    /// 未知值抛 <see cref="ArgumentException"/>（对齐 Java）。
    /// </summary>
    public static TabDisplay ToTabDisplay(this string? tabDisplay)
    {
        var s = tabDisplay?.Trim();
        if (string.IsNullOrEmpty(s))
            return TabDisplay.FileName;

        if (Enum.TryParse<TabDisplay>(s, ignoreCase: true, out var td))
            return td;

        throw new ArgumentException($"未知的标题栏显示模式： {tabDisplay}");
    }
}
