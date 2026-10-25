using Org.BouncyCastle.Asn1;

namespace NetOfd.Sign.Sm2Strut;

/// <summary>
/// GM/T 35275-2017 / GB/T 35275 第 5 章 OID 定义。
/// 参考 ofdrw-gm 的 sm2strut.OIDs。
/// </summary>
public static class OIDs
{
    /// <summary>SM2 密码算法签名消息语法规范（GM/T 35275）。</summary>
    public static readonly DerObjectIdentifier Gmt35275Sm2 = new("1.2.156.10197.6.1.4.2");

    /// <summary>数据。</summary>
    public static readonly DerObjectIdentifier Data = new("1.2.156.10197.6.1.4.2.1");

    /// <summary>签名数据。</summary>
    public static readonly DerObjectIdentifier SignedData = new("1.2.156.10197.6.1.4.2.2");

    /// <summary>数字信封数据。</summary>
    public static readonly DerObjectIdentifier EnvelopedData = new("1.2.156.10197.6.1.4.2.3");

    /// <summary>签名并加密的数据。</summary>
    public static readonly DerObjectIdentifier SignedAndEnvelopedData = new("1.2.156.10197.6.1.4.2.4");

    /// <summary>加密数据。</summary>
    public static readonly DerObjectIdentifier EncryptedData = new("1.2.156.10197.6.1.4.2.5");

    /// <summary>密钥协商信息。</summary>
    public static readonly DerObjectIdentifier KeyAgreementInfo = new("1.2.156.10197.6.1.4.2.6");

    /// <summary>SM4 对称加密算法。</summary>
    public static readonly DerObjectIdentifier Sm4 = new("1.2.156.10197.1.100");

    /// <summary>SM2 椭圆曲线公钥密码算法。</summary>
    public static readonly DerObjectIdentifier Sm2 = new("1.2.156.10197.1.301");

    /// <summary>SM2-1 数字签名算法。</summary>
    public static readonly DerObjectIdentifier Sm2Sign = new("1.2.156.10197.1.301.1");

    /// <summary>SM2-2 密钥交换协议。</summary>
    public static readonly DerObjectIdentifier Sm2KeyExchange = new("1.2.156.10197.1.301.2");

    /// <summary>SM2-3 密钥加密算法。</summary>
    public static readonly DerObjectIdentifier Sm2Encrypt = new("1.2.156.10197.1.301.3");

    /// <summary>SM3 密码杂凑算法。</summary>
    public static readonly DerObjectIdentifier Sm3 = new("1.2.156.10197.1.401");

    /// <summary>SM3 密码杂凑算法（与公钥使用）。</summary>
    public static readonly DerObjectIdentifier Sm3_1 = new("1.2.156.10197.1.401.1");

    /// <summary>SM3 密码杂凑算法（与私钥使用）。</summary>
    public static readonly DerObjectIdentifier Sm3_2 = new("1.2.156.10197.1.401.2");

    /// <summary>SM2 算法与 SM3 算法组合签名。</summary>
    public static readonly DerObjectIdentifier Sm3WithSm2 = new("1.2.156.10197.501");
}