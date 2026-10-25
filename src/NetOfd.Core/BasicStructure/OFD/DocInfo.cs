using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.OFD;

/// <summary>
/// 文档元数据（DocInfo）。
/// 对应 Java: org.ofdrw.core.basicStructure.ofd.docInfo.DocInfo
/// </summary>
public class DocInfo : OFDElement
{
    public DocInfo() : base("DocInfo") { }
    public DocInfo(XElement element) : base(element) { }

    public string? DocID
    {
        get => GetChildText("DocID");
        set => SetChildText("DocID", value);
    }

    public string? Title
    {
        get => GetChildText("Title");
        set => SetChildText("Title", value);
    }

    public string? Author
    {
        get => GetChildText("Author");
        set => SetChildText("Author", value);
    }

    public string? Subject
    {
        get => GetChildText("Subject");
        set => SetChildText("Subject", value);
    }

    public string? Abstract
    {
        get => GetChildText("Abstract");
        set => SetChildText("Abstract", value);
    }

    public string? CreationDate
    {
        get => GetChildText("CreationDate");
        set => SetChildText("CreationDate", value);
    }

    public string? ModDate
    {
        get => GetChildText("ModDate");
        set => SetChildText("ModDate", value);
    }

    public string? Creator
    {
        get => GetChildText("Creator");
        set => SetChildText("Creator", value);
    }

    public string? CreatorVersion
    {
        get => GetChildText("CreatorVersion");
        set => SetChildText("CreatorVersion", value);
    }

    /// <summary>
    /// 最大对象 ID（GB/T 33190-2016 第 7.1 节，DocInfo 可选子元素）。
    /// <para>对齐 Java: org.ofdrw.core.basicStructure.ofd.docInfo.DocInfo#setMaxUnitID</para>
    /// </summary>
    public STID? MaxUnitID
    {
        get
        {
            var v = GetChildText("MaxUnitID");
            return v is null ? null : new STID(STBase.ToLong(v));
        }
        set => SetChildText("MaxUnitID", value?.Value.ToString());
    }

    /// <summary>
    /// 文档分类；默认 <see cref="DocUsage.Normal"/>（表 4 文档元数据属性）。
    /// 对应 Java: CT_DocInfo#getDocUsage
    /// </summary>
    public DocUsage DocUsage
    {
        get => GetChildText("DocUsage").ToDocUsage();
        set => SetChildText("DocUsage", value.ToString());
    }

    /// <summary>
    /// 文档封面，指向包内一个图片文件的路径。
    /// 对应 Java: CT_DocInfo#getCover
    /// </summary>
    public STLoc? Cover
    {
        get
        {
            var v = GetChildText("Cover");
            return string.IsNullOrWhiteSpace(v) ? null : new STLoc(v);
        }
        set => SetChildText("Cover", value?.Value);
    }

    /// <summary>
    /// 关键词集合；不存在时返回 null。
    /// 对应 Java: CT_DocInfo#getKeywords
    /// </summary>
    public Keywords? GetKeywords()
    {
        var e = GetChildElement("Keywords");
        return e is null ? null : new Keywords(e);
    }

    /// <summary>设置关键词集合（替换已有的）。对应 Java: CT_DocInfo#setKeywords</summary>
    public DocInfo SetKeywords(Keywords keywords)
    {
        if (keywords is not null) SetOFDEntity(keywords);
        return this;
    }

    /// <summary>添加关键词；不存在 Keywords 节点时自动创建。对应 Java: CT_DocInfo#addKeyword</summary>
    public DocInfo AddKeyword(string keyword)
    {
        var kw = GetKeywords();
        if (kw is null)
        {
            kw = new Keywords();
            AddOFDEntity(kw);
        }
        kw.AddKeyword(keyword);
        return this;
    }

    /// <summary>
    /// 用户自定义元数据集合；不存在时返回 null。
    /// 对应 Java: CT_DocInfo#getCustomDatas
    /// </summary>
    public CustomDatas? GetCustomDatas()
    {
        var e = GetChildElement("CustomDatas");
        return e is null ? null : new CustomDatas(e);
    }

    /// <summary>设置用户自定义元数据集合（替换已有的）。对应 Java: CT_DocInfo#setCustomDatas</summary>
    public DocInfo SetCustomDatas(CustomDatas customDatas)
    {
        if (customDatas is not null) SetOFDEntity(customDatas);
        return this;
    }

    /// <summary>添加一个自定义元数据；不存在 CustomDatas 节点时自动创建。</summary>
    public DocInfo AddCustomData(string name, string value)
    {
        var cd = GetCustomDatas();
        if (cd is null)
        {
            cd = new CustomDatas();
            AddOFDEntity(cd);
        }
        cd.AddCustomData(name, value);
        return this;
    }

    /// <summary>
    /// 设置文件标识符（去掉连字符的 32 字符 UUID 形式）。
    /// 对应 Java: CT_DocInfo#setDocID
    /// </summary>
    public DocInfo SetDocID(Guid docID)
    {
        SetChildText("DocID", docID.ToString("N"));
        return this;
    }

    /// <summary>随机产生一个 UUID 作为文件标识符。对应 Java: CT_DocInfo#randomDocID</summary>
    public DocInfo RandomDocID() => SetDocID(Guid.NewGuid());
}
