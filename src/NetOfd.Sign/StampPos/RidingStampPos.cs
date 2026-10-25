using NetOfd.Core.BasicType;
using NetOfd.Reader;

namespace NetOfd.Sign.StampPos;

/// <summary>
/// 骑缝印章位置：印章跨越纸张边缘，平铺在文档各页。
/// 默认图章位于右边居中。
/// 对应 Java: org.ofdrw.sign.stamppos.RidingStampPos
/// </summary>
public class RidingStampPos : IStampAppearance
{
    /// <summary>图章所在的边</summary>
    public Side Side { get; set; }

    /// <summary>图章宽度（mm）</summary>
    public double Width { get; set; }

    /// <summary>图章高度（mm）</summary>
    public double Height { get; set; }

    /// <summary>
    /// 图章在边上距离原始纸张边的偏移量（mm）。
    /// <para>null 表示默认居中。</para>
    /// </summary>
    public double? Offset { get; set; } = null;

    /// <summary>图章在页上的页边距（mm），默认 0</summary>
    public double Margin { get; set; } = 0;

    /// <summary>
    /// 图章指定的裁剪份数（可少于页数，将印章拆成 clipNumber 份重复使用）。
    /// <para>默认 0，表示按页数均分。</para>
    /// </summary>
    public int ClipNumber { get; set; } = 0;

    /// <summary>右侧边，居中放置。</summary>
    public RidingStampPos(double width, double height)
    {
        Width = width;
        Height = height;
        Side = Side.Right;
    }

    public RidingStampPos(Side side, double width, double height)
    {
        Side = side;
        Width = width;
        Height = height;
    }

    public RidingStampPos(Side side, double? offset, double width, double height)
    {
        Side = side;
        Offset = offset;
        Width = width;
        Height = height;
    }

    public RidingStampPos(Side side, double? offset, double width, double height, double margin)
    {
        Side = side;
        Offset = offset;
        Width = width;
        Height = height;
        Margin = margin;
    }

    public RidingStampPos(Side side, double? offset, int? clipNumber, double width, double height, double margin)
    {
        Side = side;
        Offset = offset;
        Width = width;
        Height = height;
        Margin = margin;
        if (clipNumber.HasValue) ClipNumber = clipNumber.Value;
    }

    public List<SignatureModels.StampAnnot> GetAppearance(OFDReader ctx, ISignIdProvider idProvider)
    {
        int numPage = ctx.GetPageCount();
        var res = new List<SignatureModels.StampAnnot>(numPage);
        bool isClipNumber = ClipNumber > 0 && ClipNumber < numPage;
        int leftClipNumber = 0;
        int curClipNumber = ClipNumber;

        if (Side == Side.Right || Side == Side.Left)
        {
            double itemWidth = Width / numPage;
            if (isClipNumber)
            {
                itemWidth = Width / ClipNumber;
                leftClipNumber = numPage % ClipNumber;
                if (leftClipNumber == 1)
                    leftClipNumber = ClipNumber + 1;
            }
            for (int i = 0; i < numPage; i++)
            {
                int index = leftClipNumber == 0 ? i : (leftClipNumber + i - numPage);
                if (numPage - i <= leftClipNumber)
                {
                    curClipNumber = leftClipNumber;
                    itemWidth = Width / curClipNumber;
                }
                STBox pageSize = ctx.GetPageSize(i + 1);
                double x;
                STBox? clip = null;
                if (Side == Side.Right)
                {
                    x = pageSize.Width - itemWidth * (i + 1) - Margin;
                    clip = new STBox(i * itemWidth, 0, itemWidth, Height);
                    if (isClipNumber)
                    {
                        x = pageSize.Width - (FloorMod(index, curClipNumber) + 1) * itemWidth - Margin;
                        clip = new STBox(FloorMod(index, curClipNumber) * itemWidth, 0, itemWidth, Height);
                    }
                }
                else
                {
                    x = 0 - itemWidth * (numPage - 1 - i) + Margin;
                    clip = new STBox((numPage - 1 - i) * itemWidth, 0, itemWidth, Height);
                    if (isClipNumber)
                    {
                        x = 0 - itemWidth * FloorMod(index, curClipNumber) + Margin;
                        clip = new STBox(FloorMod(index, curClipNumber) * itemWidth, 0, itemWidth, Height);
                    }
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
            }
        }
        else
        {
            double itemHeight = Height / numPage;
            if (isClipNumber)
            {
                itemHeight = Height / ClipNumber;
                leftClipNumber = numPage % ClipNumber;
                if (leftClipNumber == 1)
                    leftClipNumber = ClipNumber + 1;
            }
            for (int i = 0; i < numPage; i++)
            {
                int index = leftClipNumber == 0 ? i : (leftClipNumber + i - numPage);
                if (numPage - i <= leftClipNumber)
                {
                    curClipNumber = leftClipNumber;
                    itemHeight = Width / curClipNumber;
                }
                STBox pageSize = ctx.GetPageSize(i + 1);

                double x = Offset is null
                    ? pageSize.Width / 2 - Width / 2
                    : Offset.Value;

                double y;
                STBox? clip = null;
                if (Side == Side.Bottom)
                {
                    y = pageSize.Height - itemHeight * (i + 1) - Margin;
                    clip = new STBox(0, itemHeight * i, Width, itemHeight);
                    if (isClipNumber)
                    {
                        y = pageSize.Height - (FloorMod(index, curClipNumber) + 1) * itemHeight - Margin;
                        clip = new STBox(0, itemHeight * FloorMod(index, curClipNumber), Width, itemHeight);
                    }
                }
                else
                {
                    y = 0 - itemHeight * (numPage - 1 - i) + Margin;
                    clip = new STBox(0, (numPage - 1 - i) * itemHeight, Width, itemHeight);
                    if (isClipNumber)
                    {
                        y = 0 - itemHeight * FloorMod(index, curClipNumber) + Margin;
                        clip = new STBox(0, FloorMod(index, curClipNumber) * itemHeight, Width, itemHeight);
                    }
                }
                var pageId = ctx.GetPageObjectId(i + 1);
                res.Add(new SignatureModels.StampAnnot
                {
                    Id = idProvider.IncrementAndGet(),
                    Boundary = new STBox(x, y, Width, Height).ToString(),
                    PageRef = pageId?.Value.ToString() ?? "",
                    Clip = clip?.ToString()
                });
            }
        }
        return res;
    }

    private static int FloorMod(int a, int b) => ((a % b) + b) % b;
}