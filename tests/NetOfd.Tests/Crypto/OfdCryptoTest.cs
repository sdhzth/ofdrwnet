using System;
using System.IO;
using System.IO.Compression;
using NetOfd.Core.Crypto;
using NetOfd.Crypto;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using Org.BouncyCastle.Crypto.Parameters;
using Xunit;

namespace NetOfd.Tests.Crypto;

/// <summary>
/// GM/T 0099-2020 文档加密解密测试（SM4-CBC 内容加密 + 口令/证书 FEK 密钥管理）。
/// </summary>
public class OfdCryptoTest
{
    private static string BuildSampleOfd()
    {
        var path = Path.Combine(Path.GetTempPath(),
            "netofd_crypto_sample_" + Guid.NewGuid().ToString("N") + ".ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new Paragraph().AddText("机密文档：加密测试内容").SetFontSize(10d));
            doc.AddPage();
            doc.Add(new Paragraph().AddText("第二页内容").SetFontSize(8d));
        }
        return path;
    }

    private static byte[] ReadEntry(string ofd, string name)
    {
        using var za = ZipFile.OpenRead(ofd);
        var e = za.GetEntry(name) ?? throw new Exception("缺少条目: " + name);
        using var s = e.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    [Fact]
    public void Password_Encrypt_Decrypt_RoundTrip()
    {
        var src = BuildSampleOfd();
        var enc = Path.GetTempFileName() + ".ofd";
        var dec = Path.GetTempFileName() + ".ofd";
        try
        {
            byte[] origOfdXml = ReadEntry(src, "OFD.xml");
            byte[] origDocXml = ReadEntry(src, "Doc_0/Document.xml");

            using (new OfdEncryptor(src, enc)
                       .AddUser(new UserPasswordEncryptor("张三", "12345678"))
                       .Encrypt()) { }

            // 加密后：入口元数据出现，原明文文件被替换为 .dat 密文
            using (var za = ZipFile.OpenRead(enc))
            {
                Assert.NotNull(za.GetEntry("Encryptions.xml"));
                Assert.NotNull(za.GetEntry("decryptseed.dat"));
                Assert.NotNull(za.GetEntry("entriesmap.dat"));
                Assert.Null(za.GetEntry("OFD.xml"));
            }

            new OfdDecryptor(enc, dec)
                .AddUser(new UserPasswordDecryptor("张三", "12345678"))
                .Decrypt();

            Assert.Equal(origOfdXml, ReadEntry(dec, "OFD.xml"));
            Assert.Equal(origDocXml, ReadEntry(dec, "Doc_0/Document.xml"));
        }
        finally
        {
            File.Delete(src); File.Delete(enc); File.Delete(dec);
        }
    }

    [Fact]
    public void Cert_Encrypt_Decrypt_RoundTrip()
    {
        var src = BuildSampleOfd();
        var enc = Path.GetTempFileName() + ".ofd";
        var dec = Path.GetTempFileName() + ".ofd";
        try
        {
            var kp = GmCrypto.GenerateSm2KeyPair();
            var cert = CertUtil.GenerateSelfSignedSm2("李四", kp);
            byte[] orig = ReadEntry(src, "Doc_0/Document.xml");

            using (new OfdEncryptor(src, enc)
                       .AddUser(new UserCertEncryptor("李四", cert))
                       .Encrypt()) { }

            new OfdDecryptor(enc, dec)
                .AddUser(new UserCertDecryptor((ECPrivateKeyParameters)kp.Private))
                .Decrypt();

            Assert.Equal(orig, ReadEntry(dec, "Doc_0/Document.xml"));
        }
        finally
        {
            File.Delete(src); File.Delete(enc); File.Delete(dec);
        }
    }

    [Fact]
    public void Wrong_Password_Fails()
    {
        var src = BuildSampleOfd();
        var enc = Path.GetTempFileName() + ".ofd";
        var dec = Path.GetTempFileName() + ".ofd";
        try
        {
            using (new OfdEncryptor(src, enc)
                       .AddUser(new UserPasswordEncryptor("张三", "rightpass"))
                       .Encrypt()) { }

            Assert.Throws<CryptoException>(() =>
                new OfdDecryptor(enc, dec)
                    .AddUser(new UserPasswordDecryptor("张三", "wrongpass"))
                    .Decrypt());
        }
        finally
        {
            File.Delete(src); File.Delete(enc); File.Delete(dec);
        }
    }

    [Fact]
    public void Multi_User_BothCanDecrypt()
    {
        var src = BuildSampleOfd();
        var enc = Path.GetTempFileName() + ".ofd";
        var decP = Path.GetTempFileName() + ".ofd";
        var decC = Path.GetTempFileName() + ".ofd";
        try
        {
            var kp = GmCrypto.GenerateSm2KeyPair();
            var cert = CertUtil.GenerateSelfSignedSm2("李四", kp);
            byte[] orig = ReadEntry(src, "Doc_0/Document.xml");

            using (new OfdEncryptor(src, enc)
                       .AddUser(new UserPasswordEncryptor("张三", "pass1234"))
                       .AddUser(new UserCertEncryptor("李四", cert))
                       .Encrypt()) { }

            new OfdDecryptor(enc, decP)
                .AddUser(new UserPasswordDecryptor("张三", "pass1234"))
                .Decrypt();
            new OfdDecryptor(enc, decC)
                .AddUser(new UserCertDecryptor((ECPrivateKeyParameters)kp.Private))
                .Decrypt();

            Assert.Equal(orig, ReadEntry(decP, "Doc_0/Document.xml"));
            Assert.Equal(orig, ReadEntry(decC, "Doc_0/Document.xml"));
        }
        finally
        {
            File.Delete(src); File.Delete(enc); File.Delete(decP); File.Delete(decC);
        }
    }
}
