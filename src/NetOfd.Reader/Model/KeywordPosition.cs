using NetOfd.Core.BasicType;

namespace NetOfd.Reader.Model;

/// <summary>
/// 关键字位置：关键词在文档中出现的页码（从 1 开始）与精确外接矩形（单位 mm）。
/// <para>对应 Java: <c>org.ofdrw.reader.keyword.KeywordPosition</c>（page + box + keyword）。</para>
/// <para>Box 是关键词覆盖字符的最小外接框（含 CTM 变换与换行/跨 TextCode 合并语义），
/// 可直接用于高亮、标注或点击定位。</para>
/// </summary>
public class KeywordPosition
{
    /// <summary>关键字所在页码（1-based，对齐 Java）</summary>
    public int Page { get; set; }

    /// <summary>关键词外接矩形（mm，页面坐标）</summary>
    public STBox Box { get; set; } = new();

    /// <summary>所属关键字</summary>
    public string Keyword { get; set; } = string.Empty;

    public override string ToString() =>
        $"Page={Page}, Box=({Box.TopLeftX:F2},{Box.TopLeftY:F2} {Box.Width:F2}x{Box.Height:F2}), Keyword='{Keyword}'";
}
