using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.Doc;

/// <summary>
/// CT_PageArea - 页面区域定义（页面大小、出血框等）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.CT_PageArea
/// </summary>
public class CT_PageArea : OFDElement
{
    // A4 尺寸（mm）
    public static readonly STBox A4 = new(0, 0, 210, 297);
    // A3 尺寸
    public static readonly STBox A3 = new(0, 0, 297, 420);

    public CT_PageArea() : base("PageArea") { }
    public CT_PageArea(XElement element) : base(element) { }

    /// <summary>物理页面大小</summary>
    public STBox? PhysicalBox
    {
        get
        {
            var v = GetChildText("PhysicalBox");
            return v is null ? null : STBox.Parse(v);
        }
        set => SetChildText("PhysicalBox", value?.ToString());
    }

    /// <summary>显示区域</summary>
    public STBox? ApplicationBox
    {
        get
        {
            var v = GetChildText("ApplicationBox");
            return v is null ? null : STBox.Parse(v);
        }
        set => SetChildText("ApplicationBox", value?.ToString());
    }

    /// <summary>内容区域</summary>
    public STBox? ContentBox
    {
        get
        {
            var v = GetChildText("ContentBox");
            return v is null ? null : STBox.Parse(v);
        }
        set => SetChildText("ContentBox", value?.ToString());
    }

    /// <summary>出血框</summary>
    public STBox? BleedBox
    {
        get
        {
            var v = GetChildText("BleedBox");
            return v is null ? null : STBox.Parse(v);
        }
        set => SetChildText("BleedBox", value?.ToString());
    }
}
