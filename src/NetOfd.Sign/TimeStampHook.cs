namespace NetOfd.Sign;

/// <summary>
/// 时间戳钩子：对签章签名值加盖时间戳（RFC 3161 时间戳请求令牌）。
/// 参考 ofdrw-sign 的 TimeStampHook。
/// </summary>
/// <param name="signature">签名值（对 TBS_Sign DER 的 SM2 签名）。</param>
/// <returns>时间戳令牌 DER；返回 null 表示不附加时间戳。</returns>
public delegate byte[]? TimeStampHook(byte[] signature);