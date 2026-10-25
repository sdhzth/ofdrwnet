using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.Cms;
using Org.BouncyCastle.Crypto;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;
using AttributeTable = Org.BouncyCastle.Asn1.Cms.AttributeTable;

namespace NetOfd.Sign.Sm2Strut;

/// <summary>
/// GB/T 35275 签名数据结构验证。
/// 依据 GM/T 0099-2020 7.2.2：签章值为数字签名且签名算法使用 SM2 时，签名值应遵循 GB/T 35275。
/// 参考 ofdrw-gm 的 sm2strut.GBT35275Validate。
/// </summary>
public static class GBT35275Validate
{
    /// <summary>验证 GB/T 35275 SignedData 结构。</summary>
    /// <param name="tbsContent">被签内容（无需预先摘要，内部计算 SM3）。</param>
    /// <param name="signedValue">签名值（ContentInfo 的 DER 编码）。</param>
    /// <returns>验证结果。</returns>
    public static VerifyInfo Validate(byte[] tbsContent, byte[] signedValue)
    {
        ContentInfo contentInfo;
        SignedData signedData;
        try
        {
            contentInfo = ContentInfo.GetInstance(Asn1Object.FromByteArray(signedValue));
            if (contentInfo == null)
                throw new ArgumentException("无法解析 ContentInfo 结构");
            if (!OIDs.SignedData.Equals(contentInfo.ContentType))
                return VerifyInfo.Err($"非法签名数据类型，类型：{contentInfo.ContentType}");
            signedData = SignedData.GetInstance(contentInfo.Content);
            if (signedData == null)
                return VerifyInfo.Err("无法解析签名值格式，应为 GBT35275");
        }
        catch (Exception ex)
        {
            return VerifyInfo.Err($"无法解析签名值格式：{ex.Message}");
        }

        // a) 计算原文摘要
        var digestAct = GmCrypto.Sm3(tbsContent);

        // 原文（可能是摘要值或其 Base64）
        byte[]? plaintext = null;

        var dataContent = signedData.ContentInfo?.Content;
        if (dataContent != null)
        {
            plaintext = DerOctetString.GetInstance(dataContent).GetOctets();
            if (!digestAct.AsSpan().SequenceEqual(plaintext))
            {
                // [兼容非规范格式] 某些实现将摘要值进行 Base64 编码后作为签名原文
                try
                {
                    var decoded = Convert.FromBase64String(System.Text.Encoding.UTF8.GetString(plaintext));
                    if (!digestAct.AsSpan().SequenceEqual(decoded))
                        return VerifyInfo.Err("被签名原文不一致");
                }
                catch
                {
                    return VerifyInfo.Err("被签名原文不一致");
                }
                // 注：签名原文仍是 content 中的字节（Base64 字符串），验证时保持原样
            }
        }

        // b) 逐个验证签名者信息
        if (signedData.SignerInfos == null)
            return VerifyInfo.Err("缺少签名者信息(signerInfos)");

        foreach (var item in signedData.SignerInfos)
        {
            var signerInfo = SignerInfo.GetInstance(item);

            var authAttrs = signerInfo.AuthenticatedAttributes;
            if (authAttrs != null)
            {
                // 兼容 CMS（PKCS#9）格式的摘要属性
                var attributes = new AttributeTable(authAttrs);
                var attrVec = attributes.GetAll(CmsAttributes.MessageDigest);
                if (attrVec == null || attrVec.Count == 0)
                    return VerifyInfo.Err("缺少 PKCS#9 message-digest 属性");
                var attr = Org.BouncyCastle.Asn1.Cms.Attribute.GetInstance(attrVec[0]);
                var asn1 = attr.AttrValues[0].ToAsn1Object();
                if (asn1 is not Asn1OctetString signedMessageDigest)
                    return VerifyInfo.Err("PKCS#9 message-digest 属性类型不是 'OCTET STRING'");
                if (!digestAct.AsSpan().SequenceEqual(signedMessageDigest.GetOctets()))
                    return VerifyInfo.Err("被签名原文不一致");
                plaintext = authAttrs.GetEncoded();
            }

            if (plaintext == null)
                return VerifyInfo.Err("GBT35275 签名值格式错误");

            var iasn = signerInfo.IssuerAndSerialNumber;
            var asn1Cert = signedData.GetSignCert(iasn);
            if (asn1Cert == null)
                return VerifyInfo.Err("没有找到匹配的证书，无法验证签名");

            var cert = new X509Certificate(asn1Cert.GetEncoded());
            AsymmetricKeyParameter pubKey = cert.GetPublicKey();
            var signature = signerInfo.EncryptedDigest.GetOctets();
            if (!GmCrypto.Sm2Verify(plaintext, signature, pubKey))
                return VerifyInfo.Err("签名值不一致");
        }

        return VerifyInfo.Ok();
    }
}