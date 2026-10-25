namespace NetOfd.Core.BasicType;

/// <summary>
/// OFD 包内文件路径，支持绝对路径和相对路径。
/// 例如："/Doc_0/Pages/Page_0/Content.xml"、"../Res/"、"./Fonts/"
/// 对应 Java: org.ofdrw.core.basicType.ST_Loc
/// </summary>
public sealed class STLoc : STBase, IEquatable<STLoc>
{
    private string _value;

    public string Value => _value;

    public STLoc(string value)
    {
        _value = value ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>是否为空路径</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    /// <summary>是否为根路径（以 / 开头）</summary>
    public bool IsRootPath => _value.StartsWith('/');

    /// <summary>将路径分割为各个部分</summary>
    public string[] Parts()
    {
        string v = _value.TrimStart('/');
        return v.Split('/', StringSplitOptions.RemoveEmptyEntries);
    }

    /// <summary>获取父路径</summary>
    public STLoc Parent()
    {
        if (_value.EndsWith('/'))
        {
            // "/Doc_0/Pages/" -> "/Doc_0/"
            int idx = _value.TrimEnd('/').LastIndexOf('/');
            if (idx < 0) return new STLoc("/");
            return new STLoc(_value[..(idx + 1)]);
        }
        else
        {
            int idx = _value.LastIndexOf('/');
            if (idx < 0) return new STLoc("/");
            return new STLoc(_value[..(idx + 1)]);
        }
    }

    /// <summary>获取文件名（不含路径）</summary>
    public string GetFileName()
    {
        string v = _value.TrimEnd('/');
        int idx = v.LastIndexOf('/');
        return idx < 0 ? v : v[(idx + 1)..];
    }

    /// <summary>拼接子路径</summary>
    public STLoc Cat(string sub)
    {
        string base_ = _value.EndsWith('/') ? _value : _value + "/";
        return new STLoc(base_ + sub.TrimStart('/'));
    }

    /// <summary>拼接子路径</summary>
    public STLoc Cat(STLoc sub) => Cat(sub._value);

    /// <summary>将相对路径相对于 basePath 解析为绝对路径</summary>
    public static STLoc Resolve(string basePath, string relativePath)
    {
        if (relativePath.StartsWith('/'))
            return new STLoc(relativePath);

        // 获取基础目录
        string baseDir = basePath.EndsWith('/') ? basePath : basePath.Substring(0, basePath.LastIndexOf('/') + 1);

        // 组合并规范化
        string combined = baseDir + relativePath;
        return new STLoc(NormalizePath(combined));
    }

    private static string NormalizePath(string path)
    {
        var parts = new List<string>();
        bool rooted = path.StartsWith('/');
        foreach (var part in path.Split('/'))
        {
            if (part == "..") { if (parts.Count > 0) parts.RemoveAt(parts.Count - 1); }
            else if (part != "." && part.Length > 0) parts.Add(part);
        }
        string result = string.Join("/", parts);
        return rooted ? "/" + result : result;
    }

    public override string ToString() => _value;
    public bool Equals(STLoc? other) => other is not null && _value == other._value;
    public override bool Equals(object? obj) => Equals(obj as STLoc);
    public override int GetHashCode() => _value.GetHashCode();

    public static implicit operator string(STLoc loc) => loc._value;
    public static implicit operator STLoc(string value) => new(value);
}
