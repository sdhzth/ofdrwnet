using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Integrity;

/// <summary>
/// GM/T 0099-2020 D.1 防止夹带文件信息。
/// <code>
/// File ::= 元素 File，属性：ID（必选）、FileLoc（必选，包内文件路径）
/// </code>
/// 对应 Java: org.ofdrw.core.integrity.File
/// </summary>
public class IntegrityFile : OFDElement
{
    public IntegrityFile() : base("File") { }

    public IntegrityFile(XElement element) : base(element) { }

    /// <summary>创建 防止夹带文件信息。</summary>
    public IntegrityFile(string id, string fileLoc) : base("File")
    {
        SetID(id);
        SetFileLoc(new STLoc(fileLoc));
    }

    /// <summary>创建 防止夹带文件信息。</summary>
    public IntegrityFile(string id, STLoc fileLoc) : base("File")
    {
        SetID(id);
        SetFileLoc(fileLoc);
    }

    /// <summary>【必选】设置 文件标识。</summary>
    public IntegrityFile SetID(string id)
    {
        if (string.IsNullOrEmpty(id))
            throw new ArgumentException("文件标识(ID)为空");
        SetAttr("ID", id);
        return this;
    }

    /// <summary>【必选】获取 文件标识。</summary>
    public string GetID() =>
        GetAttr("ID") ?? throw new ArgumentException("文件标识(ID)为空");

    /// <summary>【必选】设置 包内文件路径。</summary>
    public IntegrityFile SetFileLoc(STLoc fileLoc)
    {
        if (fileLoc == null)
            throw new ArgumentException("包内文件路径(FileLoc)为空");
        SetAttr("FileLoc", fileLoc.ToString());
        return this;
    }

    /// <summary>【必选】获取 包内文件路径。</summary>
    public STLoc GetFileLoc() =>
        new(GetAttr("FileLoc") ?? throw new ArgumentException("包内文件路径(FileLoc)为空"));
}