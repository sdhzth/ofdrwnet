using System;
using System.Collections.Generic;
using System.IO;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.Resources;
using NetOfd.Pkg.Container;

namespace NetOfd.Graphics2D;

/// <summary>
/// OFD 图形文档（替代 Java OFDGraphicsDocument）
/// 通过 AWT Graphics2D API 创建 OFD 文档。
/// </summary>
public class OFDGraphicsDocument : IDisposable
{
    private readonly string? _outPath;
    private readonly Stream? _outStream;
    private bool _closed = false;
    private long _maxUnitId = 0;

    /// <summary>虚拟容器根目录</summary>
    internal OFDDir OfdDir { get; }

    /// <summary>文档子目录</summary>
    internal DocDir DocDir { get; }

    /// <summary>资源管理器</summary>
    internal ResManagerG2D ResMgr { get; }

    /// <summary>创建 OFD 图形文档，输出到文件</summary>
    public OFDGraphicsDocument(string outPath)
    {
        _outPath = outPath ?? throw new ArgumentNullException(nameof(outPath));
        OfdDir = OFDDir.NewOFD();
        DocDir = OfdDir.ObtainDocDir("Doc_0");
        ResMgr = new ResManagerG2D(this);
        InitDoc();
    }

    /// <summary>创建 OFD 图形文档，输出到流</summary>
    public OFDGraphicsDocument(Stream outStream)
    {
        _outStream = outStream ?? throw new ArgumentNullException(nameof(outStream));
        OfdDir = OFDDir.NewOFD();
        DocDir = OfdDir.ObtainDocDir("Doc_0");
        ResMgr = new ResManagerG2D(this);
        InitDoc();
    }

    private void InitDoc()
    {
        // 初始化文档默认 A4 大小
        var doc = DocDir.ObtainDocument();
        var cd = doc.ObtainCommonData();
        cd.PageArea = new CT_PageArea
        {
            PhysicalBox = new STBox(0, 0, 210, 297),
            ApplicationBox = new STBox(0, 0, 210, 297)
        };
        DocDir.SetDocument(doc);
    }

    /// <summary>创建新页面（默认 210×297mm A4）</summary>
    public OFDGraphics2D NewPage(double width = 210, double height = 297)
    {
        // 创建新 Page 目录（Page_0、Page_1 …）
        var pageDir = DocDir.ObtainNewPage();

        // 创建 PageObject，设置页面大小
        var pageObj = new PageObject();
        pageObj.Area = new CT_PageArea
        {
            PhysicalBox = new STBox(0, 0, width, height),
            ApplicationBox = new STBox(0, 0, width, height)
        };
        pageDir.SetContent(pageObj);

        // 将页面引用写入 Document.xml 的 Pages 列表
        var doc = DocDir.ObtainDocument();
        var pagesList = doc.ObtainPages();

        var pageId = NewId();
        // pageDir.Name = "Page_0" / "Page_1" …
        var pageLoc = $"Pages/{pageDir.Name}/Content.xml";
        var pageRef = new Page
        {
            ID = new STID(pageId),
            BaseLoc = new STLoc(pageLoc)
        };
        pagesList.AddPage(pageRef);
        DocDir.SetDocument(doc);

        // 创建图形绘制上下文（图层实时写入 pageDir 内部的 PageObject）
        var g2d = new OFDGraphics2D(this, pageDir, width, height);
        return g2d;
    }

    /// <summary>生成新对象 ID（线程安全不是目标，单线程使用）</summary>
    public long NewId() => ++_maxUnitId;

    /// <summary>注册字体</summary>
    internal STRefID RegisterFont(string fontName) => ResMgr.RegisterFont(fontName);

    /// <summary>添加图片资源</summary>
    public OFDMedia AddImage(string filePath) => ResMgr.AddImage(filePath);

    /// <summary>添加绘制参数</summary>
    public STRefID AddDrawParam(CT_DrawParam dp) => ResMgr.AddDrawParam(dp);

    /// <summary>关闭文档并写出 OFD 文件</summary>
    public void Close()
    {
        if (_closed) return;
        _closed = true;

        // 更新 MaxUnitID
        var doc = DocDir.ObtainDocument();
        doc.ObtainCommonData().MaxUnitID = new STID(_maxUnitId);
        DocDir.SetDocument(doc);

        // 写 OFD.xml 根
        var ofdRoot = OfdDir.ObtainOfd();
        var docBody = ofdRoot.ObtainDocBody();
        docBody.DocRoot = new STLoc("Doc_0/Document.xml");
        OfdDir.SetOfd(ofdRoot);

        // 打包输出
        if (_outPath != null)
            OfdDir.Jar(_outPath);
        else if (_outStream != null)
            OfdDir.Jar(_outStream);
    }

