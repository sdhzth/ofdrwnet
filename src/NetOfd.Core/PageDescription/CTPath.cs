using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription;

/// <summary>
/// CT_Path - 路径对象（矢量图形）。
/// 对应 Java: org.ofdrw.core.pageDescription.content.layer.PathObject
/// </summary>
public class CTPath : CTGraphicUnit
{
    public CTPath() : base("PathObject") { }
    public CTPath(XElement element) : base(element) { }

    /// <summary>是否描边</summary>
    public bool Stroke
    {
        get => GetBoolAttr("Stroke", true);
        set => SetBoolAttr("Stroke", value);
    }

    /// <summary>是否填充</summary>
    public bool Fill
    {
        get => GetBoolAttr("Fill", false);
        set => SetBoolAttr("Fill", value);
    }

    /// <summary>填充规则</summary>
    public string? Rule
    {
        get => GetAttr("Rule");
        set => SetAttr("Rule", value);
    }

    /// <summary>路径数据（AbbreviatedData 格式）</summary>
    public string? AbbreviatedData
    {
        get => GetChildText("AbbreviatedData");
        set => SetChildText("AbbreviatedData", value);
    }

    /// <summary>线宽（覆盖继承值）</summary>
    public new double LineWidth
    {
        get => GetDoubleAttr("LineWidth", 0.353);
        set => SetDoubleAttr("LineWidth", value);
    }

    /// <summary>线宽是否显式设置（未设置时应继承 DrawParam 链值）</summary>
    internal double? RawLineWidth => GetAttr("LineWidth") is null ? null : LineWidth;

    /// <summary>线端样式：Butt / Round / Square</summary>
    public string? Cap
    {
        get => GetAttr("Cap");
        set => SetAttr("Cap", value);
    }

    /// <summary>线连接样式：Miter / Round / Bevel</summary>
    public string? Join
    {
        get => GetAttr("Join");
        set => SetAttr("Join", value);
    }

    /// <summary>
    /// 虚线偏移量（规范/Java 属性名 <c>DashOffset</c>），虚线重复样式开始处的位移。
    /// null 表示不设置（从 0 起）。
    /// 对应 Java: <c>CT_GraphicUnit.setDashOffset(Double)</c>
    /// </summary>
    public double? DashOffset
    {
        get
        {
            var v = GetAttr("DashOffset");
            return v is null ? null : STBase.ToDouble(v);
        }
        set => SetAttr("DashOffset", value is null ? null : STBase.Fmt(value.Value));
    }

    /// <summary>
    /// 虚线重复样式（规范/Java 属性名 <c>DashPattern</c>）：空格分隔的「线长 空白」交替序列，
    /// 如 "3 2" 表示 3 长线 + 2 空白。null 表示实线。
    /// 读取回退历史私有写法 <c>Dash</c>（旧版 NetOfd 产物）；写入一律落 <c>DashPattern</c>。
    /// 对应 Java: <c>CT_GraphicUnit.setDashPattern(ST_Array)</c>
    /// </summary>
    public STArray? DashPattern
    {
        get
        {
            var v = GetAttr("DashPattern") ?? GetAttr("Dash");
            return v is null ? null : STArray.Parse(v);
        }
        set => SetAttr("DashPattern", value?.ToString());
    }

    /// <summary>
    /// 虚线兼容访问器（别名）：等同 <see cref="DashPattern"/>。
    /// 读取时优先取规范属性 <c>DashPattern</c>，回退历史私有写法 <c>Dash</c>；写入一律落规范属性。
    /// </summary>
    public STArray? Dash
    {
        get => DashPattern;
        set => DashPattern = value;
    }
}


