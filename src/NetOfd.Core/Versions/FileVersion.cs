using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Versions;

/// <summary>
/// 文件列表文件描述（File）。
/// 对应 Java: org.ofdrw.core.versions.File（GB/T 33190-2016 第 19 章 图 71）
/// </summary>
public class FileVersion : OFDElement
{
    public FileVersion() : base("File") { }
    public FileVersion(XElement element) : base(element) { }

    /// <summary>创建文件描述。</summary>
    /// <param name="id">文件列表文件标识</param>
    /// <param name="loc">文件列表文件描述</param>
    public FileVersion(string id, STLoc loc) : this()
    {
        ID = id;
        File = loc;
    }

    /// <summary>文件列表文件标识（必填）</summary>
    public string ID
    {
        get => GetAttr("ID") ?? throw new InvalidOperationException("文件列表文件标识（ID）为空");
        set => SetAttr("ID", value);
    }

    /// <summary>文件列表文件描述（必填，元素文本为路径）。
    /// 注意：底层 SetText 会清空属性，此处保留 ID 属性以免设置顺序影响结果。</summary>
    public STLoc File
    {
        get => GetText() is { } v && v.Trim().Length > 0 ? new STLoc(v) : throw new InvalidOperationException("文件列表文件描述（File）为空");
        set
        {
            if (value is null) throw new ArgumentException("文件列表文件描述（File）不能为空", nameof(value));
            var id = GetAttr("ID");
            SetText(value.Value);
            if (id is not null) SetAttr("ID", id);
        }
    }
}