    public void Dispose() => Close();

    /// <summary>快捷创建文档</summary>
    public static OFDGraphicsDocument Create(string outPath) => new(outPath);
}

// ──────────────────────────────────────────────────────────
//  OFDMedia - 媒体资源描述
// ──────────────────────────────────────────────────────────
/// <summary>OFD 媒体资源描述</summary>
public class OFDMedia
{
    public long Id { get; internal set; }
    public string FileName { get; internal set; } = "";
    public string Format { get; internal set; } = "PNG";
    public int Width { get; internal set; }
    public int Height { get; internal set; }

    public STRefID RefID => new STRefID(Id);
}

// ──────────────────────────────────────────────────────────
//  ResManagerG2D - 资源管理器（内部）
// ──────────────────────────────────────────────────────────
internal class ResManagerG2D
{
    private readonly OFDGraphicsDocument _doc;
    private readonly Dictionary<string, STRefID> _fontCache = new();
    private readonly Dictionary<string, long> _mediaCache = new();
    private readonly Dictionary<int, STRefID> _drawParamCache = new();

    public ResManagerG2D(OFDGraphicsDocument doc) { _doc = doc; }

    public STRefID RegisterFont(string fontName)
    {
        if (_fontCache.TryGetValue(fontName, out var existing))
            return existing;

        var font = new Core.PageDescription.Font.CT_Font
        {
            FontName = fontName,
            FamilyName = fontName
        };
        var resId = _doc.NewId();
        font.ID = new STID(resId);

        var pubRes = _doc.DocDir.ObtainPublicRes();
        pubRes.AddFont(font);
        _doc.DocDir.SetPublicRes(pubRes);

        var fontRef = new STRefID(resId);
        _fontCache[fontName] = fontRef;
        return fontRef;
    }

    public OFDMedia AddImage(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        if (_mediaCache.TryGetValue(fileName, out var existingId))
            return new OFDMedia { Id = existingId, FileName = fileName };

        if (!File.Exists(filePath))
            throw new FileNotFoundException("图片文件不存在", filePath);

        var imgBytes = File.ReadAllBytes(filePath);
        var resDir = _doc.DocDir.ObtainRes();
        resDir.AddResource(fileName, imgBytes);

        var resId = _doc.NewId();
        _mediaCache[fileName] = resId;

        // 读取图片尺寸（不依赖 System.Drawing）
        var (w, h) = ReadPngSize(imgBytes);

        // 注册到资源 XML
        var mm = new CT_MultiMedia
        {
            ID = new STID(resId),
            Type = "Image",
            Format = Path.GetExtension(filePath).TrimStart('.').ToUpperInvariant(),
            MediaFile = new STLoc(fileName)
        };
        var docRes = _doc.DocDir.ObtainDocumentRes();
        docRes.AddMultiMedia(mm);
        _doc.DocDir.SetDocumentRes(docRes);

        return new OFDMedia
        {
            Id = resId,
            FileName = fileName,
            Format = mm.Format ?? "PNG",
            Width = w,
            Height = h
        };
    }

    public STRefID AddDrawParam(CT_DrawParam dp)
    {
        var key = dp.GetHashCode();
        if (_drawParamCache.TryGetValue(key, out var existing)) return existing;
        var resId = _doc.NewId();
        var refId = new STRefID(resId);
        _drawParamCache[key] = refId;
        return refId;
    }

    /// <summary>读取 PNG 图片宽高（大端 offset=16/20）</summary>
    private static (int w, int h) ReadPngSize(byte[] data)
    {
        if (data.Length < 24) return (0, 0);
        // PNG magic: 89 50 4E 47 0D 0A 1A 0A
        if (data[0] != 0x89 || data[1] != 0x50) return (0, 0);
        int w = (data[16] << 24) | (data[17] << 16) | (data[18] << 8) | data[19];
        int h = (data[20] << 24) | (data[21] << 16) | (data[22] << 8) | data[23];
        return (w, h);
    }
}
