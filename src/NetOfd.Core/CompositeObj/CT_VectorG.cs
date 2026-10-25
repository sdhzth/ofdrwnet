using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.CompositeObj;

/// <summary>
/// 矢量图像（13 复合对象 图 72 表 50）：复合对象引用的资源实体，
/// 在 Res 中以 <c>CompositeGraphicUnit</c> 元素名登记。
/// 对应 Java: org.ofdrw.core.compositeObj.CT_VectorG
/// </summary>
public sealed class CT_VectorG : OFDElement
{
    public CT_VectorG() : base("VectorG") { }

    public CT_VectorG(XElement element) : base(element) { }

    /// <summary>对象 ID</summary>
    public STID? ID
    {
        get => GetObjID();
        set
        {
            if (value is not null) SetObjID(value);
        }
    }

    /// <summary>
    /// 【必选 属性】矢量图像的宽度，超出部分做裁剪处理。
    /// 未设置时读取抛 <see cref="InvalidOperationException"/>（与 Java 一致）。
    /// </summary>
    public double Width
    {
        get
        {
            var v = GetAttr("Width");
            if (string.IsNullOrWhiteSpace(v))
                throw new InvalidOperationException("格式非法无法获取到矢量图像的宽度（Width）");
            return STBase.ToDouble(v);
        }
        set => SetDoubleAttr("Width", value);
    }

    /// <summary>
    /// 【必选 属性】矢量图像的高度，超出部分做裁剪处理。
    /// 未设置时读取抛 <see cref="InvalidOperationException"/>（与 Java 一致）。
    /// </summary>
    public double Height
    {
        get
        {
            var v = GetAttr("Height");
            if (string.IsNullOrWhiteSpace(v))
                throw new InvalidOperationException("格式非法无法获取到矢量图像的高度（Height）");
            return STBase.ToDouble(v);
        }
        set => SetDoubleAttr("Height", value);
    }

    /// <summary>【可选】缩略图，指向包内的图像文件。</summary>
    public STRefID? Thumbnail
    {
        get
        {
            var v = GetChildText("Thumbnail");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetChildText("Thumbnail", value?.ToString());
    }

    /// <summary>【可选】替换图像，用于高分辨率输出时替换缩略图。</summary>
    public STRefID? Substitution
    {
        get
        {
            var v = GetChildText("Substitution");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetChildText("Substitution", value?.ToString());
    }

    /// <summary>
    /// 【必选】设置内容的矢量描述；null 时抛 <see cref="ArgumentException"/>。
    /// </summary>
    public CT_VectorG SetContent(Content content)
    {
        if (content is null) throw new ArgumentException("内容的矢量描述（Content）不能为空");
        SetOFDEntity(content);
        return this;
    }

    /// <summary>
    /// 【必选】向内容的矢量描述追加一个页块（Content 不存在时自动创建）。
    /// </summary>
    public CT_VectorG AddContent(CT_PageBlock block)
    {
        if (block is null) return this;

        var existing = GetChildElement("Content");
        var content = existing is null ? new Content() : new Content(existing);
        content.AddPageBlock(block);
        SetOFDEntity(content);
        return this;
    }

    /// <summary>
    /// 【必选】获取内容的矢量描述；不存在时抛 <see cref="InvalidOperationException"/>。
    /// </summary>
    public Content GetContent()
    {
        var e = GetChildElement("Content");
        if (e is null) throw new InvalidOperationException("没有找到Content元素");
        return new Content(e);
    }
}
