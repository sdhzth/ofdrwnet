using NetOfd.Core.BasicType;
using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 签名的外观（GB/T 33190-2016 §18.2.3 图 88 表 69）。
/// 一个数字签名可关联零个或多个外观描述。
/// 对应 Java: org.ofdrw.core.signatures.appearance.StampAnnot
/// </summary>
/// <remarks>
/// 本类型是 <b>OFD XML 元素</b>（<c>&lt;StampAnnot ID="" PageRef="" Boundary=""&gt;</c>），
/// 与 <c>NetOfd.Sign</c> 模块中的 <c>StampAnnotEntity</c>（印章图片 VO）与
/// <c>SignatureModels.StampAnnot</c>（SES 二进制结构）<b>不是同一层东西</b>，勿混用。
/// </remarks>
public class StampAnnot : OFDElement
{
    public StampAnnot() : base("StampAnnot") { }

    public StampAnnot(XElement element) : base(element) { }

    /// <summary>【必选 属性】签章注释的标识（推荐使用 “sNNN” 编码，NNN 从 1 开始）。</summary>
    public string? ID
    {
        get => GetAttr("ID");
        set => SetAttr("ID", value);
    }

    /// <summary>【必选 属性】引用外观注释所在的页面标识符。</summary>
    public STRefID? PageRef
    {
        get
        {
            string? v = GetAttr("PageRef");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("PageRef", value?.ToString());
    }

    /// <summary>【必选 属性】签章注释的外观边框位置，用于页面内定位。</summary>
    public STBox? Boundary
    {
        get => ParseBox("Boundary");
        set => SetAttr("Boundary", value?.ToString());
    }

    /// <summary>【可选 属性】签章注释的外观裁剪设置。</summary>
    public STBox? Clip
    {
        get => ParseBox("Clip");
        set => SetAttr("Clip", value?.ToString());
    }

    private STBox? ParseBox(string name)
    {
        string? v = GetAttr(name);
        return v is null ? null : STBox.TryParse(v);
    }
}
