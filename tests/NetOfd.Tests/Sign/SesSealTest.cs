using System;
using System.IO;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using Org.BouncyCastle.Crypto;
using Xunit;

namespace NetOfd.Tests.Sign;

public class SesSealTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_seal_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static (AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    private static byte[] BuildSeal(AsymmetricCipherKeyPair sealKp, Org.BouncyCastle.X509.X509Certificate sealCert)
    {
        var png = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3, 4 };
        return SesV4Seal.Build(
            esId: "SEAL-ROUNDTRIP", vid: "v1.0", sealName: "北京市测试单位", sealType: SesV4Seal.TypeUnit,
            pictureData: png, pictureType: "PNG", widthMm: 30, heightMm: 30,
            sealCert: sealCert, sealKeyPair: sealKp,
            createDate: new DateTime(2024, 5, 1, 8, 30, 0),
            validStart: new DateTime(2024, 5, 1),
            validEnd: new DateTime(2034, 5, 1));
    }

    [Fact]
    public void Parse_ExtractsAllFields()
    {
        var (sealKp, sealCert) = MakeKeys("SealMaker");
        var sealDer = BuildSeal(sealKp, sealCert);

        var seal = SesSeal.Parse(sealDer);
        Assert.Equal(4, seal.Version);
        Assert.Equal("ES", seal.HeaderId);
        Assert.Equal("SEAL-ROUNDTRIP", seal.EsId);
        Assert.Equal("北京市测试单位", seal.Name);
        Assert.Equal(SesV4Seal.TypeUnit, seal.Type);
        Assert.Equal(1, seal.CertListType);
        Assert.Single(seal.Certs);
        Assert.NotNull(seal.CreateDate);
        Assert.NotNull(seal.ValidStart);
        Assert.NotNull(seal.ValidEnd);
        Assert.Equal("PNG", seal.PictureType);
        Assert.Equal(30, seal.PictureWidth);
        Assert.Equal(30, seal.PictureHeight);
        Assert.Equal(12, seal.PictureData.Length);
        Assert.NotNull(seal.Cert);
        Assert.Equal("CN=SealMaker", seal.Cert.SubjectDN.ToString());
        Assert.True(seal.Verify(), "印章自身签名应验证通过");
    }

    [Fact]
    public void Parse_ToDer_RoundTrip()
    {
        var (sealKp, sealCert) = MakeKeys("SealMaker");
        var sealDer = BuildSeal(sealKp, sealCert);
        var seal = SesSeal.Parse(sealDer);

        var reencoded = seal.ToDer();
        var seal2 = SesSeal.Parse(reencoded);
        Assert.Equal(seal.EsId, seal2.EsId);
        Assert.Equal(seal.Name, seal2.Name);
        Assert.True(seal2.Verify(), "重新编码后的印章验签应通过");
    }

    [Fact]
    public void ParseFile_ReadsEslAndSignsOfd()
    {
        var (sealKp, sealCert) = MakeKeys("SealMaker");
        var sealDer = BuildSeal(sealKp, sealCert);

        // 模拟读取 .esl 文件
        var eslPath = Path.Combine(OutDir(), "TestUser.esl");
        File.WriteAllBytes(eslPath, sealDer);
        var seal = SesSeal.ParseFile(eslPath);
        Assert.True(seal.Verify());

        // 用读出的印章做 SES_V4 签章
        var ofdPath = Path.Combine(OutDir(), "SealSign.ofd");
        using (var doc = new OFDDoc(ofdPath))
            doc.Add(new Paragraph().AddText("带印章签章").SetFontSize(10d));
        var ofdBytes = File.ReadAllBytes(ofdPath);

        var (signerKp, signerCert) = MakeKeys("SealSigner");
        var sealObj = Org.BouncyCastle.Asn1.Asn1Object.FromByteArray(sealDer);
        var signed = OfdSigner.Sign(ofdBytes, signerKp, signerCert,
            "2026-08-13 12:00:00", seal: sealObj, isSeal: true, version: 4);

        var result = OfdValidator.Verify(signed);
        Assert.True(result.AllValid, result.Error);
        Assert.True(result.Signatures[0].Valid);
        Assert.Contains("SealSigner", result.Signatures[0].CertSubject);
    }

    [Fact]
    public void TamperedSeal_SelfVerify_ShouldFail()
    {
        var (sealKp, sealCert) = MakeKeys("SealMaker");
        var sealDer = BuildSeal(sealKp, sealCert);
        var seal = SesSeal.Parse(sealDer);

        // 篡改印章名称并重新编码（签名值仍是原始 eSealInfo 的签名）
        seal.Name = "被篡改的单位";
        var tamperedDer = seal.ToDer();
        var tampered = SesSeal.Parse(tamperedDer);

        Assert.False(tampered.Verify(), "篡改印章信息后自身签名验证应失败");
    }
}