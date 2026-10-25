using System;
using System.IO;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// SES_V5 签章与时间戳钩子测试。
/// V5 与 V4 的 ASN.1 结构一致，仅 TBS_Sign 版本号不同（5）；
/// 时间戳钩子输出的令牌以 [0] EXPLICIT BIT STRING 写入 SES_Signature。
/// </summary>
public class SesV5SignTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_sign_v5_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static byte[] BuildSampleOfd()
    {
        var path = Path.Combine(OutDir(), "SampleV5.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("SES_V5 签名测试文档").SetFontSize(10d));
        }
        return File.ReadAllBytes(path);
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    private static byte[] BuildSeal(AsymmetricCipherKeyPair sealKeyPair, Org.BouncyCastle.X509.X509Certificate sealCert)
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        return SesV4Seal.Build(
            esId: "SEAL-V5", vid: "v1.0", sealName: "V5测试单位", sealType: SesV4Seal.TypeUnit,
            pictureData: png, pictureType: "PNG", widthMm: 30, heightMm: 30,
            sealCert: sealCert, sealKeyPair: sealKeyPair,
            createDate: new DateTime(2024, 1, 1),
            validStart: new DateTime(2024, 1, 1),
            validEnd: new DateTime(2034, 1, 1),
            version: 5);
    }

    [Fact]
    public void SesV5_Sign_ThenVerify_ShouldBeValid()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("SES_V5 Signer");
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);
        var seal = Asn1Object.FromByteArray(sealDer);

        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-14 09:00:00", seal: seal, isSeal: true, version: 5);

        var result = OfdValidator.Verify(signed);
        var detail = result.Error ?? string.Join(" | ", result.Signatures.Select(s => $"[{s.SignId}] valid={s.Valid} err={s.Error}"));
        Assert.True(result.AllValid, detail);
        Assert.Equal(1, result.SignatureCount);
        Assert.Contains("SES_V5 Signer", result.Signatures[0].CertSubject);
    }

    /// <summary>SES_V5 签章值内部结构：TBS_Sign 版本号应为 5。</summary>
    [Fact]
    public void SesV5_SignedValue_TbsVersionShouldBe5()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("SES_V5 Signer");
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);
        var seal = Asn1Object.FromByteArray(sealDer);

        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-14 09:00:00", seal: seal, isSeal: true, version: 5);

        // 读取 SignedValue.dat
        byte[] svBytes;
        using (var ms = new MemoryStream(signed))
        using (var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))
        {
            var entry = za.Entries.First(e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/SignedValue.dat");
            using var s = entry.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            svBytes = buf.ToArray();
        }

        var seq = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(svBytes));
        var tbs = Asn1Sequence.GetInstance(seq[0]);
        Assert.Equal(5, DerInteger.GetInstance(tbs[0]).Value.IntValue);
    }

    /// <summary>时间戳钩子：令牌应写入签章值（[0] 标签），且验证仍通过。</summary>
    [Fact]
    public void SesV5_TimeStampHook_ShouldEmbedTokenAndVerify()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("SES_V5 Signer");
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);
        var seal = Asn1Object.FromByteArray(sealDer);

        var token = System.Text.Encoding.UTF8.GetBytes("20260814090000Z.TOKEN.20260814090100Z");
        byte[]? hookInput = null;
        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-14 09:00:00", seal: seal, isSeal: true, version: 5,
            timeStampHook: sig => { hookInput = sig; return token; });

        // 钩子应被调用并拿到 SM2 签名值
        Assert.NotNull(hookInput);
        Assert.True(hookInput!.Length > 0);

        // 签章值中应存在 [0] EXPLICIT 时间戳标签
        byte[] svBytes;
        using (var ms = new MemoryStream(signed))
        using (var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))
        {
            var entry = za.Entries.First(e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/SignedValue.dat");
            using var s = entry.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            svBytes = buf.ToArray();
        }

        var seq = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(svBytes));
        Assert.Equal(5, seq.Count); // toSign, cert, algId, signature, [0]timeStamp
        var tagged = Asn1TaggedObject.GetInstance(seq[4]);
        Assert.True(tagged.IsExplicit());
        Assert.Equal(0, tagged.TagNo);
        var tokenBitStr = DerBitString.GetInstance(tagged.GetExplicitBaseObject());
        Assert.Equal(token, tokenBitStr.GetBytes());

        // 验证仍通过
        var result = OfdValidator.Verify(signed);
        Assert.True(result.AllValid, result.Error);
    }
}