using System.Xml.Linq;

namespace NetOfd.Core.Versions;

/// <summary>
/// 版本序列（Versions）——一个 OFD 文档可能有多个版本。
/// <para>
/// 版本结构列表位于文档包根节点（OFD.xml 的 DocBody）中。
/// </para>
/// 对应 Java: org.ofdrw.core.versions.Versions（GB/T 33190-2016 第 19 章 图 89）
/// </summary>
public class Versions : OFDElement
{
    public Versions() : base("Versions") { }
    public Versions(XElement element) : base(element) { }

    /// <summary>增加版本描述入口</summary>
    public Versions AddVersion(Version version)
    {
        if (version is not null) AddOFDEntity(version);
        return this;
    }

    /// <summary>获取版本描述入口列表</summary>
    public List<Version> GetVersions()
        => GetChildElements("Version").Select(e => new Version(e)).ToList();
}
