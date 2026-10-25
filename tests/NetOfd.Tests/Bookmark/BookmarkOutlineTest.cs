using System.Xml.Linq;
using NetOfd.Core.Action;
using NetOfd.Core.Action.ActionType.ActionGoto;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.Doc.Bookmark;
using DocBookmark = NetOfd.Core.BasicStructure.Doc.Bookmark.Bookmark;
using ActionBookmark = NetOfd.Core.Action.ActionType.ActionGoto.Bookmark;
using NetOfd.Core.BasicStructure.Outlines;
using NetOfd.Core.BasicType;
using Xunit;

namespace NetOfd.Tests.Bookmark;

/// <summary>
/// 书签（Bookmarks/Bookmark）与大纲（Outlines/CT_OutlineElem）及其动作（Goto）的序列化往返测试。
/// 参考 ofdrw-core 的 BookmarksTest / OutlinesTest。
/// </summary>
public class BookmarkOutlineTest
{
    [Fact]
    public void Bookmarks_SerializeRoundTrip()
    {
        var doc = new Document();
        var dest = new CT_Dest
        {
            Type = DestType.FitR,
            PageID = new STRefID(5),
            Left = 10,
            Top = 20,
            Right = 100,
            Bottom = 200,
            Zoom = 2
        };
        doc.ObtainBookmarks()
            .AddBookmark(new DocBookmark("第一章", dest))
            .AddBookmark(new DocBookmark("第二章", new CT_Dest { Type = DestType.XYZ, PageID = new STRefID(6) }));

        var doc2 = new Document(XElement.Parse(doc.ToString()));
        var bms = doc2.Bookmarks;
        Assert.NotNull(bms);
        var list = bms!.GetBookmarks();
        Assert.Equal(2, list.Count);

        Assert.Equal("第一章", list[0].BookmarkName);
        var d = list[0].Dest;
        Assert.NotNull(d);
        Assert.Equal(DestType.FitR, d!.Type);
        Assert.Equal(5L, (long)d.PageID!);
        Assert.Equal(10.0, d.Left);
        Assert.Equal(200.0, d.Bottom);
        Assert.Equal(2.0, d.Zoom);

        Assert.Equal("第二章", list[1].BookmarkName);
        Assert.Equal(DestType.XYZ, list[1].Dest!.Type);
    }

    [Fact]
    public void Outlines_SerializeRoundTrip_WithNesting()
    {
        var doc = new Document();
        var root = doc.ObtainOutlines();
        var ch1 = new CT_OutlineElem("第一章") { Count = 3, Expanded = true };
        ch1.AddOutlineElem(new CT_OutlineElem("1.1") { Count = 0, Expanded = false });
        ch1.AddOutlineElem(new CT_OutlineElem("1.2"));
        var ch2 = new CT_OutlineElem("第二章") { Expanded = false };
        root.AddOutlineElem(ch1).AddOutlineElem(ch2);

        var doc2 = new Document(XElement.Parse(doc.ToString()));
        var elems = doc2.Outlines!.GetOutlineElems();
        Assert.Equal(2, elems.Count);
        Assert.Equal("第一章", elems[0].Title);
        Assert.Equal(3, elems[0].Count);
        Assert.True(elems[0].Expanded);

        var sub = elems[0].GetOutlineElems();
        Assert.Equal(2, sub.Count);
        Assert.Equal("1.1", sub[0].Title);
        Assert.False(sub[0].Expanded);
        Assert.Equal("1.2", sub[1].Title);

        Assert.Equal("第二章", elems[1].Title);
        Assert.False(elems[1].Expanded);
    }

    [Fact]
    public void Outline_WithGotoAction_RoundTrip()
    {
        var outline = new CT_OutlineElem("跳转示例");
        var actions = new Actions();
        var action = new CT_Action(EventType.PO,
            new Goto(new CT_Dest { Type = DestType.Fit, PageID = new STRefID(2) }));
        actions.AddAction(action);
        outline.Actions = actions;

        var o2 = new CT_OutlineElem(XElement.Parse(outline.ToString()));
        Assert.NotNull(o2.Actions);
        var acts = o2.Actions!.GetActions();
        Assert.Single(acts);
        Assert.Equal(EventType.PO, acts[0].Event);

        var target = ((Goto)acts[0].GetAction()!).GetTarget();
        Assert.NotNull(target);
        var dest = Assert.IsType<CT_Dest>(target);
        Assert.Equal(DestType.Fit, dest.Type);
    }

    [Fact]
    public void Goto_SetBookmarkTarget()
    {
        var g = new Goto("myBookmark");
        var t = g.GetTarget();
        Assert.NotNull(t);
        var bm = Assert.IsType<ActionBookmark>(t);
        Assert.Equal("myBookmark", bm.BookmarkName);
    }

    [Fact]
    public void Document_WithoutBookmarksOrOutlines_ReturnsNull()
    {
        var doc = new Document();
        Assert.Null(doc.Bookmarks);
        Assert.Null(doc.Outlines);
    }
}
