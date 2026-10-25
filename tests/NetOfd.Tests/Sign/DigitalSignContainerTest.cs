using System;
using System.IO;
using System.Linq;
using NetOfd.Sign;
using NetOfd.Sign.Sm2Strut;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// 数字签名容器测试：
/// 1) <see cref="GBT35275DSContainer"/> 签名值应遵循 GB/T 35275（ContentInfo/SignedData）并能被验证；
/// 2) <see cref="DigitalSignContainer"/> 直接 SM2 签名（兼容旧实现）。
/// 参考 ofdrw-sign 的 GBT35275DSContainer / DigitalSignContainer。
/// </summary>
public class DigitalSignContainerTest
{
    private static (Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert)
        MakeKeys()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2("NetOfd GBT Signer", kp);
        return (kp, cert);
    }

    /// <summary>GBT35275 容器：签名 → 验证 应通过。</summary>
    [Fact]
    public void Gbt35275_SignThenValidate_ShouldPass()
    {
        var (kp, cert) = MakeKeys();
        var container = new GBT35275DSContainer(cert, kp);
        Assert.Equal("Sign", container.SignType);
        Assert.Null(container.Seal);

        var data = System.Text.Encoding.UTF8.GetBytes("GB/T 35275 数字签名测试数据");
        var signedValue = container.Sign(data, "Doc_0/Signs/Sign_1/Signature.xml");

        // 签名值应为 ContentInfo(contentType=SignedData)
        var ci = ContentInfo.GetInstance(
            Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(signedValue));
        Assert.Equal(OIDs.SignedData, ci.ContentType);

        var vr = GBT35275Validate.Validate(data, signedValue);
        Assert.True(vr.Result, $"验证失败：{vr.Hit}");
    }

    /// <summary>GBT35275 容器：篡改原文后验证应失败。</summary>
    [Fact]
    public void Gbt35275_ValidateTamperedData_ShouldFail()
    {
        var (kp, cert) = MakeKeys();
        var container = new GBT35275DSContainer(cert, kp);
        var data = System.Text.Encoding.UTF8.GetBytes("待签名原文内容");
        var signedValue = container.Sign(data, "Doc_0/Signs/Sign_1/Signature.xml");

        var tampered = System.Text.Encoding.UTF8.GetBytes("被篡改的原文内容");
        var vr = GBT35275Validate.Validate(tampered, signedValue);
        Assert.False(vr.Result);
        Assert.Contains("不一致", vr.Hit);
    }

    /// <summary>GBT35275 容器：EnableFileHashBase64（兼容模式）也应通过。</summary>
    [Fact]
    public void Gbt35275_EnableFileHashBase64_ShouldPass()
    {
        var (kp, cert) = MakeKeys();
        var container = new GBT35275DSContainer(cert, kp) { EnableFileHashBase64 = true };
        var data = System.Text.Encoding.UTF8.GetBytes("Base64 摘要兼容模式");
        var signedValue = container.Sign(data, "Doc_0/Signs/Sign_1/Signature.xml");

        var vr = GBT35275Validate.Validate(data, signedValue);
        Assert.True(vr.Result, $"验证失败：{vr.Hit}");
    }

    /// <summary>DigitalSignContainer：直接 SM2 签名，可用公钥验证。</summary>
    [Fact]
    public void DigitalSignContainer_RoundTrip_ShouldVerify()
    {
        var (kp, _) = MakeKeys();
        var container = new DigitalSignContainer(kp);
        Assert.Equal("Sign", container.SignType);
        Assert.Null(container.Seal);

        var data = System.Text.Encoding.UTF8.GetBytes("旧版数字签名数据");
        var sig = container.Sign(data, "Doc_0/Signs/Sign_1/Signature.xml");
        Assert.True(GmCrypto.Sm2Verify(data, sig, kp.Public));
        Assert.False(GmCrypto.Sm2Verify(System.Text.Encoding.UTF8.GetBytes("篡改数据"), sig, kp.Public));
    }

    /// <summary>Sm2Strut 结构：ContentInfo 可往返编码。</summary>
    [Fact]
    public void Sm2Strut_ContentInfoRoundTrip_ShouldPreserve()
    {
        var inner = new Org.BouncyCastle.Asn1.DerOctetString(new byte[] { 1, 2, 3 });
        var ci = new ContentInfo(OIDs.Data, inner);
        var bytes = ci.ToAsn1Object().GetEncoded();
        var ci2 = ContentInfo.GetInstance(Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(bytes));
        Assert.Equal(OIDs.Data, ci2.ContentType);
        Assert.NotNull(ci2.Content);
        var oct = Org.BouncyCastle.Asn1.DerOctetString.GetInstance(ci2.Content!);
        Assert.Equal(new byte[] { 1, 2, 3 }, oct.GetOctets());
    }
}