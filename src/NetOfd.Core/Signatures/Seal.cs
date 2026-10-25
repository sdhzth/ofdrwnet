using NetOfd.Core.BasicType;
using System.Xml.Linq;

namespace NetOfd.Core.Signatures;

/// <summary>
/// 电子印章信息（GB/T 33190-2016 §18.2.1 图 86 表 67）。
/// 对应 Java: org.ofdrw.core.signatures.appearance.Seal
/// </summary>
/// <remarks>
/// <c>BaseLoc</c> / <c>ImageLoc</c> 在 OFD 中是<b>子元素</b>（不是属性），
/// 与 <see cref="StampAnnot"/> 的 <c>Boundary</c> / <c>Clip</c>（属性）写法不同，移植时勿混。
/// </remarks>
public class Seal : OFDElement
{
    public Seal() : base("Seal") { }

    public Seal(XElement element) : base(element) { }

    /// <summary>以电子印章文件路径创建。</summary>
    public Seal(STLoc baseLoc) : this() => BaseLoc = baseLoc;

    /// <summary>【必选】指向包内的安全电子印章文件路径（<c>BaseLoc</c> 子元素）。</summary>
    public STLoc? BaseLoc
    {
        get => GetLocChild("BaseLoc");
        set => SetLocChild("BaseLoc", value);
    }

    /// <summary>【可选 OFD 2.0】印模图片存储位置（<c>ImageLoc</c> 子元素）。</summary>
    public STLoc? ImageLoc
    {
        get => GetLocChild("ImageLoc");
        set => SetLocChild("ImageLoc", value);
    }

    private STLoc? GetLocChild(string name)
    {
        string? t = GetChildText(name);
        return string.IsNullOrWhiteSpace(t) ? null : new STLoc(t.Trim());
    }

    private void SetLocChild(string name, STLoc? value)
    {
        if (value is null)
        {
            GetChildElement(name)?.Remove();
            return;
        }
        SetChildText(name, value.Value);
    }
}
