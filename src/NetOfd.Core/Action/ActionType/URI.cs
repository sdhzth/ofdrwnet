namespace NetOfd.Core.Action.ActionType;

/// <summary>
/// URI 动作（图 77 URI动作属性）。
/// 对应 Java: org.ofdrw.core.action.actionType.URI
/// </summary>
public class URI : OFDElement, IOFDAction
{
    public URI() : base("URI") { }
    public URI(System.Xml.Linq.XElement element) : base(element) { }

    public URI(string uri) : this()
    {
        SetURI(uri);
    }

    public URI(string uri, string? baseUri) : this()
    {
        SetURI(uri).SetBase(baseUri);
    }

    /// <summary>【必选 属性】目标URI的位置</summary>
    public string? UriValue
    {
        get => GetAttr("URI");
        set => SetAttr("URI", value);
    }

    public URI SetURI(string uri)
    {
        UriValue = uri;
        return this;
    }

    /// <summary>【可选 属性】Base URI，用于相对地址；null 表示移除</summary>
    public string? Base
    {
        get => GetAttr("Base");
        set => SetAttr("Base", value);
    }

    public URI SetBase(string? baseUri)
    {
        Base = baseUri;
        return this;
    }
}
