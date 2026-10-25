using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Integrity;

/// <summary>
/// GM/T 0099-2020 D.1 完整性保护描述文件。
/// <para>防夹带文件文件名固定为 "OFDEntries.xml"，应放置在 OFD 文件的根目录（与 OFD.xml 同级）。</para>
/// <code>
/// DocEntries ::= 元素 DocEntries，属性：ID（必选）、CreatorName（可选）、Version（可选）、CreationDate（可选）；
///                子元素：SignedValueLoc（必选）、FileList（必选）
/// </code>
/// 对应 Java: org.ofdrw.core.integrity.OFDEntries
/// </summary>
public class OFDEntries : OFDElement
{
    /// <summary>完整性保护描述文件在容器根目录的固定路径。</summary>
    public const string FileName = "OFDEntries.xml";

    public OFDEntries() : base("DocEntries") { }

    public OFDEntries(XElement element) : base(element) { }

    /// <summary>【必选】设置 标识。</summary>
    public OFDEntries SetID(string id)
    {
        if (id == null) throw new ArgumentException("标识(ID)为空");
        SetAttr("ID", id);
        return this;
    }

    /// <summary>【必选】获取 标识。</summary>
    public string GetID() =>
        GetAttr("ID") ?? throw new ArgumentException("标识(ID)为空");

    /// <summary>【可选】设置 创建者名称（null 表示删除）。</summary>
    public OFDEntries SetCreatorName(string? creatorName)
    {
        SetAttr("CreatorName", creatorName);
        return this;
    }

    /// <summary>【可选】获取 创建者名称。</summary>
    public string? GetCreatorName() => GetAttr("CreatorName");

    /// <summary>【可选】设置 模块版本（null 表示删除）。</summary>
    public OFDEntries SetVersion(string? version)
    {
        SetAttr("Version", version);
        return this;
    }

    /// <summary>【可选】获取 模块版本。</summary>
    public string? GetVersion() => GetAttr("Version");

    /// <summary>【可选】设置 创建时间（null 表示删除）。</summary>
    public OFDEntries SetCreationDate(DateTime? creationDate)
    {
        SetAttr("CreationDate", creationDate?.ToString("yyyy-MM-dd'T'HH:mm:ss"));
        return this;
    }

    /// <summary>【可选】获取 创建时间。</summary>
    public DateTime? GetCreationDate()
    {
        string? str = GetAttr("CreationDate");
        if (string.IsNullOrWhiteSpace(str)) return null;
        return DateTime.TryParse(str, out var dt) ? dt : null;
    }

    /// <summary>【必选】设置 针对防夹带文件形成的签名值位置。</summary>
    public OFDEntries SetSignedValueLoc(STLoc signedValueLoc)
    {
        if (signedValueLoc == null)
            throw new ArgumentException("防夹带文件形成的签名值位置(signedValueLoc)为空");
        SetChildText("SignedValueLoc", signedValueLoc.ToString());
        return this;
    }

    /// <summary>【必选】获取 针对防夹带文件形成的签名值位置。</summary>
    public STLoc GetSignedValueLoc()
    {
        string? v = GetChildElement("SignedValueLoc")?.Value;
        if (string.IsNullOrWhiteSpace(v))
            throw new ArgumentException("防夹带文件形成的签名值位置(signedValueLoc)为空");
        return new STLoc(v.Trim());
    }

    /// <summary>【必选】设置 防止夹带文件列表。</summary>
    public OFDEntries SetFileList(FileList fileList)
    {
        if (fileList == null)
            throw new ArgumentException("防止夹带文件列表(FileList)为空");
        SetOFDEntity(fileList);
        return this;
    }

    /// <summary>【必选】获取 防止夹带文件列表。</summary>
    public FileList GetFileList()
    {
        var e = GetChildElement("FileList");
        if (e == null)
            throw new ArgumentException("防止夹带文件列表(FileList)为空");
        return new FileList(e);
    }
}