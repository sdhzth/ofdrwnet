using System.Xml.Linq;
using System.Collections.Generic;
using System.Linq;

namespace NetOfd.Core.BasicStructure.Doc.Bookmark;

/// <summary>
/// 文档的书签集，包含一组书签（7.5 文档根节点 表 5）。
/// 对应 Java: org.ofdrw.core.basicStructure.doc.bookmark.Bookmarks
/// </summary>
public class Bookmarks : OFDElement
{
    public Bookmarks() : base("Bookmarks") { }
    public Bookmarks(XElement element) : base(element) { }

    /// <summary>【必选】增加书签</summary>
    public Bookmarks AddBookmark(Bookmark bookmark)
    {
        AddOFDEntity(bookmark);
        return this;
    }

    /// <summary>【必选】获取书签列表</summary>
    public List<Bookmark> GetBookmarks() =>
        GetChildElements("Bookmark").Select(e => new Bookmark(e)).ToList();
}
