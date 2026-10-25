using System;
using System.IO;
using System.Linq;
using NetOfd.Sign;
using NetOfd.Sign.Sm2Strut;
using NetOfd.Sign.Verify;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// 签名预检查（SignatureTerminateException）与扩展签名容器接口（IExtendSignatureContainer）的测试。
/// 参考 ofdrw-sign 的 OFDSigner.preChecker 与 ExtendSignatureContainer。
/// </summary>
public class SignPreCheckTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_sign_precheck_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static byte[] BuildSampleOfd()
    {
        var path = Path.Combine(OutDir(), "Sample.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("签名预检查测试文档").SetFontSize(10d));
        }
        return File.ReadAllBytes(path);
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    // ---------------- 签名预检查 ----------------

    /// <summary>整体保护签名后，文档再次签名应抛出 SignatureTerminateException。</summary>
    [Fact]
    public void WholeProtected_ThenResign_ShouldThrowTerminate()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp1, cert1) = MakeKeys("First Signer");
        var (kp2, cert2) = MakeKeys("Second Signer");

        var signedOnce = OfdSigner.Sign(ofdBytes, new GBT35275DSContainer(cert1, kp1), "2026-08-21 09:00:00");

        // 整体保护模式将 Signatures.xml 纳入保护，不允许续签
        var ex = Assert.Throws<SignatureTerminateException>(() =>
            OfdSigner.Sign(signedOnce, new GBT35275DSContainer(cert2, kp2), "2026-08-21 10:00:00"));
        Assert.Contains("已经被保护", ex.Message);
    }

    /// <summary>续签模式链式追加多个签名均应允许且验证通过。</summary>
    [Fact]
    public void ContinueSign_Chain_ShouldAllowMultipleSigns()
    {
        var ofdBytes = BuildSampleOfd();
        var signed = ofdBytes;
        for (int i = 1; i <= 3; i++)
        {
            var (kp, cert) = MakeKeys($"Chain Signer {i}");
            var container = new GBT35275DSContainer(cert, kp);
            signed = OfdSigner.Sign(signed, container, $"2026-08-21 0{i}:00:00",
                signMode: SignMode.ContinueSign);
        }

        var result = OfdValidator.Verify(signed);
        var detail = result.Error ?? string.Join(" | ", result.Signatures.Select(s => $"[{s.SignId}] valid={s.Valid} err={s.Error}"));
        Assert.True(result.AllValid, detail);
        Assert.Equal(3, result.SignatureCount);
        Assert.Equal(new[] { "s001", "s002", "s003" }, result.Signatures.Select(s => s.SignId).ToArray());
    }

    /// <summary>通过过滤器排除 Signatures.xml 后，文档仍可继续追加签名。</summary>
    [Fact]
    public void FilterExcludingSignaturesXml_ShouldAllowResign()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp1, cert1) = MakeKeys("Filtered Signer One");
        var (kp2, cert2) = MakeKeys("Filtered Signer Two");

        // 第一次签名时排除签名列表文件
        var signedOnce = OfdSigner.Sign(ofdBytes, new GBT35275DSContainer(cert1, kp1), "2026-08-21 09:00:00",
            protectFileFilter: absPath => !absPath.EndsWith("/Signatures.xml"));

        // 第二次签名应被允许
        var signedTwice = OfdSigner.Sign(signedOnce, new GBT35275DSContainer(cert2, kp2), "2026-08-21 10:00:00",
            signMode: SignMode.ContinueSign);

        var result = OfdValidator.Verify(signedTwice);
        Assert.True(result.AllValid, result.Error);
        Assert.Equal(2, result.SignatureCount);
    }

    /// <summary>未签名文档可直接签名（预检查不应误报）。</summary>
    [Fact]
    public void UnsignedDoc_ShouldSignDirectly()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys("Fresh Signer");
        var signed = OfdSigner.Sign(ofdBytes, new GBT35275DSContainer(cert, kp), "2026-08-21 09:00:00");
        Assert.True(OfdValidator.Verify(signed).AllValid, "首次签名应成功且验证通过");
    }

    // ---------------- IExtendSignatureContainer 接口 ----------------

    /// <summary>容器接口：摘要算法（SM3）、算法 OID、流式与字节签名结果一致。</summary>
    [Fact]
    public void ExtendSignatureContainer_Members_ShouldWork()
    {
        var (kp, cert) = MakeKeys("Interface Signer");
        ISignContainer container = new GBT35275DSContainer(cert, kp);

        // 摘要功能应为 SM3 且每次调用返回新实例
        byte[] data = System.Text.Encoding.UTF8.GetBytes("扩展签名容器接口测试数据");
        IDigest d1 = container.GetDigestFnc();
        IDigest d2 = container.GetDigestFnc();
        Assert.NotSame(d1, d2);
        var actual = new byte[d1.GetDigestSize()];
        d1.BlockUpdate(data, 0, data.Length);
        d1.DoFinal(actual, 0);
        Assert.Equal(GmCrypto.Sm3(data), actual);

        // 签名方法 OID 应为 SM2withSM3
        Assert.Equal("1.2.156.10197.1.501", container.GetSignAlgOid());

        // 流式签名与字节签名应均可通过验证（SM2 签名含随机数，结果不逐字节相同）
        byte[] viaBytes = container.Sign(data, "/Doc_0/Signs/Sign_1/Signature.xml");
        byte[] viaStream;
        using (var ms = new MemoryStream(data))
        {
            IExtendSignatureContainer ext = container;
            viaStream = ext.Sign(ms, "/Doc_0/Signs/Sign_1/Signature.xml");
        }
        new Gbt35275ValidateContainer().Validate("Sign", "", data, viaBytes);
        new Gbt35275ValidateContainer().Validate("Sign", "", data, viaStream);

        // 数字签名容器无印章
        Assert.Null(container.Seal);
        Assert.Equal("Sign", container.SignType);
    }

    /// <summary>电子签章容器的印章字节与流式签名可用，验证通过。</summary>
    [Fact]
    public void ExtendSignatureContainer_SealContainer_ShouldStreamSignAndVerify()
    {
        var ofdBytes = BuildSampleOfd();
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 9, 9 };
        var sealDer = SesV4Seal.Build(
            esId: "SEAL-EXT", vid: "v1.0", sealName: "接口测试印章", sealType: SesV4Seal.TypeUnit,
            pictureData: png, pictureType: "PNG", widthMm: 30, heightMm: 30,
            sealCert: sealCert, sealKeyPair: sealKp,
            createDate: new DateTime(2024, 1, 1),
            validStart: new DateTime(2024, 1, 1),
            validEnd: new DateTime(2034, 1, 1));

        var (signerKp, signerCert) = MakeKeys("Interface Seal Signer");
        ISignContainer container = new SesV4Container(signerKp, sealDer, signerCert)
        {
            SignatureDateTime = "2026-08-21 09:00:00"
        };

        Assert.Equal("Seal", container.SignType);
        Assert.Equal(sealDer, container.Seal);

        // 通过流式接口完成签名（模拟外部扩展用法）
        byte[] desc = BuildDescStub();
        byte[] sv;
        using (var ms = new MemoryStream(desc))
        {
            sv = ((IExtendSignatureContainer)container).Sign(ms, "/Doc_0/Signs/Sign_1/Signature.xml");
        }
        // SES_V4 验证容器应能验证该签章值
        new SesV4ValidateContainer().Validate("Seal", "", desc, sv);

        // 常规整体签名流程不受影响
        var signed = OfdSigner.Sign(ofdBytes, container, "2026-08-21 09:00:00");
        Assert.True(OfdValidator.Verify(signed).AllValid, "SES_V4 容器签名后应验证通过");
    }

    /// <summary>构造一个最小化的签名描述文件桩（用于直接调用容器签名）。</summary>
    private static byte[] BuildDescStub()
    {
        return System.Text.Encoding.UTF8.GetBytes(
            "<Signature xmlns=\"http://www.ofdspec.org/2016\"><SignedInfo/></Signature>");
    }
}