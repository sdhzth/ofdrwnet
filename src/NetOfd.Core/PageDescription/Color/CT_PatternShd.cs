using System.Xml.Linq;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 图案底纹（PatternShd），对应 GB/T 33190 §8.3.4.2 表 31。
/// 按 PatternName 引用图案单元，并以 XStep / YStep 平铺排列。
/// 对应 Java: org.ofdrw.core.pageDescription.color.pattern.CT_PatternShd
/// </summary>
public class CT_PatternShd : CT_Shd
{
    public CT_PatternShd() : base("PatternShd") { }
    public CT_PatternShd(XElement element) : base(element) { }

    /// <summary>
    /// 获取图案单元（Pattern）
    /// </summary>
    public CT_Pattern? Pattern
    {
        get
        {
            var e = GetChildElement("Pattern");
            return e is null ? null : new CT_Pattern(e);
        }
    }

    /// <summary>
    /// 获取或设置图案名称（PatternName）
    /// </summary>
    public string? PatternName
    {
        get => GetAttr("PatternName");
        set => SetAttr("PatternName", value);
    }

    /// <summary>
    /// 获取或设置图案类型（PatternType）
    /// </summary>
    public string? PatternType
    {
        get => GetAttr("PatternType");
        set => SetAttr("PatternType", value);
    }

    // 说明：XStep / YStep 是底纹单元（CT_Pattern）的属性，不属于 PatternShd。
    // PatternShd 仅通过 PatternName 引用 CT_Pattern，平铺间距由被引用的 CT_Pattern 决定，
    // 故此处不再声明 XStep / YStep（历史上曾误在此处重复声明，导致编译期引用不存在的 Width/Height）。
}
