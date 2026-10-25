using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicType;
using NetOfd.Core.CompositeObj;
using NetOfd.Core.PageDescription.Color.colorSpace;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.Resources;
using NetOfd.Pkg.Container;
using System.Xml.Linq;

namespace NetOfd.Reader;

/// <summary>
/// 资源管理器（只读），解析 OFD 文档中引用的字体、图像、颜色空间等资源。
/// 对应 Java: org.ofdrw.reader.ResourceManage
/// </summary>
public class ResourceManage
{
    private readonly DocDir _docDir;
    private readonly Document _document;

    private Dictionary<long, CT_Font>? _fontMap;
    private Dictionary<long, CT_MultiMedia>? _multiMediaMap;
    private Dictionary<long, CT_ColorSpace>? _colorSpaceMap;
    private Dictionary<long, CT_DrawParam>? _drawParamMap;
    private Dictionary<long, CT_VectorG>? _compositeMap;

    public ResourceManage(DocDir docDir, Document document)
    {
        _docDir = docDir;
        _document = document;
    }

    // ── 字体资源 ─────────────────────────────────────────────

    /// <summary>获取所有字体（ID → CT_Font）</summary>
    public Dictionary<long, CT_Font> GetFontMap()
    {
        if (_fontMap is not null) return _fontMap;

        _fontMap = new Dictionary<long, CT_Font>();
        var commonData = _document.CommonData;
        if (commonData is null) return _fontMap;

        // 加载 PublicRes
        foreach (var locStr in commonData.PublicResList)
        {
            LoadFontsFromRes(locStr.Value);
        }

        // 加载 DocumentRes
        foreach (var locStr in commonData.DocumentResList)
        {
            LoadFontsFromRes(locStr.Value);
        }

        return _fontMap;
    }

    private void LoadFontsFromRes(string resPath)
    {
        // 尝试直接加载 PublicRes.xml / DocumentRes.xml
        string fileName = System.IO.Path.GetFileName(resPath);
        var resXml = _docDir.GetFile(fileName);
        if (resXml is null) return;

        var res = new Res(resXml);
        foreach (var font in res.GetFonts())
        {
            _fontMap![font.ID.Value] = font;
        }
    }

    /// <summary>按 ID 获取字体</summary>
    public CT_Font? GetFont(long id)
    {
        GetFontMap().TryGetValue(id, out var font);
        return font;
    }

    // ── 图像资源 ─────────────────────────────────────────────

    /// <summary>获取所有多媒体资源（ID → CT_MultiMedia）</summary>
    public Dictionary<long, CT_MultiMedia> GetMultiMediaMap()
    {
        if (_multiMediaMap is not null) return _multiMediaMap;

        _multiMediaMap = new Dictionary<long, CT_MultiMedia>();
        var commonData = _document.CommonData;
        if (commonData is null) return _multiMediaMap;

        foreach (var locStr in commonData.PublicResList)
            LoadMediaFromRes(locStr.Value);
        foreach (var locStr in commonData.DocumentResList)
            LoadMediaFromRes(locStr.Value);

        return _multiMediaMap;
    }

    private void LoadMediaFromRes(string resPath)
    {
        string fileName = System.IO.Path.GetFileName(resPath);
        var resXml = _docDir.GetFile(fileName);
        if (resXml is null) return;

        var res = new Res(resXml);
        foreach (var mm in res.GetMultiMedias())
        {
            _multiMediaMap![mm.ID.Value] = mm;
        }
    }

    /// <summary>按 ID 获取多媒体资源</summary>
    public CT_MultiMedia? GetMultiMedia(long id)
    {
        GetMultiMediaMap().TryGetValue(id, out var mm);
        return mm;
    }

    /// <summary>获取图像资源的二进制数据</summary>
    public byte[]? GetImageData(CT_MultiMedia mm)
    {
        if (mm.MediaFile is null) return null;
        var resDir = _docDir.ObtainRes();
        return resDir.GetResource(mm.MediaFile.GetFileName());
    }

    // ── 颜色空间资源 ─────────────────────────────────────────

    /// <summary>获取所有颜色空间（ID → CT_ColorSpace）</summary>
    public Dictionary<long, CT_ColorSpace> GetColorSpaceMap()
    {
        if (_colorSpaceMap is not null) return _colorSpaceMap;

        _colorSpaceMap = new Dictionary<long, CT_ColorSpace>();
        var commonData = _document.CommonData;
        if (commonData is null) return _colorSpaceMap;

        foreach (var locStr in commonData.PublicResList)
            LoadColorSpacesFromRes(locStr.Value);
        foreach (var locStr in commonData.DocumentResList)
            LoadColorSpacesFromRes(locStr.Value);

        return _colorSpaceMap;
    }

