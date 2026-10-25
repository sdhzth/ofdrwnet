using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color.colorSpace;

namespace NetOfd.Core.Resources;

/// <summary>
/// OFD 资源文件（Res.xml / PublicRes.xml / DocumentRes.xml）的根元素。
/// 对应 Java: org.ofdrw.core.basicStructure.res.Res
/// </summary>
public class Res : OFDElement
{
    public Res() : base("Res") { }
    public Res(XElement element) : base(element) { }

    /// <summary>资源文件的基础路径</summary>
    public STLoc? BaseLoc
    {
        get
        {
            var v = GetAttr("BaseLoc");
            return v is null ? null : new STLoc(v);
        }
        set => SetAttr("BaseLoc", value?.Value);
    }

    // ── 资源集合（OFDResource 体系） ───────────────────────────
    /// <summary>
    /// 【可选】添加一个资源集合（Fonts / ColorSpaces / DrawParams / MultiMedias /
    /// CompositeGraphicUnits 之一）。对应 Java: Res#addResource
    /// </summary>
    public Res AddResource(IOFDResource resource)
    {
        if (resource is not null) _el.Add(resource.Element);
        return this;
    }

    /// <summary>
    /// 【可选】获取资源集合列表；未知元素类型会抛 <see cref="ArgumentException"/>。
    /// 对应 Java: Res#getResources
    /// </summary>
    public List<IOFDResource> GetResources() =>
        _el.Elements()
           .Select(e => OFDResource.GetInstance(e))
           .Where(r => r is not null)
           .Select(r => r!)
           .ToList();

    // ── Fonts ─────────────────────────────────────────────────
    /// <summary>添加字体资源</summary>
    public Res AddFont(PageDescription.Font.CT_Font font)
    {
        var fonts = _el.Element(OfdNs + "Fonts");
        if (fonts is null)
        {
            fonts = new XElement(OfdNs + "Fonts");
            _el.Add(fonts);
        }
        fonts.Add(font.Element);
        return this;
    }

    /// <summary>获取所有字体</summary>
    public List<PageDescription.Font.CT_Font> GetFonts()
    {
        var fontsEl = GetChildElement("Fonts");
        if (fontsEl is null) return [];
        return fontsEl.Elements()
            .Where(e => e.Name.LocalName == "Font")
            .Select(e => new PageDescription.Font.CT_Font(e))
            .ToList();
    }

    // ── MultiMedias ───────────────────────────────────────────
    /// <summary>添加多媒体资源（图像等）</summary>
    public Res AddMultiMedia(CT_MultiMedia mm)
    {
        var mms = _el.Element(OfdNs + "MultiMedias");
        if (mms is null)
        {
            mms = new XElement(OfdNs + "MultiMedias");
            _el.Add(mms);
        }
        mms.Add(mm.Element);
        return this;
    }

    /// <summary>获取所有多媒体对象</summary>
    public List<CT_MultiMedia> GetMultiMedias()
    {
        var mmsEl = GetChildElement("MultiMedias");
        if (mmsEl is null) return [];
        return mmsEl.Elements()
            .Where(e => e.Name.LocalName == "MultiMedia")
            .Select(e => new CT_MultiMedia(e))
            .ToList();
    }

    // ── ColorSpaces ──────────────────────────────────────────
    /// <summary>获取所有颜色空间定义（位于 &lt;ColorSpaces&gt; 容器内）</summary>
    public List<CT_ColorSpace> GetColorSpaces()
    {
        var csEl = GetChildElement("ColorSpaces");
        if (csEl is null) return [];
        return csEl.Elements()
            .Where(e => e.Name.LocalName == "ColorSpace")
            .Select(e => new CT_ColorSpace(e))
            .ToList();
    }

    // ── DrawParams ───────────────────────────────────────────
    /// <summary>获取所有绘制参数定义（位于 &lt;DrawParams&gt; 容器内）</summary>
    public List<PageDescription.DrawParam.CT_DrawParam> GetDrawParams()
    {
        var dpEl = GetChildElement("DrawParams");
        if (dpEl is null) return [];
        return dpEl.Elements()
            .Where(e => e.Name.LocalName == "DrawParam")
            .Select(e => new PageDescription.DrawParam.CT_DrawParam(e))
            .ToList();
    }

    // ── CompositeGraphicUnits ─────────────────────────────────
    /// <summary>获取所有矢量图像资源（位于 &lt;CompositeGraphicUnits&gt; 容器内）。</summary>
    public List<CompositeObj.CT_VectorG> GetCompositeGraphicUnits()
    {
        var cgEl = GetChildElement("CompositeGraphicUnits");
        if (cgEl is null) return [];
        return cgEl.Elements()
            .Where(e => e.Name.LocalName == "CompositeGraphicUnit")
            .Select(e => new CompositeObj.CT_VectorG(e))
            .ToList();
    }
}

/// <summary>
/// CT_MultiMedia - 多媒体资源（图像、音频、视频）。
/// </summary>
public class CT_MultiMedia : OFDElement
{
    public CT_MultiMedia() : base("MultiMedia") { }
    public CT_MultiMedia(XElement element) : base(element) { }

    public STID ID
    {
        get => new STID(GetLongAttr("ID"));
        set => SetLongAttr("ID", value.Value);
    }

    /// <summary>媒体类型：Image / Audio / Video</summary>
    public string? Type
    {
        get => GetAttr("Type");
        set => SetAttr("Type", value);
    }

    /// <summary>媒体文件格式（如 JPEG / PNG / TIFF）</summary>
    public string? Format
    {
        get => GetAttr("Format");
        set => SetAttr("Format", value);
    }

    /// <summary>媒体文件路径</summary>
    public STLoc? MediaFile
    {
        get
        {
            var v = GetChildText("MediaFile");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("MediaFile", value?.Value);
    }
}
