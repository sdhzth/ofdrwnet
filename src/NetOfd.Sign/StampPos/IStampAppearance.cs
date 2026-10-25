using NetOfd.Reader;

namespace NetOfd.Sign.StampPos;

/// <summary>
/// 签章外观位置提供者。
/// 对应 Java: org.ofdrw.sign.stamppos.StampAppearance
/// </summary>
public interface IStampAppearance
{
    /// <summary>
    /// 获取签章外观。
    /// </summary>
    /// <param name="ctx">OFD 读取器</param>
    /// <param name="idProvider">签名 ID 提供器</param>
    /// <returns>签章外观列表</returns>
    List<SignatureModels.StampAnnot> GetAppearance(OFDReader ctx, ISignIdProvider idProvider);
}