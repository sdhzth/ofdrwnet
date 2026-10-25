namespace NetOfd.Core.PageDescription.Color.colorSpace;

/// <summary>
/// 颜色空间类型（GB/T 33190-2016 §8.3 表 25）。
/// 对应 Java: org.ofdrw.core.pageDescription.color.colorSpace.OFDColorSpaceType
/// </summary>
public enum OFDColorSpaceType
{
    /// <summary>灰度</summary>
    GRAY,

    /// <summary>红绿蓝</summary>
    RGB,

    /// <summary>印刷颜色（青/洋红/黄/黑）</summary>
    CMYK,
}

/// <summary>
/// <see cref="OFDColorSpaceType"/> 的解析扩展。
/// </summary>
public static class OFDColorSpaceTypeExtensions
{
    /// <summary>
    /// 由类型字符串获取实例（大小写不敏感）。未知类型抛出异常（与 Java 一致）。
    /// </summary>
    public static OFDColorSpaceType GetInstance(string? type)
    {
        type = (type ?? "").Trim();
        if (type.Equals("GRAY", StringComparison.OrdinalIgnoreCase)) return OFDColorSpaceType.GRAY;
        if (type.Equals("RGB", StringComparison.OrdinalIgnoreCase)) return OFDColorSpaceType.RGB;
        if (type.Equals("CMYK", StringComparison.OrdinalIgnoreCase)) return OFDColorSpaceType.CMYK;
        throw new ArgumentException($"未知的颜色空间类型：{type}");
    }
}