    private void LoadColorSpacesFromRes(string resPath)
    {
        string fileName = System.IO.Path.GetFileName(resPath);
        var resXml = _docDir.GetFile(fileName);
        if (resXml is null) return;

        var res = new Res(resXml);
        foreach (var cs in res.GetColorSpaces())
        {
            if (cs.ID is not null)
                _colorSpaceMap![(long)cs.ID] = cs;
        }
    }

    /// <summary>
    /// 按 ID 获取颜色空间；ID 为 null 时返回 null（调用方按默认 RGB 处理）。
    /// </summary>
    public CT_ColorSpace? GetColorSpace(STRefID? id)
    {
        if (id is null) return null;
        GetColorSpaceMap().TryGetValue((long)id, out var cs);
        return cs;
    }

    // ── 绘制参数资源（DrawParam：透明度 / 可见性 / 混合模式）──

    /// <summary>获取所有绘制参数（ID → CT_DrawParam）</summary>
    public Dictionary<long, CT_DrawParam> GetDrawParamMap()
    {
        if (_drawParamMap is not null) return _drawParamMap;

        _drawParamMap = new Dictionary<long, CT_DrawParam>();
        var commonData = _document.CommonData;
        if (commonData is null) return _drawParamMap;

        foreach (var locStr in commonData.PublicResList)
            LoadDrawParamsFromRes(locStr.Value);
        foreach (var locStr in commonData.DocumentResList)
            LoadDrawParamsFromRes(locStr.Value);

        return _drawParamMap;
    }

    private void LoadDrawParamsFromRes(string resPath)
    {
        string fileName = System.IO.Path.GetFileName(resPath);
        var resXml = _docDir.GetFile(fileName);
        if (resXml is null) return;

        var res = new Res(resXml);
        foreach (var dp in res.GetDrawParams())
        {
            if (long.TryParse(dp.Id, out var id))
                _drawParamMap![id] = dp;
        }
    }

    /// <summary>
    /// 按 ID 获取绘制参数；ID 为 null 时返回 null。
    /// </summary>
    public CT_DrawParam? GetDrawParam(STRefID? id)
    {
        if (id is null) return null;
        GetDrawParamMap().TryGetValue((long)id, out var dp);
        return dp;
    }

    /// <summary>
    /// 按 ID 解析绘制参数并沿 ParentID 链合并继承属性（GB/T 33190 §8.1.2）。
    /// 返回的合并结果不写入资源池。
    /// </summary>
    public CT_DrawParam? GetDrawParamResolved(STRefID? id)
        => DrawParamResolver.Resolve(GetDrawParamMap().Values, id);

    // ── 矢量图像资源（CompositeGraphicUnit：复合对象引用）─────

    /// <summary>获取所有矢量图像资源（ID → CT_VectorG）</summary>
    public Dictionary<long, CT_VectorG> GetCompositeGraphicUnitMap()
    {
        if (_compositeMap is not null) return _compositeMap;

        _compositeMap = new Dictionary<long, CT_VectorG>();
        var commonData = _document.CommonData;
        if (commonData is null) return _compositeMap;

        foreach (var locStr in commonData.PublicResList)
            LoadCompositesFromRes(locStr.Value);
        foreach (var locStr in commonData.DocumentResList)
            LoadCompositesFromRes(locStr.Value);

        return _compositeMap;
    }

    private void LoadCompositesFromRes(string resPath)
    {
        string fileName = System.IO.Path.GetFileName(resPath);
        var resXml = _docDir.GetFile(fileName);
        if (resXml is null) return;

        var res = new Res(resXml);
        foreach (var vg in res.GetCompositeGraphicUnits())
        {
            if (vg.ID is not null)
                _compositeMap![(long)vg.ID] = vg;
        }
    }

    /// <summary>
    /// 按 ID 获取矢量图像资源（复合对象 <c>CompositeObject</c> 经 <c>ResourceID</c> 引用）；
    /// ID 为 null 或不存在时返回 null。
    /// </summary>
    public CT_VectorG? GetCompositeGraphicUnit(STRefID? id)
    {
        if (id is null) return null;
        GetCompositeGraphicUnitMap().TryGetValue((long)id, out var vg);
        return vg;
    }
}
