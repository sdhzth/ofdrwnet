namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 渐变绘制的方式（GB/T 33190 §8.3.4.2 表 29）。
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.MapType
/// </summary>
public enum MapType
{
    /// <summary>直接绘制：在轴线（轴长）范围内按段颜色渐变。默认值。</summary>
    Direct,

    /// <summary>重复绘制：以 MapUnit 为区间长度，沿轴线重复填充渐变。</summary>
    Repeat,

    /// <summary>反射绘制：以 MapUnit 为区间长度，交替正反重复填充渐变。</summary>
    Reflect
}

/// <summary>
/// <see cref="MapType"/> 的解析/格式化辅助（与 XML 属性字符串互转）。
/// </summary>
public static class MapTypeUtil
{
    /// <summary>解析 XML 属性值，空值或 "Direct" 视为 <see cref="MapType.Direct"/>。</summary>
    public static MapType Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return MapType.Direct;
        return value.Trim().ToLowerInvariant() switch
        {
            "direct" => MapType.Direct,
            "repeat" => MapType.Repeat,
            "reflect" => MapType.Reflect,
            _ => MapType.Direct
        };
    }

    /// <summary>转为 XML 属性值（首字母大写）。</summary>
    public static string ToXml(MapType type) => type.ToString();
}
