using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color.colorSpace;

/// <summary>
/// 颜色空间（GB/T 33190-2016 §8.3 图 24）。
/// 支持 GRAY / RGB / CMYK；可选定义调色板（Palette）或指向颜色配置文件（Profile）。
/// 对应 Java: org.ofdrw.core.pageDescription.color.colorSpace.CT_ColorSpace
/// </summary>
public class CT_ColorSpace : OFDElement
{
    public CT_ColorSpace() : base("ColorSpace") { }
    public CT_ColorSpace(XElement element) : base(element) { }
    public CT_ColorSpace(OFDColorSpaceType type) : this() { Type = type; }

    /// <summary>对象 ID</summary>
    public STID? ID => GetObjID();

    /// <summary>【必选】颜色空间类型；解析失败（未知类型）时回退为 RGB，避免渲染中断。</summary>
    public OFDColorSpaceType Type
    {
        get
        {
            try { return OFDColorSpaceTypeExtensions.GetInstance(GetAttr("Type")); }
            catch { return OFDColorSpaceType.RGB; }
        }
        set => SetAttr("Type", value.ToString());
    }

    /// <summary>【可选】每个颜色通道使用的位数（1/2/4/8/16）。</summary>
    public int? BitsPerComponent
    {
        get
        {
            var v = GetAttr("BitsPerComponent");
            return v is null ? null : STBase.ToInt(v);
        }
        set => SetAttr("BitsPerComponent", value?.ToString());
    }

    /// <summary>【可选】指向包内颜色配置文件路径。</summary>
    public string? Profile
    {
        get => GetAttr("Profile");
        set => SetAttr("Profile", value);
    }

    /// <summary>【可选】调色板（索引从 0 开始）。</summary>
    public Palette? Palette
    {
        get
        {
            var e = GetChildElement("Palette");
            return e is null ? null : new Palette(e);
        }
        set
        {
            GetChildElement("Palette")?.Remove();
            if (value is not null)
            {
                value._el.Name = OfdNs + "Palette";
                _el.Add(value._el);
            }
        }
    }
}
