using System.Text;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;

namespace NetOfd.Reader;

/// <summary>
/// 矩形框文本抽取过滤器：仅放行落在指定矩形区域内的字符。
/// <para>对应 Java: <c>org.ofdrw.reader.extractor.RegionTextExtractorFilter</c>。</para>
/// <para>判定语义与 Java 一致：
/// <list type="bullet">
/// <item>字符坐标 = TextObject.Boundary 左上角 + TextCode(X,Y) 逐字累加 DeltaX/DeltaY 推进；
/// 先经 CTM（若存在）变换再判定；</item>
/// <item>DeltaX/DeltaY 支持 <c>g n v</c> 压缩语法（经 <see cref="DeltaTool"/> 展开），
/// 为空时该方向按 0 推进；</item>
/// <item>区域判定为半开区间 <c>[x, x+width) × [y, y+height)</c>（同 <c>java.awt.Rectangle.contains(double,double)</c>）。</item>
/// </list>
/// 返回值是不在区域内的字符被剔除后的剩余文本；一条都不在区域内时返回 <c>null</c>（丢弃）。</para>
/// </summary>
public sealed class RegionTextExtractorFilter : IExtractorFilter
{
    private readonly double _x;
    private readonly double _y;
    private readonly double _width;
    private readonly double _height;

    /// <summary>构造区域过滤器。</summary>
    /// <param name="x">矩形左上角 X（与 OFD 页面坐标同单位，通常 mm）</param>
    /// <param name="y">矩形左上角 Y</param>
    /// <param name="width">矩形宽度</param>
    /// <param name="height">矩形高度</param>
    public RegionTextExtractorFilter(double x, double y, double width, double height)
    {
        if (width < 0 || height < 0)
            throw new ArgumentException("矩形宽高不能为负");
        _x = x;
        _y = y;
        _width = width;
        _height = height;
    }

    /// <summary>矩形左上角 X。</summary>
    public double X => _x;

    /// <summary>矩形左上角 Y。</summary>
    public double Y => _y;

    /// <summary>矩形宽度。</summary>
    public double Width => _width;

    /// <summary>矩形高度。</summary>
    public double Height => _height;

    /// <inheritdoc />
    public string? AllowText(CTText textObject, TextCode code)
    {
        string content = code.Text ?? string.Empty;
        if (content.Length == 0) return null;

        var deltaX = DeltaTool.GetDelta(code.DeltaX, content.Length);
        var deltaY = DeltaTool.GetDelta(code.DeltaY, content.Length);

        // 空 delta 处理：置 0 推进（Java emptyDeltaHandle）
        if (deltaX.Count == 0)
            for (int i = 0; i < content.Length; i++) deltaX.Add(0);
        if (deltaY.Count == 0)
            for (int i = 0; i < content.Length; i++) deltaY.Add(0);

        // Boundary 左上角（缺失按 0，Java 同）
        var box = textObject.Boundary;
        double topLeftX = box?.TopLeftX ?? 0;
        double topLeftY = box?.TopLeftY ?? 0;

        // CTM：Java 在 ctm 非空即变换；此处防御性要求 ≥6 元素
        double[]? matrix = textObject.CTM?.ToDoubleArray();
        bool hasCtm = matrix is { Length: >= 6 };

        double sx = code.X;
        double sy = code.Y;
        var builder = new StringBuilder();
        for (int i = 0; i < content.Length; i++)
        {
            char ch = content[i];
            double px, py;
            if (hasCtm)
            {
                // a b c d e f 仿射：x' = a*sx + c*sy + e；y' = b*sx + d*sy + f
                px = matrix![0] * sx + matrix[2] * sy + matrix[4];
                py = matrix[1] * sx + matrix[3] * sy + matrix[5];
            }
            else
            {
                px = sx;
                py = sy;
            }

            if (Contains(topLeftX + px, topLeftY + py))
                builder.Append(ch);

            sx += deltaX[i];
            sy += deltaY[i];
        }

        string allow = builder.ToString();
        return allow.Length == 0 ? null : allow;
    }

    /// <summary>半开区间判定：[x, x+w) × [y, y+h)，同 java.awt.Rectangle.contains(double,double)。</summary>
    private bool Contains(double px, double py)
        => px >= _x && px < _x + _width && py >= _y && py < _y + _height;
}
