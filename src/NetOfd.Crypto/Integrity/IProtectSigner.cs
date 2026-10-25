namespace NetOfd.Crypto.Integrity;

/// <summary>
/// 用于 OFD 完整性保护协议（GM/T 0099-2020 §7.4.3 c) d) 步骤）的签名实现。
/// <para>c) 根据签名方案，计算完整性保护文件的杂凑值；</para>
/// <para>d) 根据签名方案，使用版式文件合成者的签名私钥对杂凑值进行数字签名。</para>
/// 对应 Java: org.ofdrw.crypto.integrity.ProtectSigner
/// </summary>
public interface IProtectSigner
{
    /// <summary>对待保护文件执行签名。</summary>
    /// <param name="tbs">完整性保护文件（OFDEntries.xml）的内容字节。</param>
    /// <returns>签名值（应符合 GB/T 35275 标准）。</returns>
    byte[] DigestThenSign(byte[] tbs);
}