namespace NetOfd.Core.Crypto;

/// <summary>
/// 密码保护方案标识符（GM/T 0099-2020 附录 A.1）。
/// 对应 Java: org.ofdrw.core.crypto.ProtectionCaseID
/// </summary>
public enum ProtectionCaseID
{
    /// <summary>采用口令方式加密（国密）。对称算法 SM4，CBC 模式，PKCS#7 填充。</summary>
    EncryptGMPassword,

    /// <summary>采用证书方式加密（国密）。对称算法 SM4，CBC 模式；非对称算法 SM2。</summary>
    EncryptGMCert,

    /// <summary>采用证书进行数字签名（遵循 GB/T 35275）。</summary>
    Signature
}

/// <summary>ProtectionCaseID 与方案标识字符串("1.1.1"等)互转的扩展方法。</summary>
public static class ProtectionCaseIDExt
{
    public static string Id(this ProtectionCaseID id) => id switch
    {
        ProtectionCaseID.EncryptGMPassword => "1.1.1",
        ProtectionCaseID.EncryptGMCert => "1.1.2",
        ProtectionCaseID.Signature => "1.2.1",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null)
    };

    public static ProtectionCaseID? FromId(string? id) => id switch
    {
        "1.1.1" => ProtectionCaseID.EncryptGMPassword,
        "1.1.2" => ProtectionCaseID.EncryptGMCert,
        "1.2.1" => ProtectionCaseID.Signature,
        _ => null
    };
}
