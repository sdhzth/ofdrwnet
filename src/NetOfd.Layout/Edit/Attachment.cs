using NetOfd.Core.Attachment;
using NetOfd.Core.BasicType;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 附件对象构造器。
/// <para>
/// 同名附件默认会被替换（<see cref="SetDisableReplace"/> 可禁用）。
/// </para>
/// 对应 Java: org.ofdrw.layout.edit.Attachment
/// </summary>
public class Attachment
{
    /// <summary>附件文件路径</summary>
    public string? FilePath { get; private set; }

    /// <summary>附件对象（CT_Attachment）</summary>
    public CTAttachment AttachmentObj { get; } = new();

    /// <summary>是否禁用同名替换，默认 false（同名替换）</summary>
    public bool DisableReplace { get; private set; }

    /// <summary>创建附件对象，同名文件将被替换。</summary>
    /// <param name="name">附件名称（必填）</param>
    /// <param name="filePath">附件文件路径（必填，须存在）</param>
    public Attachment(string name, string filePath) : this(name, filePath, false) { }

    /// <summary>创建附件对象。</summary>
    /// <param name="name">附件名称（必填）</param>
    /// <param name="filePath">附件文件路径（必填，须存在）</param>
    /// <param name="disableReplace">是否禁用同名替换，默认 false（同名替换）</param>
    public Attachment(string name, string filePath, bool disableReplace)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            throw new ArgumentException("附件文件(file)为空或不存在", nameof(filePath));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("附件名称(name)不能为空", nameof(name));

        FilePath = filePath;
        DisableReplace = disableReplace;
        SetName(name);
    }

    /// <summary>【必填属性】设置附件名称。</summary>
    public Attachment SetName(string attachmentName)
    {
        if (string.IsNullOrWhiteSpace(attachmentName))
            throw new ArgumentException("附件名称(name)不能为空", nameof(attachmentName));
        AttachmentObj.Name = attachmentName;
        return this;
    }

    /// <summary>获取附件名称。</summary>
    public string GetName() => AttachmentObj.Name;

    /// <summary>【可选属性】设置附件格式。</summary>
    public Attachment SetFormat(string format)
    {
        AttachmentObj.Format = format;
        return this;
    }

    /// <summary>获取附件格式。</summary>
    public string? GetFormat() => AttachmentObj.Format;

    /// <summary>【可选属性】设置创建时间。</summary>
    public Attachment SetCreationDate(DateTime creationDate)
    {
        AttachmentObj.CreationDate = creationDate.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        return this;
    }

    /// <summary>获取创建时间文本。</summary>
    public string? GetCreationDate() => AttachmentObj.CreationDate;

    /// <summary>【可选属性】设置修改时间。</summary>
    public Attachment SetModDate(DateTime modDate)
    {
        AttachmentObj.ModDate = modDate.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        return this;
    }

    /// <summary>获取修改时间文本。</summary>
    public string? GetModDate() => AttachmentObj.ModDate;

    /// <summary>【可选属性】设置附件是否可见，默认 true。</summary>
    public Attachment SetVisible(bool visible)
    {
        AttachmentObj.Visible = visible;
        return this;
    }

    /// <summary>获取附件是否可见。</summary>
    public bool GetVisible() => AttachmentObj.Visible;

    /// <summary>【可选属性】设置附件用途，默认 none。</summary>
    public Attachment SetUsage(string usage)
    {
        AttachmentObj.Usage = usage;
        return this;
    }

    /// <summary>获取附件用途。</summary>
    public string GetUsage() => AttachmentObj.Usage;

    /// <summary>【可选属性】设置附件摘要（自由文本，描述内容要点）。</summary>
    public Attachment SetSummary(string summary)
    {
        AttachmentObj.Summary = summary;
        return this;
    }

    /// <summary>获取附件摘要。</summary>
    public string? GetSummary() => AttachmentObj.Summary;

    /// <summary>【可选属性】设置替代附件引用（指向可替代本附件的另一附件 ID）。</summary>
    public Attachment SetSubstitutional(string id)
    {
        if (!string.IsNullOrWhiteSpace(id))
            AttachmentObj.Substitutional = STRefID.Parse(id);
        return this;
    }

    /// <summary>获取替代附件引用。</summary>
    public STRefID? GetSubstitutional() => AttachmentObj.Substitutional;

    /// <summary>设置是否禁用同名替换，默认 false（同名替换）。</summary>
    public Attachment SetDisableReplace(bool disableReplace)
    {
        DisableReplace = disableReplace;
        return this;
    }

    /// <summary>获取内部 CT_Attachment 对象。</summary>
    public CTAttachment GetAttachment() => AttachmentObj;
}
