using System.Globalization;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 三角单元切换的方向标志（GB/T 33190 §8.3.4.4 表 31，附录 A.13）。
/// <para>
/// 高洛德渐变中，后续控制点与当前三角形两条可用边之一构成新三角形：
/// <list type="bullet">
/// <item>Flag0（0）：开始一个与之前无连接的新三角形（该点 + 后续两点）；默认值。</item>
/// <item>Flag1（1）：与当前三角形的 vbc 边构成新三角形 (vb, vc, vd)。</item>
/// <item>Flag2（2）：与当前三角形的 vac 边构成新三角形 (va, vc, vd)。</item>
/// </list>
/// 语义对齐 ISO 32000-1 type-4 free-form Gouraud-shaded triangle mesh。
/// </para>
/// 对应 Java: org.ofdrw.core.pageDescription.color.color.EdgeFlag
/// </summary>
public enum EdgeFlag
{
    /// <summary>0：开始新三角形（不与之前相连）</summary>
    Flag0 = 0,

    /// <summary>1：与当前三角形的 vbc 边连接</summary>
    Flag1 = 1,

    /// <summary>2：与当前三角形的 vac 边连接</summary>
    Flag2 = 2
}

/// <summary>
/// <see cref="EdgeFlag"/> 的解析/格式化辅助（与 XML 属性整数值互转）。
/// </summary>
public static class EdgeFlagUtil
{
    /// <summary>
    /// 解析 XML 属性值：空/缺省视为 <see cref="EdgeFlag.Flag0"/>（对齐 Java 空串→_0）；
    /// 非法值抛 <see cref="ArgumentException"/>（对齐 Java IllegalArgumentException）。
    /// </summary>
    public static EdgeFlag Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return EdgeFlag.Flag0;
        if (!int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n)
            || n is < 0 or > 2)
            throw new ArgumentException("未知的三角单元切换的方向标志：" + value, nameof(value));
        return (EdgeFlag)n;
    }

    /// <summary>转为 XML 属性值（0-2 整数字符串）。</summary>
    public static string ToXml(EdgeFlag flag) => ((int)flag).ToString(CultureInfo.InvariantCulture);
}
