namespace NetOfd.Core.Action.ActionType;

/// <summary>
/// 附件动作，表明打开当前文档内的一个附件（图 76 附件动作结构）。
/// 对应 Java: org.ofdrw.core.action.actionType.GotoA
/// </summary>
public class GotoA : OFDElement, IOFDAction
{
    public GotoA() : base("GotoA") { }
    public GotoA(System.Xml.Linq.XElement element) : base(element) { }

    public GotoA(string attachId, bool newWindow) : this()
    {
        SetAttachID(attachId).SetNewWindow(newWindow);
    }

    public GotoA(string attachId) : this()
    {
        SetAttachID(attachId);
    }

    /// <summary>【必选 属性】附件的标识（xs:IDREF）</summary>
    public string? AttachID
    {
        get => GetAttr("AttachID");
        set => SetAttr("AttachID", value);
    }

    public GotoA SetAttachID(string attachId)
    {
        AttachID = attachId;
        return this;
    }

    /// <summary>【可选 属性】是否在新窗口中打开；null 表示未设置</summary>
    public bool? NewWindow
    {
        get
        {
            var str = GetAttr("NewWindow");
            if (string.IsNullOrWhiteSpace(str)) return null;
            return bool.Parse(str);
        }
        set => SetAttr("NewWindow", value is null ? null : value.ToString());
    }

    public GotoA SetNewWindow(bool newWindow)
    {
        NewWindow = newWindow;
        return this;
    }
}
