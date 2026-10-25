using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using NetOfd.Sign.Sm2Strut;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// 追加签名（续签）、Seal 元素、SES 容器与 PKCS#9 容器的测试。
/// 参考 ofdrw-sign OFDSigner（ContinueSign / WholeProtected / setRelative / ProtectFileFilter）。
/// </summary>
public class ContinueSignTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_continue_sign_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static byte[] BuildSampleOfd()
    {
        var path = Path.Combine(OutDir(), "Sample.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("追加签名测试文档").SetFontSize(10d));
        }
        return File.ReadAllBytes(path);
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    private static byte[] BuildSeal(AsymmetricCipherKeyPair sealKeyPair, Org.BouncyCastle.X509.X509Certificate sealCert,
        string? sealName = null, int version = 4)
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        return SesV4Seal.Build(
            esId: "SEAL-CS", vid: "v1.0", sealName: sealName ?? "续签测试单位", sealType: SesV4Seal.TypeUnit,
            pictureData: png, pictureType: "PNG", widthMm: 30, heightMm: 30,
            sealCert: sealCert, sealKeyPair: sealKeyPair,
            createDate: new DateTime(2024, 1, 1),
            validStart: new DateTime(2024, 1, 1),
            validEnd: new DateTime(2034, 1, 1),
            version: version);
    }

    private static byte[] ReadEntry(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var entry = za.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/') == entryName);
        Assert.NotNull(entry);
        using var s = entry!.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    /// <summary>在同一文档上追加第二个签名（续签模式），两个签名都应验证通过。</summary>
    [Fact]
    public void Append_SecondSignature_ContinueSign_ShouldBothVerify()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp1, cert1) = MakeKeys("Signer One");
        var (kp2, cert2) = MakeKeys("Signer Two");
        var c1 = new GBT35275DSContainer(cert1, kp1);
        var c2 = new GBT35275DSContainer(cert2, kp2);

        var signed1 = OfdSigner.Sign(ofdBytes, c1, "2026-08-20 09:00:00", signMode: SignMode.ContinueSign);
        var signed2 = OfdSigner.Sign(signed1, c2, "2026-08-20 10:00:00", signMode: SignMode.ContinueSign);

        // 两个签名目录并存（ReadEntry 找不到会抛异常）
        Assert.NotNull(ReadEntry(signed2, "Doc_0/Signs/Sign_1/Signature.xml"));
        Assert.NotNull(ReadEntry(signed2, "Doc_0/Signs/Sign_2/Signature.xml"));

        var result = OfdValidator.Verify(signed2);
        var detail = result.Error ?? string.Join(" | ", result.Signatures.Select(s => $"[{s.SignId}] valid={s.Valid} err={s.Error}"));
        Assert.True(result.AllValid, detail);
        Assert.Equal(2, result.SignatureCount);
        Assert.All(result.Signatures, s => Assert.True(s.Valid));
    }

    /// <summary>追加签名后 Signatures.xml 应包含两条记录与 MaxSignId。</summary>
    [Fact]
    public void Append_SecondSignature_SignaturesListShouldHaveTwoRecords()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp1, cert1) = MakeKeys("Signer One");
        var (kp2, cert2) = MakeKeys("Signer Two");
        var signed1 = OfdSigner.Sign(ofdBytes, new GBT35275DSContainer(cert1, kp1), "2026-08-20 09:00:00",
            signMode: SignMode.ContinueSign);
        var signed2 = OfdSigner.Sign(signed1, new GBT35275DSContainer(cert2, kp2), "2026-08-20 10:00:00",
            signMode: SignMode.ContinueSign);

        var listXml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(ReadEntry(signed2, "Doc_0/Signs/Signatures.xml")));
        XNamespace ns = "http://www.ofdspec.org/2016";
        var sigs = listXml.Root!.Elements(ns + "Signature").ToList();
        Assert.Equal(2, sigs.Count);
        Assert.Equal("s001", sigs[0].Attribute("ID")?.Value);
        Assert.Equal("s002", sigs[1].Attribute("ID")?.Value);
        Assert.Contains("Doc_0/Signs/Sign_2/Signature.xml", sigs[1].Attribute("BaseLoc")?.Value ?? "");
        Assert.Equal("s002", listXml.Root.Element(ns + "MaxSignId")?.Value);
    }

    /// <summary>续签第二条签名可标注 Relative（基于第一条签名）。</summary>
    [Fact]
    public void Append_SecondSignature_WithRelative_ShouldWriteAttribute()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp1, cert1) = MakeKeys("Signer One");
        var (kp2, cert2) = MakeKeys("Signer Two");
        var signed1 = OfdSigner.Sign(ofdBytes, new GBT35275DSContainer(cert1, kp1), "2026-08-20 09:00:00",
            signMode: SignMode.ContinueSign);
        var signed2 = OfdSigner.Sign(signed1, new GBT35275DSContainer(cert2, kp2), "2026-08-20 10:00:00",
            signMode: SignMode.ContinueSign, relativeId: "s001");

        var listXml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(ReadEntry(signed2, "Doc_0/Signs/Signatures.xml")));
        XNamespace ns = "http://www.ofdspec.org/2016";
        var sigs = listXml.Root!.Elements(ns + "Signature").ToList();
        Assert.Equal("s001", sigs[1].Attribute("Relative")?.Value);
    }

    /// <summary>ProtectFileFilter 排除的文件不应出现在 References 中。</summary>
    [Fact]
    public void ProtectFileFilter_ShouldExcludeFileFromReferences()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys("Filter Signer");
        var container = new GBT35275DSContainer(cert, kp);

        var signed = OfdSigner.Sign(ofdBytes, container, "2026-08-20 09:00:00",
            protectFileFilter: absPath => !absPath.Contains("Content.xml"));

        var sigXml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(ReadEntry(signed, "Doc_0/Signs/Sign_1/Signature.xml")));
        XNamespace ns = "http://www.ofdspec.org/2016";
        var refs = sigXml.Root!.Element(ns + "SignedInfo")!.Element(ns + "References")!.Elements(ns + "Reference").ToList();
        Assert.DoesNotContain(refs, r => (r.Attribute("FileRef")?.Value ?? "").Contains("Content.xml"));
        Assert.Contains(refs, r => (r.Attribute("FileRef")?.Value ?? "") == "/OFD.xml");

        // 验证仍通过（未引用的文件不会被校验）
        var result = OfdValidator.Verify(signed);
        Assert.True(result.AllValid, result.Error);
    }

    /// <summary>签章模式下 SignedInfo 应包含 &lt;Seal BaseLoc&gt; 引用。</summary>
    [Fact]
    public void Seal_Element_ShouldBeWrittenInSignedInfo()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("Seal Signer");
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);
        var seal = Asn1Object.FromByteArray(sealDer);

        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-20 09:00:00", seal: seal, isSeal: true, version: 4);

        var sigXml = XDocument.Parse(System.Text.Encoding.UTF8.GetString(ReadEntry(signed, "Doc_0/Signs/Sign_1/Signature.xml")));
        XNamespace ns = "http://www.ofdspec.org/2016";
        var sealEl = sigXml.Root!.Element(ns + "SignedInfo")!.Element(ns + "Seal");
        Assert.NotNull(sealEl);
        Assert.Equal("/Doc_0/Signs/Sign_1/Seal.esl", sealEl!.Attribute("BaseLoc")?.Value);

        var result = OfdValidator.Verify(signed);
        Assert.True(result.AllValid, result.Error);
    }

    /// <summary>SES_V1 / SES_V4 / SES_V5 容器签名后应验证通过。</summary>
    [Fact]
    public void SesContainers_ShouldSignAndVerify()
    {
        var ofdBytes = BuildSampleOfd();
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert);

        // SES_V1（无印章）
        var (kp1, cert1) = MakeKeys("SES_V1 Signer");
        var v1 = new SesV1Container(kp1, null, cert1);
        var signed1 = OfdSigner.Sign(ofdBytes, v1, "2026-08-20 09:00:00");
        Assert.True(OfdValidator.Verify(signed1).AllValid, "SES_V1 验证应通过");

        // SES_V4（印章必填）
        var (kp4, cert4) = MakeKeys("SES_V4 Signer");
        var v4 = new SesV4Container(kp4, sealDer, cert4) { SignatureDateTime = "2026-08-20 09:00:00" };
        var signed4 = OfdSigner.Sign(ofdBytes, v4, "2026-08-20 09:00:00");
        Assert.True(OfdValidator.Verify(signed4).AllValid, "SES_V4 验证应通过");

        // SES_V5（印章必填，内嵌印章也应为 V5）
        var (kp5, cert5) = MakeKeys("SES_V5 Signer");
        var v5SealDer = BuildSeal(sealKp, sealCert, "续签测试单位", version: 5);
        var v5 = new SesV5Container(kp5, v5SealDer, cert5) { SignatureDateTime = "2026-08-20 09:00:00" };
        var signed5 = OfdSigner.Sign(ofdBytes, v5, "2026-08-20 09:00:00");
        Assert.True(OfdValidator.Verify(signed5).AllValid, "SES_V5 验证应通过");
    }

    /// <summary>GBT35275PKCS9 容器：签名值含 PKCS#9 authenticatedAttributes 且能被验证。</summary>
    [Fact]
    public void Gbt35275Pkcs9Container_ShouldSignAndVerify()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys("PKCS9 Signer");
        var container = new GBT35275PKCS9DSContainer(cert, kp);
        Assert.Equal("Sign", container.SignType);
        Assert.Null(container.Seal);

        var signed = OfdSigner.Sign(ofdBytes, container, "2026-08-20 09:00:00");

        // 签名值应为 ContentInfo(SignedData) 且 signerInfo 带 authenticatedAttributes
        var sv = ReadEntry(signed, "Doc_0/Signs/Sign_1/SignedValue.dat");
        var ci = ContentInfo.GetInstance(Asn1Object.FromByteArray(sv));
        var sd = SignedData.GetInstance(ci.Content!);
        var si = SignerInfo.GetInstance(sd.SignerInfos![0]);
        Assert.NotNull(si.AuthenticatedAttributes);

        var result = OfdValidator.Verify(signed);
        Assert.True(result.AllValid, result.Error);
        Assert.Equal("Sign", result.Signatures[0].Type);
        Assert.Contains("PKCS9 Signer", result.Signatures[0].CertSubject);
    }
}