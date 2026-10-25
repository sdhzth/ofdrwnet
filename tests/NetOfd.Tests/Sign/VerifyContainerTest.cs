using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NetOfd.Sign;
using NetOfd.Sign.Sm2Strut;
using NetOfd.Sign.Verify;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// 验证容器（ISignedDataValidateContainer）、SES 版本解析与印章匹配检查的测试。
/// 参考 ofdrw-sign verify（SignedDataValidateContainer / SESV1/V4/V5ValidateContainer /
/// GBT35275ValidateContainer / DigitalValidateContainer）与 ofdrw-gm ses.parse.VersionParser。
/// </summary>
public class VerifyContainerTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_verify_container_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Res(string name) =>
        Path.Combine(AppContext.BaseDirectory, "Resources", name);

    private static byte[] BuildSampleOfd()
    {
        var path = Path.Combine(OutDir(), "Sample.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("验证容器测试文档").SetFontSize(10d));
        }
        return File.ReadAllBytes(path);
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    private static byte[] BuildSeal(AsymmetricCipherKeyPair sealKp, Org.BouncyCastle.X509.X509Certificate sealCert,
        string sealName, int version = 4)
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        return SesV4Seal.Build(
            esId: "SEAL-VC", vid: "v1.0", sealName: sealName, sealType: SesV4Seal.TypeUnit,
            pictureData: png, pictureType: "PNG", widthMm: 30, heightMm: 30,
            sealCert: sealCert, sealKeyPair: sealKp,
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

    private static byte[] ReplaceEntry(byte[] ofd, string entryName, byte[] newContent)
    {
        using var ms = new MemoryStream();
        ms.Write(ofd, 0, ofd.Length);
        ms.Position = 0;
        using (var za = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = za.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/') == entryName);
            Assert.NotNull(entry);
            entry!.Delete();
            var ne = za.CreateEntry(entryName);
            using (var s = ne.Open()) s.Write(newContent, 0, newContent.Length);
        }
        return ms.ToArray();
    }

    // ---------------- SesVersionParser ----------------

    /// <summary>版本解析：自建 V4/V5 印章与 Java 生成的 UserV4.esl 应识别正确。</summary>
    [Fact]
    public void SesVersionParser_ShouldParseSealVersions()
    {
        var (sealKp, sealCert) = MakeKeys("Seal Maker V4");
        var v4Seal = BuildSeal(sealKp, sealCert, "V4 单位印章", version: 4);
        Assert.Equal(SesVersion.V4, SesVersionParser.ParseSesSealVersion(v4Seal).Version);

        var v5Seal = BuildSeal(sealKp, sealCert, "V5 单位印章", version: 5);
        Assert.Equal(SesVersion.V5, SesVersionParser.ParseSesSealVersion(v5Seal).Version);

        // Java ofdrw 生成的 SES_V4 电子印章
        Assert.Equal(SesVersion.V4, SesVersionParser.ParseSesSealVersion(File.ReadAllBytes(Res("UserV4.esl"))).Version);

        // 非法结构应抛出参数异常
        Assert.ThrowsAny<ArgumentException>(() =>
            SesVersionParser.ParseSesSealVersion(new byte[] { 0x01, 0x02, 0x03 }));
    }

    /// <summary>版本解析：SES_V1/V4/V5 签章数据与 Java 生成的 SES_V4 文档应识别正确。</summary>
    [Fact]
    public void SesVersionParser_ShouldParseSignatureVersions()
    {
        var ofdBytes = BuildSampleOfd();
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert, "测试单位印章");

        // SES_V1
        var (kp1, cert1) = MakeKeys("V1 Signer");
        var signed1 = OfdSigner.Sign(ofdBytes, new SesV1Container(kp1, null, cert1), "2026-08-20 09:00:00");
        var sv1 = ReadEntry(signed1, "Doc_0/Signs/Sign_1/SignedValue.dat");
        Assert.Equal(SesVersion.V1, SesVersionParser.ParseSesSignatureVersion(sv1).Version);

        // SES_V4
        var (kp4, cert4) = MakeKeys("V4 Signer");
        var signed4 = OfdSigner.Sign(BuildSampleOfd(), new SesV4Container(kp4, sealDer, cert4)
        {
            SignatureDateTime = "2026-08-20 09:00:00"
        }, "2026-08-20 09:00:00");
        var sv4 = ReadEntry(signed4, "Doc_0/Signs/Sign_1/SignedValue.dat");
        Assert.Equal(SesVersion.V4, SesVersionParser.ParseSesSignatureVersion(sv4).Version);

        // SES_V5（内嵌印章也须为 V5，版本由印章 header 中的 version 决定）
        var (kp5, cert5) = MakeKeys("V5 Signer");
        var v5SealDer = BuildSeal(sealKp, sealCert, "V5 测试单位印章", version: 5);
        var signed5 = OfdSigner.Sign(BuildSampleOfd(), new SesV5Container(kp5, v5SealDer, cert5)
        {
            SignatureDateTime = "2026-08-20 09:00:00"
        }, "2026-08-20 09:00:00");
        var sv5 = ReadEntry(signed5, "Doc_0/Signs/Sign_1/SignedValue.dat");
        Assert.Equal(SesVersion.V5, SesVersionParser.ParseSesSignatureVersion(sv5).Version);

        // Java ofdrw 生成的 SES_V4 签名文档（签名目录为 Sign_0）
        var javaOfd = File.ReadAllBytes(Res("SESV4SignDoc.ofd"));
        var javaSv = ReadEntry(javaOfd, "Doc_0/Signs/Sign_0/SignedValue.dat");
        Assert.Equal(SesVersion.V4, SesVersionParser.ParseSesSignatureVersion(javaSv).Version);
    }

    // ---------------- GBT35275ValidateContainer ----------------

    /// <summary>GB/T 35275 验证容器：合法签名通过，篡改原文或类型错误时失败。</summary>
    [Fact]
    public void Gbt35275Container_ShouldValidateAndReject()
    {
        var (kp, cert) = MakeKeys("GB Signer");
        var container = new GBT35275DSContainer(cert, kp);
        byte[] tbs = Encoding.UTF8.GetBytes("<Signature><Base>验证容器</Base></Signature>");
        byte[] sv = container.Sign(tbs, "/Doc_0/Signs/Sign_1/Signature.xml");

        // 合法签名通过
        new Gbt35275ValidateContainer().Validate("Sign", "SM3withSM2", tbs, sv);

        // 类型错误
        Assert.Throws<ArgumentException>(() =>
            new Gbt35275ValidateContainer().Validate("Seal", "", tbs, sv));

        // 篡改待签原文 → InvalidSignedValueException
        byte[] tampered = Encoding.UTF8.GetBytes("<Signature><Base>被篡改</Base></Signature>");
        Assert.Throws<InvalidSignedValueException>(() =>
            new Gbt35275ValidateContainer().Validate("Sign", "", tampered, sv));
    }

    // ---------------- SES_V1 / V4 / V5 ValidateContainer ----------------

    /// <summary>SES_V1 验证容器：电子签章通过；数字签名类型与篡改值被拒绝。</summary>
    [Fact]
    public void SesV1Container_ShouldValidateAndReject()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys("V1 Container Signer");
        var signed = OfdSigner.Sign(ofdBytes, new SesV1Container(kp, null, cert), "2026-08-20 09:00:00");

        byte[] desc = ReadEntry(signed, "Doc_0/Signs/Sign_1/Signature.xml");
        byte[] sv = ReadEntry(signed, "Doc_0/Signs/Sign_1/SignedValue.dat");

        new SesV1ValidateContainer().Validate("Seal", "", desc, sv);

        Assert.Throws<ArgumentException>(() =>
            new SesV1ValidateContainer().Validate("Sign", "", desc, sv));

        byte[] bad = (byte[])sv.Clone();
        bad[bad.Length - 1] ^= 0xFF;
        Assert.Throws<InvalidSignedValueException>(() =>
            new SesV1ValidateContainer().Validate("Seal", "", desc, bad));
    }

    /// <summary>SES_V4/V5 验证容器：结构一致（与 Java 容器行为一致），均可验证 V4/V5 签章数据。</summary>
    [Fact]
    public void SesV4V5Containers_ShouldValidateBothVersions()
    {
        var ofdBytes = BuildSampleOfd();
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert, "版本测试印章");

        var (kp4, cert4) = MakeKeys("V4 Signer");
        var signed4 = OfdSigner.Sign(ofdBytes, new SesV4Container(kp4, sealDer, cert4)
        {
            SignatureDateTime = "2026-08-20 09:00:00"
        }, "2026-08-20 09:00:00");
        byte[] desc4 = ReadEntry(signed4, "Doc_0/Signs/Sign_1/Signature.xml");
        byte[] sv4 = ReadEntry(signed4, "Doc_0/Signs/Sign_1/SignedValue.dat");

        // V4 数据：两个容器均应验证通过
        new SesV4ValidateContainer().Validate("Seal", "", desc4, sv4);
        new SesV5ValidateContainer().Validate("Seal", "", desc4, sv4);

        // 类型错误
        Assert.Throws<ArgumentException>(() =>
            new SesV4ValidateContainer().Validate("Sign", "", desc4, sv4));

        // V5 数据（内嵌 V5 印章）
        var v5SealDer = BuildSeal(sealKp, sealCert, "V5 版本测试印章", version: 5);
        var (kp5, cert5) = MakeKeys("V5 Signer");
        var signed5 = OfdSigner.Sign(BuildSampleOfd(), new SesV5Container(kp5, v5SealDer, cert5)
        {
            SignatureDateTime = "2026-08-20 09:00:00"
        }, "2026-08-20 09:00:00");
        byte[] desc5 = ReadEntry(signed5, "Doc_0/Signs/Sign_1/Signature.xml");
        byte[] sv5 = ReadEntry(signed5, "Doc_0/Signs/Sign_1/SignedValue.dat");

        // V5 数据：两个容器均应验证通过
        new SesV5ValidateContainer().Validate("Seal", "", desc5, sv5);
        new SesV4ValidateContainer().Validate("Seal", "", desc5, sv5);

        // 篡改签章值 → InvalidSignedValueException
        byte[] bad = (byte[])sv5.Clone();
        bad[bad.Length - 1] ^= 0xFF;
        Assert.Throws<InvalidSignedValueException>(() =>
            new SesV5ValidateContainer().Validate("Seal", "", desc5, bad));
    }

    // ---------------- DigitalValidateContainer（已废弃的裸 SM2） ----------------

    /// <summary>Digital 验证容器：外部公钥验签通过；篡改与类型错误被拒绝。</summary>
    [Fact]
