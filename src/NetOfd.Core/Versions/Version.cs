using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Versions;

/// <summary>
/// 版本描述入口（Version）。
/// 对应 Java: org.ofdrw.core.versions.Version（GB/T 33190-2016 第 19 章 图 89）
/// </summary>
public class Version : OFDElement
{
    public Version() : base("Version") { }
    public Version(XElement element) : base(element) { }

    /// <summary>创建版本描述入口，默认为非当前版本。</summary>
    /// <param name="id">版本标识（不含特殊字符的字符串）</param>
    /// <param name="index">版本号</param>
    /// <param name="baseLoc">指向包内的版本描述文件</param>
    public Version(string id, int index, STLoc baseLoc) : this()
    {
        ID = id;
        Index = index;
        BaseLoc = baseLoc;
    }

    /// <summary>版本标识（必填，字符串）</summary>
    public string ID
    {
        get => GetAttr("ID") ?? throw new InvalidOperationException("版本标识（ID）为空");
        set => SetAttr("ID", value);
    }

    /// <summary>版本号（必填）</summary>
    public int? Index
    {
        get => long.TryParse(GetAttr("Index"), out var v) ? (int)v : null;
        set => SetAttr("Index", value?.ToString());
    }

    /// <summary>是否是默认版本（可选，默认 false）</summary>
    public bool Current
    {
        get => string.Equals(GetAttr("Current"), "true", StringComparison.OrdinalIgnoreCase);
        set => SetAttr("Current", value ? "true" : "false");
    }

    /// <summary>指向包内的版本描述文件（必填）</summary>
    public STLoc? BaseLoc
    {
        get => GetAttr("BaseLoc") is { } v ? new STLoc(v) : null;
        set => SetAttr("BaseLoc", value?.Value);
    }
}
