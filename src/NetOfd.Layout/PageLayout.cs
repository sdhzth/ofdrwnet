using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicType;

namespace NetOfd.Layout;

/// <summary>
/// 虚拟页面样式（页面尺寸 + 页边距）。
/// 对应 Java: org.ofdrw.layout.PageLayout
/// </summary>
public class PageLayout
{
    public static PageLayout A0() => new(841d, 1189d);
    public static PageLayout A1() => new(594d, 841d);
    public static PageLayout A2() => new(420d, 594d);
    public static PageLayout A3() => new(297d, 420d);
    public static PageLayout A4() => new(210d, 297d);
    public static PageLayout A5() => new(148d, 210d);
    public static PageLayout A6() => new(105d, 148d);
    public static PageLayout A7() => new(74d, 105d);
    public static PageLayout A8() => new(52d, 74d);
    public static PageLayout A9() => new(37d, 52d);
    public static PageLayout A10() => new(26d, 37d);

    /// <summary>页面宽度（mm）</summary>
    public double Width { get; set; }

    /// <summary>页面高度（mm）</summary>
    public double Height { get; set; }

    /// <summary>
    /// 外边距（mm）：上 左 下 右
    /// 默认值：上下 25.4mm（1英寸），左右 31.7mm。
    /// </summary>
    private double[] _margin = { 25.4, 31.7, 25.4, 31.7 };

    public PageLayout(double width, double height)
    {
        Width = width;
        Height = height;
    }

    public PageLayout(STBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        Width = box.Width;
        Height = box.Height;
    }

    /// <summary>设置外边距（可传 1/2/4 个值）</summary>
    public PageLayout SetMargin(params double[] margin)
    {
        _margin = ArrayParamTool.Arr4p(margin);
        return this;
    }

    public double[] Margin => _margin;

    public PageLayout SetMarginTop(double top) { _margin[0] = top; return this; }
    public double MarginTop { get => _margin[0]; set => _margin[0] = value; }

    public PageLayout SetMarginRight(double right) { _margin[1] = right; return this; }
    public double MarginRight { get => _margin[1]; set => _margin[1] = value; }

    public PageLayout SetMarginBottom(double bottom) { _margin[2] = bottom; return this; }
    public double MarginBottom { get => _margin[2]; set => _margin[2] = value; }

    public PageLayout SetMarginLeft(double left) { _margin[3] = left; return this; }
    public double MarginLeft { get => _margin[3]; set => _margin[3] = value; }

    /// <summary>实际能放置内容的宽度</summary>
    public double ContentWidth() => Width - MarginLeft - MarginRight;

    /// <summary>实际能放置内容的高度</summary>
    public double ContentHeight() => Height - MarginTop - MarginBottom;

    /// <summary>绘制区域原点 X</summary>
    public double StartX => MarginLeft;

    /// <summary>绘制区域原点 Y</summary>
    public double StartY => MarginTop;

    /// <summary>页面正文的工作区域</summary>
    public Rectangle GetWorkerArea() => new(StartX, StartY, ContentWidth(), ContentHeight());

    /// <summary>获取 OFD 页面区域</summary>
    public CT_PageArea GetPageArea() => new()
    {
        // 物理区域为实际页面大小
        PhysicalBox = new STBox(0, 0, Width, Height),
        // 为了兼容骑缝章，不减去页面边距
        ApplicationBox = new STBox(0, 0, Width, Height)
    };

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj)) return true;
        if (obj is PageLayout that)
        {
            return _margin.SequenceEqual(that._margin)
                && Width == that.Width
                && Height == that.Height;
        }
        return false;
    }

    public override int GetHashCode() => HashCode.Combine(Width, Height);

    /// <summary>浅拷贝</summary>
    public PageLayout Clone() => new(Width, Height) { _margin = (double[])_margin.Clone() };
}

/// <summary>
/// 用于将变长参数规范为 4 元素数组的辅助类。
/// 对应 Java: org.ofdrw.layout.element.ArrayParamTool
/// </summary>
public static class ArrayParamTool
{
    /// <summary>
    /// 将长度 1/2/4 的数组规范为 4 元素数组。
    /// 1 个值：全部相等；2 个值：[上下, 左右]；4 个值：[上, 右, 下, 左]。
    /// </summary>
    public static double[] Arr4p(double[] args)
    {
        if (args == null || args.Length == 0)
        {
            return new double[4];
        }
        return args.Length switch
        {
            1 => new[] { args[0], args[0], args[0], args[0] },
            2 => new[] { args[0], args[1], args[0], args[1] },
            4 => new[] { args[0], args[1], args[2], args[3] },
            _ => throw new ArgumentException("参数个数必须是 1/2/4 个", nameof(args))
        };
    }
}