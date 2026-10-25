using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Text;

/// <summary>
/// 变换描述 CT_CGTransform（GB/T 33190-2016 §11.4.1 变换描述，图 66 表 48）。
/// <para>
/// 当存在字形变换时，TextCode 对象中使用字形变换节点（<c>CGTransform</c>）描述
/// <b>字符编码</b>和<b>字形索引</b>之间的关系：即一个（或连续多个）字符
/// 在字体文件中对应的字形索引号。
/// </para>
/// <para>
/// 典型场景：OFD 内嵌子集化字体时，字符码与字形索引不再一一对应，
/// 必须依靠 CGTransform 才能正确取字形渲染。
/// </para>
/// 对应 Java: org.ofdrw.core.text.CT_CGTransform
/// </summary>
public class CT_CGTransform : OFDElement
{
    public CT_CGTransform() : base("CGTransform") { }

    public CT_CGTransform(XElement element) : base(element) { }

    /// <summary>
    /// 【必选 属性】TextCode 中字符编码的起始位置，从 0 开始。
    /// <para>缺失或非法时返回 <c>null</c>（Java 版此处直接抛异常；此处放宽以便容错解析不合规文件）。</para>
    /// </summary>
    public int? CodePosition
    {
        get
        {
            var v = GetAttr("CodePosition");
            if (string.IsNullOrWhiteSpace(v)) return null;
            return (int)STBase.ToDouble(v);
        }
        set
        {
            if (value is null)
                throw new ArgumentNullException(nameof(value), "字符编码的起始位置不能为空");
            SetAttr("CodePosition", value.Value.ToString());
        }
    }

    /// <summary>
    /// 【可选 属性】变换关系中字符的数量，应大于等于 1，默认 1。
    /// </summary>
    public int CodeCount
    {
        get
        {
            var v = GetAttr("CodeCount");
            if (string.IsNullOrWhiteSpace(v)) return 1;
            return (int)STBase.ToDouble(v);
        }
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "变换关系中字符的数值应大于等于 1");
            SetAttr("CodeCount", value.ToString());
        }
    }

    /// <summary>
    /// 【可选 属性】变换关系中字形索引的个数，应大于等于 1，默认 1。
    /// </summary>
    public int GlyphCount
    {
        get
        {
            var v = GetAttr("GlyphCount");
            if (string.IsNullOrWhiteSpace(v)) return 1;
            return (int)STBase.ToDouble(v);
        }
        set
        {
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(value), "变换关系中字形索引的个数应大于等于 1");
            SetAttr("GlyphCount", value.ToString());
        }
    }

    /// <summary>
    /// 【可选】变换后的字形索引列表（子元素 <c>Glyphs</c>，空格分隔的整数数组）。
    /// 未设置时返回 <c>null</c>。
    /// </summary>
    public STArray? Glyphs
    {
        get
        {
            var v = GetChildText("Glyphs");
            return string.IsNullOrWhiteSpace(v) ? null : new STArray(v!);
        }
        set => SetChildText("Glyphs", value?.ToString());
    }

    /// <summary>便捷构造：创建一条字形变换。</summary>
    /// <param name="codePosition">TextCode 中字符编码的起始位置（从 0 开始）。</param>
    /// <param name="glyphs">变换后的字形索引列表。</param>
    /// <param name="codeCount">变换关系中字符的数量，默认 1。</param>
    public static CT_CGTransform Create(int codePosition, STArray glyphs, int codeCount = 1)
    {
        var cg = new CT_CGTransform
        {
            CodePosition = codePosition,
            CodeCount = codeCount
        };
        cg.Glyphs = glyphs;
        cg.GlyphCount = glyphs.Count;
        return cg;
    }
}
