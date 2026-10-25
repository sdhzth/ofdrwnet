using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NetOfd.Crypto.Integrity;
using NetOfd.Sign;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.X509;
using Xunit;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Tests.Crypto;

/// <summary>
/// GM/T 0099-2020 §7.4 OFD 完整性保护（防夹带）协议测试。
/// 对应 Java: org.ofdrw.crypto.integrity.*
/// </summary>
public class OfdIntegrityTest
{
    private static string SamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Resources", "sample.ofd");

    private static byte[] SampleBytes => File.ReadAllBytes(SamplePath);

    private static byte[] CopyResource(string name) =>
        File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Resources", name));

    private static (AsymmetricCipherKeyPair kp, X509Certificate cert) MakeKeys(string name)
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2(name, kp);
        return (kp, cert);
    }

    private static byte[] RewriteZip(byte[] zipBytes, Action<ZipArchive> edit)
    {
        using var ms = new MemoryStream();
        ms.Write(zipBytes, 0, zipBytes.Length);
        ms.Position = 0;
        using (var za = new ZipArchive(ms, ZipArchiveMode.Update, leaveOpen: true))
        {
            edit(za);
        }
        return ms.ToArray();
    }

    private static byte[] GetEntry(byte[] zipBytes, string name)
    {
        using var ms = new MemoryStream(zipBytes);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var e = za.GetEntry(name) ?? throw new FileNotFoundException(name);
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    /// <summary>完整保护后应能通过完整性校验（字节流 API 与路径 API）。</summary>
    [Fact]
    public void Protect_ThenVerify_RoundTrip()
    {
        var (kp, cert) = MakeKeys("完整性签名人");
        var signer = new GmProtectSigner(kp.Private, cert);
        var verifier = new GmProtectVerifier();

        var protectedBytes = OfdIntegrity.Protect(SampleBytes, signer);
        Assert.True(new OfdIntegrityVerifier().Integrity(protectedBytes, verifier));

        // 路径 API 也应一致
        var src = Path.GetTempFileName() + ".ofd";
        var dst = Path.GetTempFileName() + ".ofd";
        try
        {
            File.WriteAllBytes(src, SampleBytes);
            using (var p = new OfdIntegrity(src, dst))
                p.Protect(signer);
            Assert.True(new OfdIntegrityVerifier().Integrity(dst, verifier));
        }
        finally
        {
            File.Delete(src); File.Delete(dst);
        }
    }

    /// <summary>保护后容器内应生成 OFDEntries.xml（DocEntries）与 signedvalue.dat，FileList 覆盖全部原文件。</summary>
    [Fact]
    public void Protect_ShouldWriteEntriesAndSignedValue()
    {
        var (kp, cert) = MakeKeys("完整性签名人");
        var protectedBytes = OfdIntegrity.Protect(SampleBytes, new GmProtectSigner(kp.Private, cert));

        var entriesBytes = GetEntry(protectedBytes, "OFDEntries.xml");
        var signedValue = GetEntry(protectedBytes, "signedvalue.dat");
        Assert.NotEmpty(signedValue);

        var entries = new NetOfd.Core.Integrity.OFDEntries(
            System.Xml.Linq.XElement.Parse(Encoding.UTF8.GetString(entriesBytes)));
        Assert.Equal("1", entries.GetID());
        Assert.Equal("ofdrw-crypto", entries.GetCreatorName());
        Assert.Equal("/signedvalue.dat", entries.GetSignedValueLoc().ToString());

        var files = entries.GetFileList().GetFiles();
        var locs = files.Select(f => f.GetFileLoc().ToString()).ToHashSet();
        // 应包含原始包内所有文件
        var original = ZipIOReadNames(SampleBytes);
        foreach (var loc in original) Assert.Contains("/" + loc.TrimStart('/'), locs);
        // OFDEntries.xml 与 signedvalue.dat 不应出现在文件列表中
        Assert.DoesNotContain("/OFDEntries.xml", locs);
        Assert.DoesNotContain("/signedvalue.dat", locs);
    }

    private static System.Collections.Generic.List<string> ZipIOReadNames(byte[] zipBytes)
    {
        var names = new System.Collections.Generic.List<string>();
        using var ms = new MemoryStream(zipBytes);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        names.AddRange(za.Entries
            .Where(e => !e.FullName.EndsWith('/') && !e.FullName.EndsWith('\\'))
            .Select(e => "/" + e.FullName.Replace('\\', '/').TrimStart('/')));
        return names;
    }

    /// <summary>篡改清单文件（OFDEntries.xml）内容后校验应失败。</summary>
    [Fact]
    public void Tamper_Entries_ShouldFail()
    {
        var (kp, cert) = MakeKeys("完整性签名人");
        var protectedBytes = OfdIntegrity.Protect(SampleBytes, new GmProtectSigner(kp.Private, cert));

        var tampered = RewriteZip(protectedBytes, za =>
        {
            var e = za.GetEntry("OFDEntries.xml")!;
            string xml;
            using (var s = e.Open())
            using (var r = new StreamReader(s))
                xml = r.ReadToEnd();
            e.Delete();
            var ne = za.CreateEntry("OFDEntries.xml");
            using var ws = ne.Open();
            var b = Encoding.UTF8.GetBytes(xml.Replace("CreatorName=\"ofdrw-crypto\"", "CreatorName=\"attacker\""));
            ws.Write(b, 0, b.Length);
        });

        Assert.False(new OfdIntegrityVerifier().Integrity(tampered, new GmProtectVerifier()));
    }

    /// <summary>夹带额外文件后校验应失败。</summary>
    [Fact]
    public void Tamper_AddExtraFile_ShouldFail()
    {
        var (kp, cert) = MakeKeys("完整性签名人");
        var protectedBytes = OfdIntegrity.Protect(SampleBytes, new GmProtectSigner(kp.Private, cert));

        var tampered = RewriteZip(protectedBytes, za =>
        {
            var e = za.CreateEntry("extra_malware.txt");
            using var s = e.Open();
            var b = Encoding.UTF8.GetBytes("夹带内容");
            s.Write(b, 0, b.Length);
        });

        Assert.False(new OfdIntegrityVerifier().Integrity(tampered, new GmProtectVerifier()));
    }

    /// <summary>篡改签名值文件后校验应失败。</summary>
    [Fact]
    public void Tamper_SignedValue_ShouldFail()
    {
        var (kp, cert) = MakeKeys("完整性签名人");
        var protectedBytes = OfdIntegrity.Protect(SampleBytes, new GmProtectSigner(kp.Private, cert));

        var tampered = RewriteZip(protectedBytes, za =>
        {
            var e = za.GetEntry("signedvalue.dat")!;
            e.Delete();
            var ne = za.CreateEntry("signedvalue.dat");
            using var s = ne.Open();
            var b = new byte[] { 1, 2, 3, 4, 5 };
            s.Write(b, 0, b.Length);
        });

        Assert.False(new OfdIntegrityVerifier().Integrity(tampered, new GmProtectVerifier()));
    }

    /// <summary>未被保护的普通 OFD 文件缺少 OFDEntries.xml 时应抛出异常。</summary>
    [Fact]
    public void Unprotected_File_ShouldThrow()
    {
        var ex = Assert.Throws<FileNotFoundException>(() =>
            new OfdIntegrityVerifier().Integrity(SampleBytes, new GmProtectVerifier()));
        Assert.Contains("OFDEntries.xml", ex.Message);
    }

    /// <summary>跨实现互操作：校验 Java ofdrw 生成的完整性保护文件应通过。</summary>
    [Fact]
    public void Java_Protected_File_ShouldVerify()
    {
        var javaProtected = CopyResource("hello-integrity.ofd");
        Assert.True(new OfdIntegrityVerifier().Integrity(javaProtected, new GmProtectVerifier()));
    }

    /// <summary>自定义签名实现（非国密）亦可用于完整性保护。</summary>
    [Fact]
    public void Custom_Signer_Verifier_ShouldWork()
    {
        var key = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var signer = new HmacSigner(key);
        var verifier = new HmacVerifier(key);

        var protectedBytes = OfdIntegrity.Protect(SampleBytes, signer);
        Assert.True(new OfdIntegrityVerifier().Integrity(protectedBytes, verifier));
    }

    /// <summary>自定义 HMAC 签名实现（用于验证抽象接口可用性）。</summary>
    private sealed class HmacSigner : IProtectSigner
    {
        private readonly byte[] _key;
        public HmacSigner(byte[] key) => _key = key;
        public byte[] DigestThenSign(byte[] tbs)
        {
            using var h = new System.Security.Cryptography.HMACSHA256(_key);
            return h.ComputeHash(tbs);
        }
    }

    /// <summary>自定义 HMAC 验证实现（用于验证抽象接口可用性）。</summary>
    private sealed class HmacVerifier : IProtectVerifier
    {
        private readonly byte[] _key;
        public HmacVerifier(byte[] key) => _key = key;
        public bool DigestThenVerify(byte[] ofdEntriesXml, byte[] signedValue)
        {
            using var h = new System.Security.Cryptography.HMACSHA256(_key);
            return h.ComputeHash(ofdEntriesXml).AsSpan().SequenceEqual(signedValue);
        }
    }
}