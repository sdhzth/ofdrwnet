namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 底纹单元起始绘制位置（GB/T 33190 §8.3.3 表 28 RelativeTo）。
/// 对应 Java: org.ofdrw.core.pageDescription.color.pattern.RelativeTo
/// </summary>
public enum RelativeTo
{
    /// <summary>相对于页面坐标系原点（默认 Object：对象坐标系原点）。</summary>
    Page,

    /// <summary>相对于对象坐标系原点（默认值）。</summary>
    Object
}

/// <summary>
/// <see cref="RelativeTo"/> 的解析/格式化辅助。
/// </summary>
public static class RelativeToUtil
{
    /// <summary>解析 XML 属性值，空值或 "Object" 视为 <see cref="RelativeTo.Object"/>。</summary>
    public static RelativeTo Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return RelativeTo.Object;
        return value.Trim().ToLowerInvariant() switch
        {
            "page" => RelativeTo.Page,
            "object" => RelativeTo.Object,
            _ => RelativeTo.Object
        };
    }

    /// <summary>转为 XML 属性值（首字母大写）。</summary>
    public static string ToXml(RelativeTo to) => to.ToString();
}
