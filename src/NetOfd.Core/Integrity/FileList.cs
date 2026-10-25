using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Integrity;

/// <summary>
/// GM/T 0099-2020 D.1 防止夹带文件列表。
/// <code>
/// FileList ::= 元素 FileList，子元素：File*（必选）
/// </code>
/// 对应 Java: org.ofdrw.core.integrity.FileList
/// </summary>
public class FileList : OFDElement
{
    public FileList() : base("FileList") { }

    public FileList(XElement element) : base(element) { }

    /// <summary>【必选】增加 防止夹带文件信息。</summary>
    public FileList AddFile(IntegrityFile? file)
    {
        if (file is null) return this;
        AddOFDEntity(file);
        return this;
    }

    /// <summary>【必选】增加 防止夹带文件信息。</summary>
    public FileList AddFile(string id, string fileLoc)
    {
        AddOFDEntity(new IntegrityFile(id, fileLoc));
        return this;
    }

    /// <summary>【必选】增加 防止夹带文件信息。</summary>
    public FileList AddFile(string id, STLoc fileLoc)
    {
        AddOFDEntity(new IntegrityFile(id, fileLoc));
        return this;
    }

    /// <summary>【必选】获取 防止夹带文件信息列表。</summary>
    public List<IntegrityFile> GetFiles() =>
        GetChildElements("File").Select(e => new IntegrityFile(e)).ToList();
}