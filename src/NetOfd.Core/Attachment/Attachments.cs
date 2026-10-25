using System.Xml.Linq;

namespace NetOfd.Core.Attachment;

/// <summary>
/// Attachments - 附件列表，定义于文档节点的 7.5 文档节点中。
/// 对应 Java: org.ofdrw.core.attachment.Attachments
/// </summary>
public class Attachments : OFDElement
{
    public Attachments() : base("Attachments") { }
    public Attachments(XElement element) : base(element) { }

    /// <summary>添加附件</summary>
    public Attachments AddAttachment(CTAttachment attachment)
    {
        if (attachment is not null) AddOFDEntity(attachment);
        return this;
    }

    /// <summary>获取附件列表</summary>
    public List<CTAttachment> GetAttachments()
        => GetChildElements("Attachment").Select(e => new CTAttachment(e)).ToList();
}