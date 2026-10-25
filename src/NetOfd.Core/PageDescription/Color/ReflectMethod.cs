namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 底纹单元的翻转绘制效果（GB/T 33190 §8.3.3 表 28 ReflectMethod）。
/// 对应 Java: org.ofdrw.core.pageDescription.color.pattern.ReflectMethod
/// </summary>
public enum ReflectMethod
{
    /// <summary>普通重复（默认）。</summary>
    Normal,

    /// <summary>竖轴对称翻转：相邻列镜像（每列以其轴线翻转一次）。</summary>
    Column,

    /// <summary>横轴对称翻转：相邻行镜像。</summary>
    Row,

    /// <summary>十字轴对称翻转：行列同时镜像。</summary>
    RowAndColumn
}

/// <summary>
/// <see cref="ReflectMethod"/> 的解析/格式化辅助。
/// </summary>
public static class ReflectMethodUtil
{
    /// <summary>解析 XML 属性值，空值或 "Normal" 视为 <see cref="ReflectMethod.Normal"/>。</summary>
    public static ReflectMethod Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return ReflectMethod.Normal;
        return value.Trim().ToLowerInvariant() switch
        {
            "normal" => ReflectMethod.Normal,
            "column" => ReflectMethod.Column,
            "row" => ReflectMethod.Row,
            "rowandcolumn" => ReflectMethod.RowAndColumn,
            _ => ReflectMethod.Normal
        };
    }

    /// <summary>转为 XML 属性值（首字母大写，如 "RowAndColumn"）。</summary>
    public static string ToXml(ReflectMethod method) => method.ToString();
}
