using NetOfd.Sign;
using NetOfd.Sign.Sm2Strut;
using NetOfd.Sign.Sm2Strut.Builder;
using Org.BouncyCastle.Crypto;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Crypto.Integrity;

/// <summary>
/// 以国密 SM2、SM3 算法实现的 OFD 完整性保护签名实现。
/// <para>签名值符合《GB/T 35275》标准。</para>
/// 对应 Java: org.ofdrw.crypto.integrity.GMProtectSigner
/// </summary>
public class GmProtectSigner : IProtectSigner
{
    /// <summary>版式文件合成者的签名私钥。</summary>
    private readonly AsymmetricKeyParameter _privateKey;

    /// <summary>版式文件合成者的公钥证书。</summary>
    private readonly X509Certificate _signCert;

    /// <summary>创建 国密 SM2 + SM3 的完整性保护签名实现。</summary>
    /// <param name="privateKey">版式文件合成者的签名私钥。</param>
    /// <param name="signCert">版式文件合成者的公钥证书。</param>
    public GmProtectSigner(AsymmetricKeyParameter privateKey, X509Certificate signCert)
    {
        if (privateKey == null)
            throw new ArgumentException("版式文件合成者的签名私钥(privateKey)为空");
        if (signCert == null)
            throw new ArgumentException("版式文件合成者的公钥证书(signCert)为空");
        _privateKey = privateKey;
        _signCert = signCert;
    }

    /// <summary>
    /// 根据 GM/T 0099 OFD 完整性保护协议执行完整性保护 §7.4.3 中的 c) d) 步骤。
    /// <para>c) 计算完整性保护文件的杂凑值（SM3，遵循 GB/T 32905）；</para>
    /// <para>d) 使用签名私钥对杂凑值进行数字签名（SM2，遵循 GB/T 32918 与 GB/T 35275）。</para>
    /// </summary>
    /// <param name="tbs">待签名文件内容。</param>
    /// <returns>签名值，应符合《GB/T 35275》标准。</returns>
    public byte[] DigestThenSign(byte[] tbs)
    {
        // 杂凑算法采用 SM3 时，遵循《GB/T 32905》：计算完整性保护文件的杂凑值作为待签名原文
        var plaintext = GmCrypto.Sm3(tbs);
        // 签名算法采用 SM2 时，遵循《GB/T 32918》和《GB/T 35275》
        var signature = GmCrypto.Sm2Sign(plaintext, _privateKey);
        // 构建《GB/T 35275》数据格式
        var signedData = SignedDataBuilder.SignedData(plaintext, signature, _signCert);
        var contentInfo = new ContentInfo(OIDs.SignedData, signedData);
        return contentInfo.GetEncoded();
    }
}