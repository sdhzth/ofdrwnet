using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;

namespace NetOfd.Core.CompositeObj;

/// <summary>
/// 复合对象（13 复合对象 图 71 表 49）：一种特殊的图元对象，拥有图元对象的一切特性，
/// 但其内容在 ResourceID 指向的矢量图像资源中描述；一个资源可被多个复合对象引用，
/// 借此实现文档内矢量图文内容的复用。
/// 对应 Java: org.ofdrw.core.compositeObj.CT_Composite
/// </summary>
public class CT_Composite : CTGraphicUnit
{
    public CT_Composite() : base("Composite") { }

    public CT_Composite(XElement element) : base(element) { }

    /// <summary>以指定元素名构造（供 CompositeObject 等子类使用）。</summary>
    protected CT_Composite(string name) : base(name) { }

    /// <summary>
    /// 【必选 属性】引用资源文件中定义的矢量图像标识。
    /// 复合对象引用的是 Res 中的矢量图像（CompositeGraphicUnit，类型 CT_VectorG）。
    /// </summary>
    public STRefID? ResourceID
    {
        get
        {
            var v = GetAttr("ResourceID");
            return v is null ? null : STRefID.Parse(v);
        }
        set
        {
            if (value is null) throw new ArgumentException("矢量图像的标识（ResourceID）不能为空");
            SetAttr("ResourceID", value.ToString());
        }
    }
}
