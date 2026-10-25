using System.Xml.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace NetOfd.Core.BasicStructure.Outlines;

/// <summary>
/// 大纲按照树形结构进行组织（图 18 大纲节点结构）。
/// 对应 Java: org.ofdrw.core.basicStructure.outlines.Outlines
/// </summary>
public class Outlines : OFDElement, IEnumerable<CT_OutlineElem>
{
    public Outlines() : base("Outlines") { }
    public Outlines(XElement element) : base(element) { }

    /// <summary>【必选】增加大纲节点</summary>
    public Outlines AddOutlineElem(CT_OutlineElem outlineElem)
    {
        AddOFDEntity(outlineElem);
        return this;
    }

    /// <summary>【必选】获取大纲节点列表</summary>
    public List<CT_OutlineElem> GetOutlineElems() =>
        GetChildElements("OutlineElem").Select(e => new CT_OutlineElem(e)).ToList();

    public IEnumerator<CT_OutlineElem> GetEnumerator() => GetOutlineElems().GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
