using NetOfd.Sign.Sm2Strut;

namespace NetOfd.Crypto.Integrity;

/// <summary>
/// 基于国密算法 SM2、SM3 的完整性保护签名值验证实现。
/// <para>待签名值应符合《GB/T 35275》标准。</para>
/// 对应 Java: org.ofdrw.crypto.integrity.GMProtectVerifier
/// </summary>
public class GmProtectVerifier : IProtectVerifier
{
    /// <summary>创建 国密 SM2 + SM3 的完整性保护签名值验证实现。</summary>
    public GmProtectVerifier()
    {
    }

    /// <summary>
    /// 根据 GM/T 0099 OFD 完整性保护协议执行完整性保护校验 §7.4.6。
    /// <para>b) 调用杂凑算法计算完整性保护文件得到杂凑值；</para>
    /// <para>c) 读取签名值文件，进行签名验证。</para>
    /// </summary>
    /// <param name="ofdEntriesXml">防夹带文件内容。</param>
    /// <param name="signedValue">待验证的签名值（数据结构参照 GM/T 0099-2020 §7.4.3 密码算法要求）。</param>
    /// <returns>true - 签名值验证通过；false - 验证不通过。</returns>
    public bool DigestThenVerify(byte[] ofdEntriesXml, byte[] signedValue)
    {
        var verifyInfo = GBT35275Validate.Validate(ofdEntriesXml, signedValue);
        return verifyInfo.Result;
    }
}