namespace NetOfd.Core.BasicStructure.Doc.VPreferences;

/// <summary>
/// 页面布局模式（7.5 表 9 视图首选项）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.vpreferences.PageLayout
/// <para>
/// 注意：与 <c>NetOfd.Layout.PageLayout</c>（布局引擎的分页布局，同名不同物）无关，
/// 本枚举是 OFD 标准中 VPreferences 的页面布局取值。
/// </para>
/// </summary>
public enum PageLayout
{
    /// <summary>单页模式（默认值）</summary>
    OnePage,
    /// <summary>单列模式</summary>
    OneColumn,
    /// <summary>对开模式</summary>
    TwoPageL,
    /// <summary>对开连续模式</summary>
    TwoColumnL,
    /// <summary>对开靠右模式</summary>
    TwoPageR,
    /// <summary>对开连续靠右模式</summary>
    TwoColumnR
}

/// <summary>PageLayout 解析扩展（对齐 Java PageLayout.getInstance）。</summary>
public static class PageLayoutExtensions
{
    /// <summary>
    /// 根据名称获取页面布局模式；null / 空串返回 <see cref="PageLayout.OnePage"/>，
    /// 未知值抛 <see cref="ArgumentException"/>（对齐 Java）。
    /// </summary>
    public static PageLayout ToPageLayout(this string? pageLayout)
    {
        var s = pageLayout?.Trim();
        if (string.IsNullOrEmpty(s) || s.Equals("OnePage", StringComparison.OrdinalIgnoreCase))
            return PageLayout.OnePage;

        if (Enum.TryParse<PageLayout>(s, ignoreCase: true, out var pl))
            return pl;

        throw new ArgumentException($"未知页面布局类型： {pageLayout}");
    }
}
