using System.Linq;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.Action;
using NetOfd.Core.Action.ActionType;
using NetOfd.Core.Action.ActionType.ActionGoto;
using NetOfd.Core.Action.ActionType.ActionMovie;
using NetOfd.Core.BasicType;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 动作（Action）子系统测试：Goto/GotoA/URI/Sound/Movie 建模 + 序列化往返 + 工厂分发。
/// 对齐 Java: org.ofdrw.core.action（GB/T 33190 图 73-79）。
/// </summary>
public class ActionTest
{
    private static XElement RoundTrip(OFDElement e)
    {
        return XElement.Parse(e.Element.ToString());
    }

    // ── GotoA 附件动作（图 76）──

    [Fact]
    public void GotoA_AttachIDAndNewWindow_RoundTrip()
    {
        var a = new GotoA("att_1", true);
        Assert.Equal("att_1", a.AttachID);
        Assert.True(a.NewWindow);

        var rt = new GotoA(RoundTrip(a));
        Assert.Equal("att_1", rt.AttachID);
        Assert.True(rt.NewWindow);
        Assert.IsType<GotoA>(OFDAction.GetInstance(rt.Element));
    }

    [Fact]
    public void GotoA_NewWindow_Optional_DefaultNull()
    {
        var a = new GotoA("att_2");
        Assert.Equal("att_2", a.AttachID);
        Assert.Null(a.NewWindow);

        var rt = new GotoA(RoundTrip(a));
        Assert.Null(rt.NewWindow);
    }

    // ── URI 动作（图 77）──

    [Fact]
    public void URI_ValueAndBase_RoundTrip()
    {
        var u = new URI("https://example.com/doc.ofd", "https://example.com/");
        Assert.Equal("https://example.com/doc.ofd", u.UriValue);
        Assert.Equal("https://example.com/", u.Base);

        var rt = new URI(RoundTrip(u));
        Assert.Equal("https://example.com/doc.ofd", rt.UriValue);
        Assert.Equal("https://example.com/", rt.Base);
        Assert.IsType<URI>(OFDAction.GetInstance(rt.Element));
    }

    [Fact]
    public void URI_SetBaseNull_RemovesAttribute()
    {
        var u = new URI("a.html", "https://example.com/");
        Assert.Equal("https://example.com/", u.Base);
        u.SetBase(null);
        Assert.Null(u.Base);
        Assert.Null(u.Element.Attribute("Base"));
    }

    // ── Sound 播放音频动作（图 78）──

    [Fact]
    public void Sound_ResourceID_Volume_RoundTrip()
    {
        var s = new Sound(new STRefID(42)).SetVolume(80);
        Assert.Equal(42L, s.ResourceID!.Value);
        Assert.Equal(80, s.Volume);

        var rt = new Sound(RoundTrip(s));
        Assert.Equal(42L, rt.ResourceID!.Value);
        Assert.Equal(80, rt.Volume);
        Assert.False(rt.Repeat);   // 默认 false（未设置）
        Assert.False(rt.Synchronous); // 默认 false（未设置）
        Assert.IsType<Sound>(OFDAction.GetInstance(rt.Element));
    }

    [Fact]
    public void Sound_Volume_ClampedLikeJava()
    {
        // 对齐 Java setVolume：max(v,0) 后对 100 取模
        Assert.Equal(0, new Sound(new STRefID(1)).SetVolume(100).Volume);
        Assert.Equal(0, new Sound(new STRefID(1)).SetVolume(-5).Volume);
        Assert.Equal(15, new Sound(new STRefID(1)).SetVolume(115).Volume);
    }

    [Fact]
    public void Sound_Repeat_Synchronous_RoundTrip()
    {
        var s = new Sound(new STRefID(7)).SetRepeat(true).SetSynchronous(true);
        Assert.True(s.Repeat);
        Assert.True(s.Synchronous);

        var rt = new Sound(RoundTrip(s));
        Assert.True(rt.Repeat);
        Assert.True(rt.Synchronous);
    }

    // ── Movie 播放视频动作（图 79）──

    [Fact]
    public void Movie_ResourceID_Operator_RoundTrip()
    {
        var m = new Movie(new STRefID(9)).SetOperator(PlayType.Pause);
        Assert.Equal(9L, m.ResourceID!.Value);
        Assert.Equal(PlayType.Pause, m.Operator);

        var rt = new Movie(RoundTrip(m));
        Assert.Equal(9L, rt.ResourceID!.Value);
        Assert.Equal(PlayType.Pause, rt.Operator);
        Assert.IsType<Movie>(OFDAction.GetInstance(rt.Element));
    }

    [Fact]
    public void Movie_Operator_DefaultNull_StringParsing()
    {
        var m = new Movie("3");
        Assert.Equal(3L, m.ResourceID!.Value);
        Assert.Null(m.Operator);

        // 大小写不敏感解析（对齐 Java PlayType.getInstance）
        Assert.Equal(PlayType.Resume, "resume".ToPlayType());
        Assert.Throws<ArgumentException>(() => "rewind".ToPlayType());
    }

    // ── CT_Action 容器（图 73）+ 工厂分发 ──

    [Fact]
    public void CTAction_HoldsEachActionType_FactoryDispatches()
    {
        // 每种具体动作经 CT_Action 包装 → 序列化 → GetAction 反解，类型与属性不丢失
        var cases = new List<IOFDAction>
        {
            new Goto(new CT_Dest { Type = DestType.Fit, PageID = new STRefID(2) }),
            new GotoA("att_9", false),
            new URI("https://example.com/"),
            new Sound(new STRefID(5)).SetVolume(30),
            new Movie(new STRefID(6)).SetOperator(PlayType.Play),
        };

        foreach (var action in cases)
        {
            var ct = new CT_Action(EventType.DO, (OFDElement)action);
            var rt = new CT_Action(ct.Element);
            var parsed = rt.GetAction();
            Assert.NotNull(parsed);
            Assert.IsType(action.GetType(), parsed);
            Assert.Equal(((OFDElement)action).Element.Name.LocalName, ((OFDElement)parsed).Element.Name.LocalName);
        }
    }

    [Fact]
    public void CTAction_SetAction_ReplacesExistingChild()
    {
        var ct = new CT_Action(EventType.DO, new URI("https://a.com/"));
        ct.SetAction(new Sound(new STRefID(1)));
        var children = ct.Element.Elements().ToList();
        Assert.Single(children);
        Assert.Equal("Sound", children[0].Name.LocalName);
    }
}
