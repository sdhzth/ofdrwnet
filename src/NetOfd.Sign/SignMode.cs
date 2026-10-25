namespace NetOfd.Sign;

/// <summary>
/// 签名模式。
/// 对应 Java: org.ofdrw.sign.SignMode
/// </summary>
public enum SignMode
{
    /// <summary>
    /// 整体保护文档：该模式下不产生新的签名/改签。
    /// </summary>
    WholeProtected,

    /// <summary>
    /// 续签：该模式下会自动忽略 Signatures.xml 的变更。
    /// </summary>
    ContinueSign
}