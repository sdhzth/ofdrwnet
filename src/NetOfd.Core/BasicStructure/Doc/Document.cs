using System.Xml.Linq;
using NetOfd.Core.Action;
using NetOfd.Core.BasicStructure.Doc.Bookmark;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.Doc;

/// <summary>
/// Document.xml 根元素。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.Document
/// </summary>
public class Document : OFDElement
{
    public Document() : base("Document") { }
    public Document(XElement element) : base(element) { }

    /// <summary>公共数据</summary>
    public CTCommonData? CommonData
    {
        get
        {
            var e = GetChildElement("CommonData");
            return e is null ? null : new CTCommonData(e);
        }
        set
        {
            if (value is null) GetChildElement("CommonData")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建 CommonData</summary>
    public CTCommonData ObtainCommonData()
    {
        var cd = CommonData;
        if (cd is not null) return cd;
        cd = new CTCommonData();
        SetOFDEntity(cd);
        return cd;
    }

    /// <summary>页面树</summary>
    public PageTree.Pages? Pages
    {
        get
        {
            var e = GetChildElement("Pages");
            return e is null ? null : new PageTree.Pages(e);
        }
        set
        {
            if (value is null) GetChildElement("Pages")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建 Pages</summary>
    public PageTree.Pages ObtainPages()
    {
        var pages = Pages;
        if (pages is not null) return pages;
        pages = new PageTree.Pages();
        SetOFDEntity(pages);
        return pages;
    }

    /// <summary>大纲（可选）</summary>
    public NetOfd.Core.BasicStructure.Outlines.Outlines? Outlines
    {
        get
        {
            var e = GetChildElement("Outlines");
            return e is null ? null : new NetOfd.Core.BasicStructure.Outlines.Outlines(e);
        }
        set
        {
            if (value is null) GetChildElement("Outlines")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建 Outlines</summary>
    public NetOfd.Core.BasicStructure.Outlines.Outlines ObtainOutlines()
    {
        var o = Outlines;
        if (o is not null) return o;
        o = new NetOfd.Core.BasicStructure.Outlines.Outlines();
        SetOFDEntity(o);
        return o;
    }

    /// <summary>书签集（可选）</summary>
    public Bookmarks? Bookmarks
    {
        get
        {
            var e = GetChildElement("Bookmarks");
            return e is null ? null : new Bookmarks(e);
        }
        set
        {
            if (value is null) GetChildElement("Bookmarks")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>获取或创建 Bookmarks</summary>
    public Bookmarks ObtainBookmarks()
    {
        var b = Bookmarks;
        if (b is not null) return b;
        b = new Bookmarks();
        SetOFDEntity(b);
        return b;
    }

    /// <summary>文档级动作序列（可选）</summary>
    public Actions? Actions
    {
        get
        {
            var e = GetChildElement("Actions");
            return e is null ? null : new Actions(e);
        }
        set
        {
            if (value is null) GetChildElement("Actions")?.Remove();
            else SetOFDEntity(value);
        }
    }

    /// <summary>附件列表文件路径（可选）</summary>
    public STLoc? Attachments
    {
        get
        {
            var v = GetChildText("Attachments");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("Attachments", value?.Value);
    }

    /// <summary>注解列表文件路径（可选）</summary>
    public STLoc? Annotations
    {
        get
        {
            var v = GetChildText("Annotations");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("Annotations", value?.Value);
    }

    /// <summary>自定义标引列表文件路径（可选）</summary>
    public STLoc? CustomTags
    {
        get
        {
            var v = GetChildText("CustomTags");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("CustomTags", value?.Value);
    }
}
