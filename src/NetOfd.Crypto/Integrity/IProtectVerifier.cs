namespace NetOfd.Crypto.Integrity;

/// <summary>
/// 用于 OFD 完整性保护协议（GM/T 0099-2020 §7.4.6 校验流程）的签名验证实现。
/// <para>b) 根据签名方案，调用杂凑算法计算完整性保护文件得到杂凑值；</para>
/// <para>c) 读取签名值文件，进行签名验证。</para>
/// 对应 Java: org.ofdrw.crypto.integrity.ProtectVerifier
/// </summary>
public interface IProtectVerifier
{
    /// <summary>对完整性保护文件与签名值执行验证。</summary>
    /// <param name="ofdEntriesXml">完整性保护文件（OFDEntries.xml）的内容字节。</param>
    /// <param name="signedValue">待验证的签名值（数据结构参照 GM/T 0099-2020 §7.4.3 密码算法要求）。</param>
    /// <returns>true - 签名值验证通过；false - 验证不通过。</returns>
    bool DigestThenVerify(byte[] ofdEntriesXml, byte[] signedValue);
}