using NetOfd.Core.Graph.PathObj;

namespace NetOfd.Layout.Engine;

/// <summary>
/// 图形工具：用于快速构建以路径为基础的图形图像数据。
/// 对应 Java: org.ofdrw.layout.engine.GraphHelper
/// </summary>
public static class GraphHelper
{
    /// <summary>
    /// 创建一个矩形轮廓（顺时针：左上 → 右上 → 右下 → 左下 → 闭合）。
    /// </summary>
    /// <param name="x">矩形左上角 X 坐标</param>
    /// <param name="y">矩形左上角 Y 坐标</param>
    /// <param name="w">矩形宽度</param>
    /// <param name="h">矩形高度</param>
    /// <returns>矩形路径轮廓（已 Flush，可直接取 <see cref="AbbreviatedData.Value"/>）</returns>
    public static AbbreviatedData Rect(double x, double y, double w, double h)
    {
        return new AbbreviatedData()
            .M(x, y)
            .L(x + w, y)
            .L(x + w, y + h)
            .L(x, y + h)
            .C()
            .Flush();
    }

    /// <summary>
    /// 创建一个矩形轮廓（以 <see cref="Rectangle"/> 描述区域，坐标取 X/Y，尺寸取 Width/Height）。
    /// 本移植版扩展：Java 仅提供 <see cref="Rect(double, double, double, double)"/>。
    /// </summary>
    public static AbbreviatedData Rect(Rectangle area)
    {
        if (area is null) throw new System.ArgumentNullException(nameof(area));
        return Rect(area.X, area.Y, area.Width, area.Height);
    }
}
