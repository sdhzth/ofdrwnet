using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Attachment;

/// <summary>
/// CT_Attachment - 附件。
/// 对应 Java: org.ofdrw.core.attachment.CT_Attachment
/// </summary>
public class CTAttachment : OFDElement
{
    public CTAttachment() : base("Attachment") { }
    public CTAttachment(XElement element) : base(element) { }

    /// <summary>附件标识（必填）</summary>
    public string ID
    {
        get => GetAttr("ID") ?? throw new InvalidOperationException("附件标识(ID)不能为空");
        set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("附件标识(ID)不能为空", nameof(value));
            SetAttr("ID", value);
        }
    }

    /// <summary>附件名称（必填）</summary>
    public string Name
    {
        get => GetAttr("Name") ?? throw new InvalidOperationException("附件名称(Name)不能为空");
        set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("附件名称(Name)不能为空", nameof(value));
            SetAttr("Name", value);
        }
    }

    /// <summary>附件格式（可选）</summary>
    public string? Format
    {
        get => GetAttr("Format");
        set => SetAttr("Format", string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>创建时间（可选）</summary>
    public string? CreationDate
    {
        get => GetAttr("CreationDate");
        set => SetAttr("CreationDate", string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>修改时间（可选）</summary>
    public string? ModDate
    {
        get => GetAttr("ModDate");
        set => SetAttr("ModDate", string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>附件大小（KB，可选）</summary>
    public double? Size
    {
        get
        {
            var v = GetAttr("Size");
            return double.TryParse(v, out double d) ? d : null;
        }
        set => SetAttr("Size", value is null ? null : STBase.Fmt(value.Value));
    }

    /// <summary>附件是否可见（默认 true）</summary>
    public bool Visible
    {
        get
        {
            var v = GetAttr("Visible");
            return string.IsNullOrEmpty(v) ? true : v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
        }
        set => SetAttr("Visible", value ? "true" : "false");
    }

    /// <summary>附件用途（默认 none）</summary>
    public string Usage
    {
        get => string.IsNullOrEmpty(GetAttr("Usage")) ? "none" : GetAttr("Usage")!;
        set => SetAttr("Usage", string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>附件在包内的路径</summary>
    public STLoc? FileLoc
    {
        get
        {
            var v = GetChildText("FileLoc");
            return v is null ? null : new STLoc(v);
        }
        set
        {
            if (value is null) GetChildElement("FileLoc")?.Remove();
            else SetChildText("FileLoc", value.Value);
        }
    }

    /// <summary>附件摘要（可选，自由文本，描述附件内容要点）。</summary>
    public string? Summary
    {
        get => GetAttr("Summary");
        set => SetAttr("Summary", string.IsNullOrWhiteSpace(value) ? null : value);
    }

    /// <summary>替代附件引用（可选，ST_RefID，指向可替代本附件的另一附件 ID）。</summary>
    public STRefID? Substitutional
    {
        get
        {
            var v = GetAttr("Substitutional");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("Substitutional", value?.Value.ToString());
    }
}