using NetOfd.Core.BasicType;
using NetOfd.Reader;

namespace NetOfd.Sign.StampPos;

/// <summary>
/// 对开跨页骑缝印章位置：印章跨越纸张边缘（对开装订），平铺在文档各页。
/// 对应 Java: org.ofdrw.sign.stamppos.CuttingRideStampPos
/// </summary>
public class CuttingRideStampPos : IStampAppearance
{
    /// <summary>图章所在的边</summary>
    public Side Side { get; set; }

    /// <summary>图章宽度（mm）</summary>
    public double Width { get; set; }

    /// <summary>图章高度（mm）</summary>
    public double Height { get; set; }

    /// <summary>图章在边上距离原始纸张边的偏移量（mm）；null 表示默认居中</summary>
    public double? Offset { get; set; } = null;

    /// <summary>图章在页上的页边距（mm），默认 0</summary>
    public double Margin { get; set; } = 0;

    /// <summary>对开跨页时图章左右两页的比例，size 需与页数一致；null 时左右各 0.5</summary>
    public List<CuttingRatio>? CuttingRatios { get; set; }

    public CuttingRideStampPos(Side side, double? offset, double width, double height, double margin,
        List<CuttingRatio>? cuttingRatios)
    {
        Side = side;
        Offset = offset;
        Width = width;
        Height = height;
        Margin = margin;
        CuttingRatios = cuttingRatios;
    }

    public List<SignatureModels.StampAnnot> GetAppearance(OFDReader ctx, ISignIdProvider idProvider)
    {
        int numPage = ctx.GetPageCount();
        var res = new List<SignatureModels.StampAnnot>(numPage);
        if (Side == Side.Right)
        {
            double itemWidth = Width * 0.5;
            for (int i = 0; i < numPage; i++)
            {
                STBox pageSize = ctx.GetPageSize(i + 1);
                double x;
                STBox? clip = null;

                if (numPage - 1 != i)
                {
                    if (CuttingRatios is { Count: > 0 })
                        itemWidth = Width * CuttingRatios[i].Left;
                    x = pageSize.Width - itemWidth - Margin;
                    clip = new STBox(0, 0, itemWidth, Height);
                }
                else
                {
                    if (CuttingRatios is { Count: > 0 })
                        itemWidth = Width * CuttingRatios[i - 1].Right;
                    x = 0 - (Width - itemWidth) + Margin;
                    clip = new STBox(Width - itemWidth, 0, itemWidth, Height);
                }
                double y = Offset is null
                    ? pageSize.Height / 2 - Height / 2
                    : Offset.Value;
                var pageId = ctx.GetPageObjectId(i + 1);
                res.Add(new SignatureModels.StampAnnot
                {
                    Id = idProvider.IncrementAndGet(),
                    Boundary = new STBox(x, y, Width, Height).ToString(),
                    PageRef = pageId?.Value.ToString() ?? "",
                    Clip = clip?.ToString()
                });
                if (i != 0 && numPage - 1 != i)
                {
                    if (CuttingRatios is { Count: > 0 })
                        itemWidth = Width * CuttingRatios[i - 1].Right;
                    x = 0 - (Width - itemWidth) + Margin;
                    clip = new STBox(Width - itemWidth, 0, itemWidth, Height);
                    res.Add(new SignatureModels.StampAnnot
                    {
                        Id = idProvider.IncrementAndGet(),
                        Boundary = new STBox(x, y, Width, Height).ToString(),
                        PageRef = pageId?.Value.ToString() ?? "",
                        Clip = clip?.ToString()
                    });
                }
            }
        }
        else if (Side == Side.Left)
        {
            double itemWidth = Width * 0.5;
            for (int i = 0; i < numPage; i++)
            {
                STBox pageSize = ctx.GetPageSize(i + 1);
                double x;
                STBox? clip = null;

                if (numPage - 1 != i)
                {
                    if (CuttingRatios is { Count: > 0 })
                        itemWidth = Width * CuttingRatios[i].Left;
                    x = 0 - (Width - itemWidth) + Margin;
                    clip = new STBox(Width - itemWidth, 0, itemWidth, Height);
                }
                else
                {
                    if (CuttingRatios is { Count: > 0 })
                        itemWidth = Width * CuttingRatios[i - 1].Right;
                    x = pageSize.Width - itemWidth - Margin;
                    clip = new STBox(0, 0, itemWidth, Height);
                }
                double y = Offset is null
                    ? pageSize.Height / 2 - Height / 2
                    : Offset.Value;
                var pageId = ctx.GetPageObjectId(i + 1);
                res.Add(new SignatureModels.StampAnnot
                {
                    Id = idProvider.IncrementAndGet(),
                    Boundary = new STBox(x, y, Width, Height).ToString(),
                    PageRef = pageId?.Value.ToString() ?? "",
                    Clip = clip?.ToString()
                });
                if (i != 0 && numPage - 1 != i)
                {
                    if (CuttingRatios is { Count: > 0 })
                        itemWidth = Width * CuttingRatios[i - 1].Right;
                    x = pageSize.Width - itemWidth - Margin;
                    clip = new STBox(0, 0, itemWidth, Height);
                    res.Add(new SignatureModels.StampAnnot
                    {
                        Id = idProvider.IncrementAndGet(),
                        Boundary = new STBox(x, y, Width, Height).ToString(),
                        PageRef = pageId?.Value.ToString() ?? "",
                        Clip = clip?.ToString()
                    });
                }
            }
        }
        return res;
    }
}