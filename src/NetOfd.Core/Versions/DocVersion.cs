using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Versions;

/// <summary>
/// 版本（DocVersion）——版本信息在独立的文件中描述，
/// 版本定义结构中列出了一个 OFD 文档版本中所需的所有文件。
/// <para>
/// 对应 Java: org.ofdrw.core.versions.DocVersion（GB/T 33190-2016 第 19 章 图 90）
/// 注：Java 版 CreationDate 写入属性、读取却解析子元素（不一致），此处统一为属性。
/// </para>
/// </summary>
public class DocVersion : OFDElement
{
    public DocVersion() : base("DocVersion") { }
    public DocVersion(XElement element) : base(element) { }

    /// <summary>创建版本描述。</summary>
    /// <param name="id">版本标识符</param>
    /// <param name="fileList">版本包含的文件列表</param>
    public DocVersion(string id, FileList fileList) : this()
    {
        ID = id;
        FileList = fileList;
    }

    /// <summary>版本标识符（必填）</summary>
    public string ID
    {
        get => GetAttr("ID") ?? throw new InvalidOperationException("版本标识符（ID）为空");
        set
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("版本标识符（ID）不能为空", nameof(value));
            SetAttr("ID", value);
        }
    }

    /// <summary>该文件适用的格式版本（可选）</summary>
    public string? Version
    {
        get => GetAttr("Version");
        set => SetAttr("Version", value);
    }

    /// <summary>版本名称（可选）</summary>
    public string? Name
    {
        get => GetAttr("Name");
        set => SetAttr("Name", value);
    }

    /// <summary>创建时间（可选，属性形式 yyyy-MM-dd）</summary>
    public DateOnly? CreationDate
    {
        get => DateOnly.TryParse(GetAttr("CreationDate"), out var d) ? d : null;
        set => SetAttr("CreationDate", value?.ToString("yyyy-MM-dd"));
    }

    /// <summary>版本包含的文件列表（必填）</summary>
    public FileList? FileList
    {
        get
        {
            var e = GetChildElement("FileList");
            return e is null ? null : new FileList(e);
        }
        set
        {
            if (value is null) throw new ArgumentException("版本包含的文件列表（FileList）不能为空", nameof(value));
            SetOFDEntity(value);
        }
    }

    /// <summary>该版本的入口文件（必填）</summary>
    public STLoc? DocRoot
    {
        get
        {
            var v = GetChildText("DocRoot");
            return v is null ? null : new STLoc(v);
        }
        set
        {
            if (value is null) throw new ArgumentException("该版本的入口文件（DocRoot）不能为空", nameof(value));
            SetChildText("DocRoot", value.Value);
        }
    }
}
