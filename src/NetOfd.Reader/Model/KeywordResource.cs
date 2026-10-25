using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Font;

namespace NetOfd.Reader.Model;

/// <summary>
/// 关键词命中的文本资源：命中所在页码 + 字体对象 + 文字对象。
/// 对应 Java: org.ofdrw.reader.keyword.KeywordResource
/// <para>
/// 类型映射：Java <c>CT_Text</c> → netofd <see cref="CTText"/>；
/// <c>CT_Font</c> → netofd <see cref="CT_Font"/>（命名保留下划线）。
/// </para>
/// </summary>
public class KeywordResource
{
    /// <summary>命中所在页码（1 起）。</summary>
    public int Page { get; set; }

    /// <summary>命中文字所引用的字体对象。</summary>
    public CT_Font? Font { get; set; }

    /// <summary>命中的文字对象。</summary>
    public CTText? Text { get; set; }
}