#pragma warning disable CS0618
    public void DigitalContainer_ShouldValidateWithExternalPublicKey()
    {
        var (kp, _) = MakeKeys("Digital Signer");
        byte[] tbs = Encoding.UTF8.GetBytes("裸 SM2 数字签名原文");
        byte[] sig = GmCrypto.Sm2Sign(tbs, kp.Private);

        new DigitalValidateContainer(kp.Public).Validate("Sign", "SM3withSM2", tbs, sig);

        byte[] bad = (byte[])sig.Clone();
        bad[bad.Length - 1] ^= 0xFF;
        Assert.Throws<InvalidSignedValueException>(() =>
            new DigitalValidateContainer(kp.Public).Validate("Sign", "", tbs, bad));

        Assert.Throws<ArgumentException>(() =>
            new DigitalValidateContainer(kp.Public).Validate("Seal", "", tbs, sig));
    }
#pragma warning restore CS0618

    // ---------------- OfdValidator 印章匹配检查 ----------------

    /// <summary>替换 Seal.esl 为另一枚合法印章后，整体验证应因印章不匹配而失败。</summary>
    [Fact]
    public void OfdValidator_SealMismatch_ShouldFail()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("Match Signer");
        var (sealKp, sealCert) = MakeKeys("Real Seal Maker");
        var realSeal = BuildSeal(sealKp, sealCert, "真实印章");

        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-20 09:00:00", seal: Asn1Object.FromByteArray(realSeal), isSeal: true, version: 4);

        // 未篡改时通过
        Assert.True(OfdValidator.Verify(signed).AllValid, "原始签章文档应验证通过");

        // 用另一枚合法印章替换 Seal.esl → 印章不匹配
        var (fakeKp, fakeCert) = MakeKeys("Fake Seal Maker");
        var fakeSeal = BuildSeal(fakeKp, fakeCert, "伪造印章");
        var tampered = ReplaceEntry(signed, "Doc_0/Signs/Sign_1/Seal.esl", fakeSeal);

        var result = OfdValidator.Verify(tampered);
        Assert.False(result.AllValid, "印章被替换后验证应失败");
        Assert.Contains("不匹配", result.Signatures[0].Error);
    }

    /// <summary>删除 Seal.esl 后，整体验证应报告印章文件缺失。</summary>
    [Fact]
    public void OfdValidator_MissingSealFile_ShouldFail()
    {
        var ofdBytes = BuildSampleOfd();
        var (signerKp, signerCert) = MakeKeys("Missing Seal Signer");
        var (sealKp, sealCert) = MakeKeys("Seal Maker");
        var sealDer = BuildSeal(sealKp, sealCert, "缺失测试印章");

        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-20 09:00:00", seal: Asn1Object.FromByteArray(sealDer), isSeal: true, version: 4);

        // 删除 Seal.esl 条目
        using var ms = new MemoryStream();
        ms.Write(signed, 0, signed.Length);
        ms.Position = 0;
        byte[] removed;
        using (var za = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            var entry = za.Entries.FirstOrDefault(e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/Seal.esl");
            Assert.NotNull(entry);
            entry!.Delete();
        }
        removed = ms.ToArray();

        var result = OfdValidator.Verify(removed);
        Assert.False(result.AllValid);
        Assert.Contains("电子印章文件缺失", result.Signatures[0].Error);
    }

    // ---------------- 异常模型 ----------------

    /// <summary>FileIntegrityException 应携带文件位置与预期/实际杂凑（Base64）。</summary>
    [Fact]
    public void FileIntegrityException_ShouldCarryDetails()
    {
        var expect = GmCrypto.Sm3(Encoding.UTF8.GetBytes("expect"));
        var actual = GmCrypto.Sm3(Encoding.UTF8.GetBytes("actual"));
        var ex = new FileIntegrityException("/Doc_0/Content.xml", expect, actual);

        Assert.Equal("/Doc_0/Content.xml", ex.FileRef);
        Assert.Equal(Convert.ToBase64String(expect), ex.ExpectDataHash);
        Assert.Equal(Convert.ToBase64String(actual), ex.ActualDataHash);
        Assert.IsAssignableFrom<OfdVerifyException>(ex);

        var notSign = new DocNotSignException("未找到任何签名");
        Assert.IsAssignableFrom<OfdVerifyException>(notSign);
        var invalid = new InvalidSignedValueException("签名失效");
        Assert.IsAssignableFrom<OfdVerifyException>(invalid);
    }
}