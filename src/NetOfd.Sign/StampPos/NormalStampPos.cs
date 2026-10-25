using NetOfd.Core.BasicType;
using NetOfd.Reader;

namespace NetOfd.Sign.StampPos;

/// <summary>
/// 普通印章位置：在指定页面指定坐标处放置印章。
/// 对应 Java: org.ofdrw.sign.stamppos.NormalStampPos
/// </summary>
public class NormalStampPos : IStampAppearance
{
    /// <summary>图章所在页码（从 1 起）</summary>
    public int Page { get; set; }

    /// <summary>图章左上角 X 坐标（mm）</summary>
    public double X { get; set; }

    /// <summary>图章左上角 Y 坐标（mm）</summary>
    public double Y { get; set; }

    /// <summary>图章宽度（mm）</summary>
    public double Width { get; set; }

    /// <summary>图章高度（mm）</summary>
    public double Height { get; set; }

    public NormalStampPos(int page, double x, double y, double width, double height)
    {
        Page = page;
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    public List<SignatureModels.StampAnnot> GetAppearance(OFDReader ctx, ISignIdProvider idProvider)
    {
        // 通过 OFD 页码获取页面对应的 ID
        var pageId = ctx.GetPageObjectId(Page);
        var annot = new SignatureModels.StampAnnot
        {
            Id = idProvider.IncrementAndGet(),
            Boundary = new STBox(X, Y, Width, Height).ToString(),
            PageRef = pageId?.Value.ToString() ?? ""
        };
        return new List<SignatureModels.StampAnnot> { annot };
    }
}