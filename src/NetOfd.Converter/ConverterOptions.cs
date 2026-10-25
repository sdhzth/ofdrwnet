using SkiaSharp;

namespace NetOfd.Converter;

/// <summary>
/// OFD 转图像的可选配置。
/// 对应 Java: org.ofdrw.converter.Config
/// </summary>
public class ConverterOptions
{
    /// <summary>
    /// 像素/毫米 (Pixels Per Millimeter)。默认 15，约等于 380 DPI。
    /// 等价于 Java ImageExporter 的默认 ppm=15。
    /// </summary>
    public double PPM { get; set; } = 15;

    /// <summary>
    /// 文字 Size 的缩放系数。
    /// <para>
    /// 本移植版写入器（NetOfd.Layout）将 TextObject 的 Size 字段直接以
    /// <b>毫米</b> 为单位写入（与 Java 版约定一致，但不同于 OFD 标准的“磅”）。
    /// 因此默认 1.0 表示“直接使用文件中的 Size 值（视为 mm）”。
    /// </para>
    /// <para>
    /// 若需要渲染标准合规 OFD（Size 单位为磅），请设为 25.4 / 72 ≈ 0.353，
    /// 将磅转换为毫米。
    /// </para>
    /// </summary>
    public double TextSizeScale { get; set; } = 1.0;

    /// <summary>页面背景色，默认白色。</summary>
    public SKColor Background { get; set; } = SKColors.White;

    /// <summary>是否抗锯齿，默认 true。</summary>
    public bool AntiAlias { get; set; } = true;
}
