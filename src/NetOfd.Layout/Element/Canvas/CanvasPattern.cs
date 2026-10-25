using System;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Color;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// Canvas 填充底纹（Pattern）：以一张图片为重复单元填充目标区域。
/// 对应 Java: org.ofdrw.layout.element.canvas.CanvasPattern
/// <para>
/// 重复样式（对齐 Java）：<c>repeat</c>（默认，普通平铺）→ <see cref="ReflectMethod.Normal"/>；
/// <c>column</c>（竖轴镜像）→ <see cref="ReflectMethod.Column"/>；
/// <c>row</c>（横轴镜像）→ <see cref="ReflectMethod.Row"/>；
/// <c>row-column</c>（十字镜像）→ <see cref="ReflectMethod.RowAndColumn"/>。
/// Java 注释明确 "repeat-x / repeat-y / no-repeat" 不支持。</para>
/// </summary>
public class CanvasPattern
{
    /// <summary>Java ofdrw 布局默认像素密度（像素/毫米），用于图片像素尺寸 → 毫米换算。</summary>
    public const double Ppm = 3.78;

    /// <summary>OFD 底纹对象（挂到 CT_Color 上作为颜色簇）。</summary>
    public CT_Pattern Pattern { get; }

    /// <summary>底纹图片路径（绝对路径）。</summary>
    public string ImgFilePath { get; }

    /// <summary>OFD 图片对象（置于底纹单元 CellContent 内）。</summary>
    public CTImage ImgObj { get; }

    /// <summary>
    /// 创建图案底纹。
    /// </summary>
    /// <param name="imgFilePath">底纹图片路径（绝对或相对均可，将归一化为绝对路径）。</param>
    /// <param name="repetition">重复样式：repeat / column / row / row-column。</param>
    /// <param name="imgObj">已就绪的 OFD 图片对象（含 ResourceID 与 Boundary/CTM）。</param>
    public CanvasPattern(string imgFilePath, string repetition, CTImage imgObj)
    {
        ImgFilePath = System.IO.Path.GetFullPath(imgFilePath);
        ImgObj = imgObj;
        Pattern = new CT_Pattern
        {
            Width = imgObj.Boundary?.Width ?? 0,
            Height = imgObj.Boundary?.Height ?? 0
        };

        switch ((repetition ?? "repeat").Trim().ToLowerInvariant())
        {
            case "repeat":
            case "":
                break; // 默认 Normal，无需设置
            case "column":
                Pattern.ReflectMethod = ReflectMethod.Column;
                break;
            case "row":
                Pattern.ReflectMethod = ReflectMethod.Row;
                break;
            case "row-column":
                Pattern.ReflectMethod = ReflectMethod.RowAndColumn;
                break;
            default:
                throw new ArgumentException("不支持的重复样式：" + repetition);
        }

        Pattern.SetCellContent(new CellContent().AddPageBlock(imgObj));
    }

    /// <summary>
    /// 设置底纹单元变换矩阵（6 参数 [a, b, c, d, e, f]）。
    /// 变换先应用于底纹单元，以便将其映射到目标区域。
    /// </summary>
    public CanvasPattern SetTransform(params double[] matrix)
    {
        if (matrix is null || matrix.Length < 6)
            throw new ArgumentException("矩阵参数错误：需要 [a, b, c, d, e, f] 6 个参数");
        Pattern.CTM = new STArray(matrix);
        return this;
    }

    /// <summary>
    /// 设置图片尺寸（每个重复单元尺寸，mm）。会同步更新 Pattern 单元尺寸与图片对象的 Boundary/CTM。
    /// </summary>
    public CanvasPattern SetImageSize(double w, double h)
    {
        Pattern.Width = w;
        Pattern.Height = h;
        ImgObj.Boundary = new STBox(0, 0, w, h);
        ImgObj.CTM = new STArray([w, 0, 0, h, 0, 0]);
        return this;
    }
}
