using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace NetOfd.Tests.Sign;

public class SesV4SignTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_sign_v4_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static byte[] BuildSampleOfd()
    {
        var path = Path.Combine(OutDir(), "SampleV4.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("SES_V4 签名测试文档").SetFontSize(10d));
        }
        return File.ReadAllBytes(path);
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    /// <summary>构建一个最小但结构完整的 SESeal（V4）DER。</summary>
    private static byte[] BuildSeal(AsymmetricCipherKeyPair sealKeyPair, Org.BouncyCastle.X509.X509Certificate sealCert)
    {
        // 简单的 PNG 占位数据（黑色 4x4 像素）
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        return SesV4Seal.Build(
            esId: "SEAL-001", vid: "v1.0", sealName: "测试单位", sealType: SesV4Seal.TypeUnit,
            pictureData: png, pictureType: "PNG", widthMm: 30, heightMm: 30,
            sealCert: sealCert, sealKeyPair: sealKeyPair,
            createDate: new DateTime(2024, 1, 1),
            validStart: new DateTime(2024, 1, 1),
            validEnd: new DateTime(2034, 1, 1));
    }

    [Fact]
    public void SesV4_SealBuild_ProducesParseableDer()
    {
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);
        Assert.True(sealDer.Length > 50);

        // 可重新解析为 ASN1 SEQUENCE
        var seq = Org.BouncyCastle.Asn1.Asn1Sequence.GetInstance(
            Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(sealDer));
        Assert.Equal(4, seq.Count);
    }

    [Fact]
    public void SesV4_Sign_ThenVerify_ShouldBeValid()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("SES_V4 Signer");
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);

        var seal = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(sealDer);

        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-13 11:00:00", seal: seal, isSeal: true, version: 4);

        // 验证应通过（自动识别 SES_V4）
        var result = OfdValidator.Verify(signed);
        var detail = result.Error ?? string.Join(" | ", result.Signatures.Select(s => $"[{s.SignId}] valid={s.Valid} err={s.Error}"));
        Assert.True(result.AllValid, detail);
        Assert.Equal(1, result.SignatureCount);
        Assert.True(result.Signatures[0].Valid);
        Assert.Contains("SES_V4 Signer", result.Signatures[0].CertSubject);
    }

    [Fact]
    public void SesV4_Sign_WithoutSeal_ShouldThrow()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("No Seal");
        Assert.Throws<SignException>(() =>
            OfdSigner.Sign(ofdBytes, signerKp, signerCert, version: 4));
    }

    [Fact]
    public void SesV4_TamperedProtectedFile_VerifyShouldFail()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("SES_V4 Signer");
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);
        var seal = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(sealDer);

        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-13 11:00:00", seal: seal, isSeal: true, version: 4);

        // 篡改 Document.xml（保持 XML 合法，使 References 杂凑不一致）
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var ms = new MemoryStream(signed))
        using (var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))
            foreach (var e in za.Entries)
            {
                using var s = e.Open();
                using var buf = new MemoryStream();
                s.CopyTo(buf);
                map[e.FullName] = buf.ToArray();
            }
        if (map.TryGetValue("Doc_0/Document.xml", out var docBytes))
        {
            var doc = System.Xml.Linq.XDocument.Parse(System.Text.Encoding.UTF8.GetString(docBytes));
            var maxUnit = doc.Descendants().FirstOrDefault(x => x.Name.LocalName == "MaxUnitID");
            if (maxUnit is not null)
                maxUnit.Value = "999999";
            map["Doc_0/Document.xml"] = System.Text.Encoding.UTF8.GetBytes(doc.ToString());
        }

        using var outMs = new MemoryStream();
        using (var za = new System.IO.Compression.ZipArchive(outMs, System.IO.Compression.ZipArchiveMode.Create, true))
            foreach (var kv in map)
            {
                var e = za.CreateEntry(kv.Key);
                using var s = e.Open();
                s.Write(kv.Value, 0, kv.Value.Length);
            }

        var result = OfdValidator.Verify(outMs.ToArray());
        Assert.False(result.AllValid, "篡改后 SES_V4 验证应失败");
    }
}