using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color.colorSpace;

/// <summary>
/// 调色板中预定义的颜色（索引从 0 开始）。
/// 颜色分量可为：灰度（单通道）、RGB（三通道）、CMYK（四通道）；
/// 支持十进制（如 <c>17 34 51</c>）或 #hex（如 <c>#11 #22 #33</c>）。
/// 对应 Java: org.ofdrw.core.pageDescription.color.colorSpace.CV
/// </summary>
public class CV : OFDElement
{
    public CV() : base("CV") { }
    public CV(XElement element) : base(element) { }

    /// <summary>预定义颜色的原始文本（保留 #hex / 十进制 / 浮点分量，供 ColorConvert 精确解析）。</summary>
    public string? ColorText
    {
        get => GetText();
        set => SetText(value);
    }

    /// <summary>
    /// 解析为 <see cref="STArray"/>。注意：<see cref="STArray"/> 无法表达 #hex 分量，
    /// 着色时优先使用 <see cref="ColorText"/> 由 <c>ColorConvert</c> 解析。
    /// </summary>
    public STArray? Color
    {
        get
        {
            var t = GetText();
            return string.IsNullOrWhiteSpace(t) ? null : STArray.Parse(t);
        }
    }
}
