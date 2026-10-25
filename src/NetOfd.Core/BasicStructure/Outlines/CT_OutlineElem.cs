using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;
using NetOfd.Core.Action;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.Outlines;

/// <summary>
/// 大纲节点（图 19 大纲节点结构）。可层层嵌套形成树状结构。
/// 对应 Java: org.ofdrw.core.basicStructure.outlines.CT_OutlineElem
/// </summary>
public class CT_OutlineElem : OFDElement
{
    public CT_OutlineElem() : base("OutlineElem") { }
    public CT_OutlineElem(XElement element) : base(element) { }

    public CT_OutlineElem(string title) : this()
    {
        Title = title;
    }

    /// <summary>【必选 属性】大纲节点标题</summary>
    public string? Title
    {
        get => GetAttr("Title");
        set => SetAttr("Title", value);
    }

    /// <summary>【可选 属性】该节点下所有叶节点的数目参考值，默认 0</summary>
    public int Count
    {
        get => STBase.ToInt(GetAttr("Count"));
        set => SetAttr("Count", value.ToString());
    }

    /// <summary>【可选 属性】初始是否展开子节点，默认 true</summary>
    public bool Expanded
    {
        get => GetBoolAttr("Expanded", true);
        set => SetBoolAttr("Expanded", value);
    }

    /// <summary>【可选】当此大纲节点被激活时执行的动作序列</summary>
    public Actions? Actions
    {
        get
        {
            var e = GetChildElement("Actions");
            return e is null ? null : new Actions(e);
        }
        set
        {
            if (value is null) GetChildElement("Actions")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>【可选】增加大纲子节点</summary>
    public CT_OutlineElem AddOutlineElem(CT_OutlineElem outlineElem)
    {
        AddOFDEntity(outlineElem);
        return this;
    }

    /// <summary>【可选】获取该大纲下所有子节点</summary>
    public List<CT_OutlineElem> GetOutlineElems() =>
        GetChildElements("OutlineElem").Select(e => new CT_OutlineElem(e)).ToList();
}
