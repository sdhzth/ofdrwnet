using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// OFD 数字签名/验证测试（参考 ofdrw-sign OFDSigner / OFDValidator）。
/// 流程：生成自签名 SM2 证书 → 用 NetOfd.Layout 生成 OFD → 签名 → 验证（应通过）；
/// 篡改受保护文件后再验证（应失败）。
/// </summary>
public class OfdSignTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_sign_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>生成一个带简单文本内容的 OFD 文档字节（用于签名/验证）。</summary>
    private static byte[] BuildSampleOfd()
    {
        var path = Out("SampleContent.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("OFD 数字签名测试文档").SetFontSize(10d));
            doc.Add(new Paragraph().AddText("坚持就是胜利，加油！").SetFontSize(6d));
        }
        return File.ReadAllBytes(path);
    }

    private static (Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert)
        MakeKeys()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2("NetOfd Test Signer", kp);
        return (kp, cert);
    }

    [Fact]
    public void Sign_ThenVerify_ShouldBeValid()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys();

        var signed = OfdSigner.Sign(ofdBytes, kp, cert, "2026-08-08 10:00:00");

        // 签名产物应写入包内
        using var ms = new MemoryStream(signed);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/Signature.xml");
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/SignedValue.dat");
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Signatures.xml");

        // 验证应通过
        var result = OfdValidator.Verify(signed);
        var detail = result.Error ?? string.Join(" | ", result.Signatures.Select(s => $"[{s.SignId}] valid={s.Valid} mismatch={s.ReferenceMismatch} err={s.Error} cert={s.CertSubject}"));
        Assert.True(result.AllValid, $"验证应全部通过，实际：{detail}");
        Assert.Equal(1, result.SignatureCount);
        var sig = result.Signatures[0];
        Assert.True(sig.Valid);
        Assert.Equal(0, sig.ReferenceMismatch);
        Assert.Contains("NetOfd Test Signer", sig.CertSubject);
        Assert.NotNull(sig.SignTime);
    }

    [Fact]
    public void Sign_ToFile_ThenVerify_ShouldBeValid()
    {
        var ofdPath = Out("SignToFile.ofd");
        if (File.Exists(ofdPath)) File.Delete(ofdPath);
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.Add(new Paragraph().AddText("文件版签名测试").SetFontSize(8d));
        }

        var (kp, cert) = MakeKeys();
        var signedPath = Out("SignedFile.ofd");
        if (File.Exists(signedPath)) File.Delete(signedPath);

        OfdSigner.Sign(ofdPath, signedPath, kp, cert);

        var result = OfdValidator.Verify(signedPath);
        Assert.True(result.AllValid, result.Error);
        Assert.Equal(1, result.SignatureCount);
        Assert.True(result.Signatures[0].Valid);
    }

    [Fact]
    public void Sign_ThenTamperProtectedFile_VerifyShouldFail()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys();
        var signed = OfdSigner.Sign(ofdBytes, kp, cert, "2026-08-08 10:00:00");

        // 读取已签名包到字典（受保护文件）
        var map = new System.Collections.Generic.Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var ms = new MemoryStream(signed))
        using (var za = new ZipArchive(ms, ZipArchiveMode.Read))
            foreach (var e in za.Entries)
            {
                using var s = e.Open();
                using var buf = new MemoryStream();
                s.CopyTo(buf);
                map[e.FullName] = buf.ToArray();
            }

        // 篡改一个受保护文件：将某个内容文件末尾追加字节
        bool tampered = false;
        foreach (var key in map.Keys)
        {
            var n = key.Replace('\\', '/');
            if (n.EndsWith("Doc_0/Document.xml") || n.EndsWith("Doc_0/Pages/Page_0/Content.xml"))
            {
                var original = map[key];
                var modified = new byte[original.Length + 4];
                Array.Copy(original, modified, original.Length);
                modified[original.Length] = (byte)'X';
                modified[original.Length + 1] = (byte)'X';
                modified[original.Length + 2] = (byte)'X';
                modified[original.Length + 3] = (byte)'X';
                map[key] = modified;
                tampered = true;
                break;
            }
        }
        Assert.True(tampered, "应能定位到受保护文件用于篡改");

        using var outMs = new MemoryStream();
        using (var za = new ZipArchive(outMs, ZipArchiveMode.Create, true))
        {
            foreach (var kv in map)
            {
                var e = za.CreateEntry(kv.Key);
                using var s = e.Open();
                s.Write(kv.Value, 0, kv.Value.Length);
            }
        }
        var tamperedBytes = outMs.ToArray();

        var result = OfdValidator.Verify(tamperedBytes);
        Assert.False(result.AllValid, "篡改后验证应失败");
        Assert.True(result.Signatures[0].ReferenceMismatch > 0 || !result.Signatures[0].Valid);
    }

    [Fact]
    public void Verify_WithoutSignature_ShouldReportError()
    {
        var ofdBytes = BuildSampleOfd();
        var result = OfdValidator.Verify(ofdBytes);
        Assert.False(result.AllValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void Sm2_KeyGen_Sign_Verify_RoundTrip()
    {
        var (kp, _) = MakeKeys();
        var data = System.Text.Encoding.UTF8.GetBytes("待签名的 TBS 数据");
        var sig = GmCrypto.Sm2Sign(data, kp.Private);
        Assert.True(GmCrypto.Sm2Verify(data, sig, kp.Public));
        // 篡改原文应验证失败
        var tampered = System.Text.Encoding.UTF8.GetBytes("被篡改的 TBS 数据");
        Assert.False(GmCrypto.Sm2Verify(tampered, sig, kp.Public));
    }

    /// <summary>
    /// 签章外观转注释：外观以 <c>StampAnnot</c> 注解形式写入 <c>SignedInfo</c>，
    /// 而非烤进页 Content；且校验仍应通过。
    /// </summary>
    [Fact]
    public void Sign_WithAppearanceAnnotation_WritesStampAnnotInSignedInfo()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys();
        var appearances = new List<SignatureModels.StampAnnot>
        {
            new() { Id = "s1", PageRef = "2", Boundary = "100 100 50 50" },
            new() { Id = "s2", PageRef = "2", Boundary = "200 200 40 40", Clip = "0 0 40 40" }
        };

        var signed = OfdSigner.Sign(ofdBytes, kp, cert, "2026-08-10 12:00:00", appearances: appearances);

        // 取出 Sign_1/Signature.xml 解析
        byte[] sigBytes;
        using (var ms = new MemoryStream(signed))
        using (var za = new ZipArchive(ms, ZipArchiveMode.Read))
        {
            var entry = za.Entries.First(e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/Signature.xml");
            using var s = entry.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            sigBytes = buf.ToArray();
        }

        var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(sigBytes));
        XNamespace ns = "http://www.ofdspec.org/2016";
        var signedInfo = doc.Root?.Element(ns + "SignedInfo");
        Assert.NotNull(signedInfo);

        var stamps = signedInfo!.Elements(ns + "StampAnnot").ToList();
        Assert.Equal(2, stamps.Count);

        var s1 = stamps[0];
        Assert.Equal("s1", s1.Attribute("ID")?.Value);
        Assert.Equal("2", s1.Attribute("PageRef")?.Value);
        Assert.Equal("100 100 50 50", s1.Attribute("Boundary")?.Value);
        Assert.Null(s1.Attribute("Clip"));

        var s2 = stamps[1];
        Assert.Equal("s2", s2.Attribute("ID")?.Value);
        Assert.Equal("200 200 40 40", s2.Attribute("Boundary")?.Value);
        Assert.Equal("0 0 40 40", s2.Attribute("Clip")?.Value);

        // 关键：外观坐标不得出现在页 Content.xml 中（即“转注释”而非“烤进页面”）
        using (var ms2 = new MemoryStream(signed))
        using (var za2 = new ZipArchive(ms2, ZipArchiveMode.Read))
        {
            var contentEntry = za2.Entries
                .FirstOrDefault(e => e.FullName.Replace('\\', '/') == "Doc_0/Pages/Page_0/Content.xml");
            if (contentEntry != null)
            {
                using var cs = contentEntry.Open();
                using var cbuf = new MemoryStream();
                cs.CopyTo(cbuf);
                var contentText = System.Text.Encoding.UTF8.GetString(cbuf.ToArray());
                Assert.DoesNotContain("100 100 50 50", contentText);
            }
        }

        // References 不受影响，验证应仍通过
        var result = OfdValidator.Verify(signed);
        Assert.True(result.AllValid, result.Error);
    }
}
