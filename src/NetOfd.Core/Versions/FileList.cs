using System.Xml.Linq;

namespace NetOfd.Core.Versions;

/// <summary>
/// 版本包含的文件列表（FileList）。
/// 对应 Java: org.ofdrw.core.versions.FileList（GB/T 33190-2016 第 19 章 图 71）
/// </summary>
public class FileList : OFDElement
{
    public FileList() : base("FileList") { }
    public FileList(XElement element) : base(element) { }

    /// <summary>增加文件列表文件描述</summary>
    public FileList AddFile(FileVersion file)
    {
        if (file is not null) AddOFDEntity(file);
        return this;
    }

    /// <summary>增加文件列表文件描述</summary>
    /// <param name="id">文件列表文件标识</param>
    /// <param name="fileLoc">文件列表文件描述</param>
    public FileList AddFile(string id, BasicType.STLoc fileLoc)
        => AddFile(new FileVersion(id, fileLoc));

    /// <summary>获取文件列表文件描述列表</summary>
    public List<FileVersion> GetFiles()
        => GetChildElements("File").Select(e => new FileVersion(e)).ToList();
}
