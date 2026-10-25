using System.Xml.Linq;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.Resources;

namespace NetOfd.Pkg.Container;

/// <summary>
/// 文档容器（Doc_N 目录）。
/// 包含：Document.xml、Pages、Res、Signs、Annotations 等子目录。
/// 对应 Java: org.ofdrw.pkg.container.DocDir
/// </summary>
public class DocDir : VirtualContainer
{
    public const string DOCUMENT_FILE = "Document.xml";
    public const string PAGES_DIR = "Pages";
    public const string RES_DIR = "Res";
    public const string SIGNS_DIR = "Signs";
    public const string ANNOTS_DIR = "Annots";
    public const string TEMPS_DIR = "Temps";
    public const string ANNOTATIONS_FILE = "Annotations.xml";
    public const string PUBLIC_RES_FILE = "PublicRes.xml";
    public const string DOCUMENT_RES_FILE = "DocumentRes.xml";

    public DocDir(string name, string? physicalPath = null) : base(name, physicalPath) { }

    // ────────────────────────────────────────────────────────
    //  Document.xml
    // ────────────────────────────────────────────────────────
    public void SetDocument(Document doc)
    {
        PutFile(DOCUMENT_FILE, doc.Element);
    }

    public Document? GetDocument()
    {
        var el = GetFile(DOCUMENT_FILE);
        return el is null ? null : new Document(el);
    }

    public Document ObtainDocument()
    {
        var doc = GetDocument();
        if (doc is not null) return doc;
        doc = new Document();
        SetDocument(doc);
        return doc;
    }

    // ────────────────────────────────────────────────────────
    //  PublicRes.xml
    // ────────────────────────────────────────────────────────
    public void SetPublicRes(Res res)
    {
        PutFile(PUBLIC_RES_FILE, res.Element);
    }

    public Res? GetPublicRes()
    {
        var el = GetFile(PUBLIC_RES_FILE);
        return el is null ? null : new Res(el);
    }

    public Res ObtainPublicRes()
    {
        var res = GetPublicRes();
        if (res is not null) return res;
        res = new Res();
        SetPublicRes(res);
        return res;
    }

    // ────────────────────────────────────────────────────────
    //  DocumentRes.xml
    // ────────────────────────────────────────────────────────
    public void SetDocumentRes(Res res)
    {
        PutFile(DOCUMENT_RES_FILE, res.Element);
    }

    public Res? GetDocumentRes()
    {
        var el = GetFile(DOCUMENT_RES_FILE);
        return el is null ? null : new Res(el);
    }

    public Res ObtainDocumentRes()
    {
        var res = GetDocumentRes();
        if (res is not null) return res;
        res = new Res();
        SetDocumentRes(res);
        return res;
    }

    // ────────────────────────────────────────────────────────
    //  Pages 子目录
    // ────────────────────────────────────────────────────────
    public PagesDir ObtainPages()
    {
        if (DirCache.TryGetValue(PAGES_DIR, out var existing) && existing is PagesDir pd)
            return pd;

        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, PAGES_DIR) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var pagesDir = new PagesDir(PAGES_DIR, childPhys);
        pagesDir.Parent = this;
        DirCache[PAGES_DIR] = pagesDir;
        return pagesDir;
    }

    // ────────────────────────────────────────────────────────
    //  Res 子目录
    // ────────────────────────────────────────────────────────
    public ResDir ObtainRes()
    {
        if (DirCache.TryGetValue(RES_DIR, out var existing) && existing is ResDir rd)
            return rd;

        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, RES_DIR) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var resDir = new ResDir(RES_DIR, childPhys);
        resDir.Parent = this;
        DirCache[RES_DIR] = resDir;
        return resDir;
    }

    // ────────────────────────────────────────────────────────
    //  Signs 子目录（签名）
    // ────────────────────────────────────────────────────────
    public SignsDir ObtainSigns()
    {
        if (DirCache.TryGetValue(SIGNS_DIR, out var existing) && existing is SignsDir sd)
            return sd;

        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, SIGNS_DIR) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var signsDir = new SignsDir(SIGNS_DIR, childPhys);
        signsDir.Parent = this;
        DirCache[SIGNS_DIR] = signsDir;
        return signsDir;
    }

    // ────────────────────────────────────────────────────────
    //  Annots 子目录（注释）
    // ────────────────────────────────────────────────────────
    public AnnotsDir ObtainAnnots()
    {
        if (DirCache.TryGetValue(ANNOTS_DIR, out var existing) && existing is AnnotsDir ad)
            return ad;

        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, ANNOTS_DIR) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var annotsDir = new AnnotsDir(ANNOTS_DIR, childPhys);
        annotsDir.Parent = this;
        DirCache[ANNOTS_DIR] = annotsDir;
        return annotsDir;
    }

    // ────────────────────────────────────────────────────────
    //  Temps 子目录（模板页）
    // ────────────────────────────────────────────────────────
    public TempsDir ObtainTemps()
    {
        if (DirCache.TryGetValue(TEMPS_DIR, out var existing) && existing is TempsDir td)
            return td;

        string? childPhys = PhysicalPath is not null ? Path.Combine(PhysicalPath, TEMPS_DIR) : null;
        if (childPhys is not null && !Directory.Exists(childPhys)) childPhys = null;
        var tempsDir = new TempsDir(TEMPS_DIR, childPhys);
        tempsDir.Parent = this;
        DirCache[TEMPS_DIR] = tempsDir;
        return tempsDir;
    }

    // ────────────────────────────────────────────────────────
    //  Pages 操作
    // ────────────────────────────────────────────────────────
    /// <summary>获取文档总页数</summary>
    public int GetPagesCount() => ObtainPages().GetPagesCount();

    /// <summary>创建并获取新页面目录</summary>
    public PageDir ObtainNewPage() => ObtainPages().ObtainNewPage();

    /// <summary>获取最后一个页面目录</summary>
    public PageDir? GetLatestPage() => ObtainPages().GetLatestPage();
}
