using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription;

/// <summary>
/// 裁剪区域集合（图元 Clips 子元素）：包含一个或多个 <see cref="CT_Clip"/>。
/// </summary>
public class CT_Clips : OFDElement
{
    public CT_Clips() : base("Clips") { }

    public CT_Clips(XElement xe) : base(xe) { }

    private readonly List<CT_Clip> _clips = new();

    public IReadOnlyList<CT_Clip> Clips
    {
        get
        {
            if (_clips.Count == 0)
                foreach (var e in GetChildElements("Clip"))
                    _clips.Add(new CT_Clip(e));
            return _clips;
        }
    }

    public CT_Clips AddClip(CT_Clip clip)
    {
        if (clip is null) throw new ArgumentNullException(nameof(clip));
        _ = Clips; // 先惰性加载既有子元素，避免追加时丢失
        _clips.Add(clip);
        _el.Add(clip.Element);
        return this;
    }
}

/// <summary>
/// 单个裁剪区域：由一个或多个 <see cref="ClipArea"/> 围成的裁剪路径。
/// </summary>
public class CT_Clip : OFDElement
{
    public CT_Clip() : base("Clip") { }

    public CT_Clip(XElement xe) : base(xe) { }

    private readonly List<ClipArea> _areas = new();

    public IReadOnlyList<ClipArea> Areas
    {
        get
        {
            if (_areas.Count == 0)
                foreach (var e in GetChildElements("Area"))
                    _areas.Add(new ClipArea(e));
            return _areas;
        }
    }

    /// <summary>追加裁剪子区域。</summary>
    public CT_Clip AddArea(ClipArea area)
    {
        if (area is null) throw new ArgumentNullException(nameof(area));
        _ = Areas;
        _areas.Add(area);
        _el.Add(area.Element);
        return this;
    }
}

/// <summary>
/// 裁剪子区域：Boundary 包围盒 + AbbreviatedData 路径数据。
/// </summary>
public class ClipArea : OFDElement
{
    public ClipArea() : base("Area") { }

    public ClipArea(XElement xe) : base(xe) { }

    public STBox? Boundary
    {
        get
        {
            var v = GetAttr("Boundary");
            return v is null ? null : STBox.Parse(v);
        }
        set => SetAttr("Boundary", value?.ToString());
    }

    public string? AbbreviatedData
    {
        get => GetChildText("AbbreviatedData");
        set => SetChildText("AbbreviatedData", value);
    }
}

