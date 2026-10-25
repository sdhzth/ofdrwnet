namespace NetOfd.Core.BasicStructure.Doc.VPreferences;

/// <summary>
/// 窗口模式（7.5 表 9 视图首选项属性）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.PageMode
/// </summary>
public enum PageMode
{
    /// <summary>常规模式（默认值）</summary>
    None,
    /// <summary>打开后全文显示</summary>
    FullScreen,
    /// <summary>同时呈现文档大纲</summary>
    UseOutlines,
    /// <summary>同时呈现缩略图</summary>
    UseThumbs,
    /// <summary>同时呈现语义结构</summary>
    UseCustomTags,
    /// <summary>同时呈现图层</summary>
    UseLayers,
    /// <summary>同时呈现附件</summary>
    UseAttatchs,
    /// <summary>同时呈现书签</summary>
    UseBookmarks
}

/// <summary>PageMode 解析扩展（对齐 Java PageMode.getInstance）。</summary>
public static class PageModeExtensions
{
    /// <summary>
    /// 根据名称获取窗口模式；null / 空串 / "None" 返回 <see cref="PageMode.None"/>，
    /// 未知值抛 <see cref="ArgumentException"/>（对齐 Java）。
    /// </summary>
    public static PageMode ToPageMode(this string? mode)
    {
        var s = mode?.Trim();
        if (string.IsNullOrEmpty(s) || s.Equals("None", StringComparison.OrdinalIgnoreCase))
            return PageMode.None;

        if (Enum.TryParse<PageMode>(s, ignoreCase: true, out var pm))
            return pm;

        throw new ArgumentException($"未知的窗口模式： {mode}");
    }
}
