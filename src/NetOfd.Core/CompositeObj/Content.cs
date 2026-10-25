using System.Xml.Linq;

namespace NetOfd.Core.CompositeObj;

/// <summary>
/// 内容的矢量描述（13 复合对象 表 50）。
/// 对应 Java: org.ofdrw.core.compositeObj.Content
/// </summary>
public sealed class Content : CT_PageBlock
{
    public Content() : base("Content") { }

    public Content(XElement element) : base(element) { }
}
