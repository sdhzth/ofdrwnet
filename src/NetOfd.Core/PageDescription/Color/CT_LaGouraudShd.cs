using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 网格高洛德渐变（GB/T 33190 §8.3.4.5 图 42-43 表 32）。
/// <para>
/// 高洛德渐变的一种特殊形式：允许 4 个以上控制点，按每行固定的网格数
/// （VerticesPerRow）形成若干行列；相邻 4 个控制点定义一个网格单元，单元内
/// EdgeFlag 固定为 1；每对相邻行形成一条三角带（对齐 ISO 32000-1 type-5
/// lattice-form Gouraud-shaded mesh）。
/// </para>
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.CT_LaGouraudShd
/// </summary>
public class CT_LaGouraudShd : CT_GouraudShd
{
    public CT_LaGouraudShd() : base("LaGouraudShd") { }
    public CT_LaGouraudShd(XElement element) : base(element) { }

    /// <summary>【必选 属性】渐变区域内每行的网格数（Java 语义：缺失视为文档错误，此处宽松返回 null）。</summary>
    public int? VerticesPerRow
    {
        get
        {
            var v = GetAttr("VerticesPerRow");
            return v is null ? null : STBase.ToInt(v);
        }
        set => SetAttr("VerticesPerRow", value?.ToString());
    }

    /// <summary>设置渐变区域内每行的网格数（必选；null 移除属性）。</summary>
    public CT_LaGouraudShd SetVerticesPerRow(int? verticesPerRow)
    {
        VerticesPerRow = verticesPerRow;
        return this;
    }
}
