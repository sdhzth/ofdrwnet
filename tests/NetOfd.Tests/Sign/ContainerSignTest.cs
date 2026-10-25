using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// 基于 <see cref="ISignContainer"/> 的签名流程测试：
/// 使用 <see cref="GBT35275DSContainer"/> 与 <see cref="DigitalSignContainer"/> 完成整包签名，
/// 应生成 Sign_1/Signature.xml、SignedValue.dat、Signatures.xml 且验证通过；
/// 以及 <see cref="SignCleaner"/> 清理后应移除全部签名。
/// </summary>
public class ContainerSignTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_container_sign_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private static byte[] BuildSampleOfd()
    {
        var path = Out("ContainerSample.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("容器签名测试文档").SetFontSize(10d));
        }
        return File.ReadAllBytes(path);
    }

    private static (Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert)
        MakeKeys()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2("NetOfd GBT Signer", kp);
        return (kp, cert);
    }

    [Fact]
    public void Gbt35275Container_SignOfd_ThenVerify_ShouldPass()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys();
        var container = new GBT35275DSContainer(cert, kp);

        var signed = OfdSigner.Sign(ofdBytes, container, "2026-08-12 09:30:00");

        // 产物应写入包内
        using var ms = new MemoryStream(signed);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/Signature.xml");
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/SignedValue.dat");
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Signatures.xml");
        // 数字签名不应有 Seal.esl
        Assert.DoesNotContain(za.Entries, e => e.FullName.Replace('\\', '/').EndsWith("Seal.esl"));

        var result = OfdValidator.Verify(signed);
        var detail = result.Error ?? string.Join(" | ", result.Signatures.Select(s => $"[{s.SignId}] valid={s.Valid} mismatch={s.ReferenceMismatch} err={s.Error} cert={s.CertSubject}"));
        Assert.True(result.AllValid, $"验证应全部通过，实际：{detail}");
        Assert.Equal(1, result.SignatureCount);
        Assert.Equal("Sign", result.Signatures[0].Type);
        Assert.Equal(0, result.Signatures[0].ReferenceMismatch);
        Assert.Contains("NetOfd GBT Signer", result.Signatures[0].CertSubject);
    }

    /// <summary>
    /// 旧版裸 SM2 签名容器（已废弃）：能生成完整 OFD 签名结构，
    /// 但签名值无证书、无法自我验证（与 Java DigitalValidateContainer 一致，需外部公钥）。
    /// </summary>
    [Fact]
    public void DigitalSignContainer_SignOfd_ShouldProduceStructure()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, _) = MakeKeys();
        var container = new DigitalSignContainer(kp);

        var signed = OfdSigner.Sign(ofdBytes, container, "2026-08-12 09:30:00");

        using var ms = new MemoryStream(signed);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/Signature.xml");
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/SignedValue.dat");
        Assert.Contains(za.Entries, e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Signatures.xml");

        // 验证应报告 GBT35275 不兼容（裸签名已废弃）
        var result = OfdValidator.Verify(signed);
        Assert.False(result.AllValid);
        Assert.Contains("35275", result.Signatures[0].Error);
    }

    /// <summary>容器签名后，SignCleaner 应移除全部签名（无 Signatures 入口、无 Signs 目录）。</summary>
    [Fact]
    public void Clean_AfterGbt35275Sign_ShouldRemoveSignatures()
    {
        var ofdBytes = BuildSampleOfd();
        var (kp, cert) = MakeKeys();
        var container = new GBT35275DSContainer(cert, kp);
        var signed = OfdSigner.Sign(ofdBytes, container, "2026-08-12 09:30:00");

        var cleaned = SignCleaner.Clean(signed);

        using var ms = new MemoryStream(cleaned);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        Assert.DoesNotContain(za.Entries, e => e.FullName.Replace('\\', '/').StartsWith("Doc_0/Signs/"));
        // OFD.xml 中不应再有 Signatures 入口
        var ofdEntry = za.Entries.First(e => e.FullName.Replace('\\', '/') == "OFD.xml");
        using var os = ofdEntry.Open();
        using var buf = new MemoryStream();
        os.CopyTo(buf);
        var ofdText = System.Text.Encoding.UTF8.GetString(buf.ToArray());
        Assert.DoesNotContain("Signatures", ofdText);

        // 清理后无签名
        var result = OfdValidator.Verify(cleaned);
        Assert.False(result.AllValid);
        Assert.NotNull(result.Error);
    }
}