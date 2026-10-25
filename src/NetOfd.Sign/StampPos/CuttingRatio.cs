namespace NetOfd.Sign.StampPos;

/// <summary>
/// 对开跨页签章时印章在左右两页的比例。
/// 对应 Java: org.ofdrw.sign.stamppos.CuttingRatio
/// </summary>
public class CuttingRatio
{
    /// <summary>左侧比例</summary>
    public double Left { get; set; }

    /// <summary>右侧比例</summary>
    public double Right { get; set; }
}