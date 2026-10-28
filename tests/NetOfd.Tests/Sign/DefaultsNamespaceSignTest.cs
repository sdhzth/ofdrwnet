using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// 覆盖「<c>defaults:ofd</c> 命名空间声明」与「数字签名 / 文件杂凑」之间的<b>字节一致性</b>约束。
/// <para>
/// 背景：GB/T 33190-2016 §7.1 要求包内各 XML 根节点声明 <c>defaults:ofd</c>；
/// 而签名的 References 杂凑是对<b>文件字节</b>计算的。若声明注入发生在杂凑/签名<b>之后</b>，
/// 签名值将与实际字节不符 → 签名验证失败。故此处以「签名后仍能通过验证」为探针锁死该约束。
/// </para>
/// </summary>
public class DefaultsNamespaceSignTest
{
    private const string Ns = "http://www.ofdspec.org/2016";

    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_defaults_sign_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static byte[] BuildSampleOfd(string name)
    {
        var path = Path.Combine(OutDir(), name);
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new OFDDoc(path))
        {
            doc.Add(new Paragraph().AddText("命名空间声明与签名一致性探针").SetFontSize(10d));
            doc.Add(new Paragraph().AddText("杂凑须覆盖注入声明后的实际字节。").SetFontSize(6d));
        }
        return File.ReadAllBytes(path);
    }

    private static (Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair KeyPair,
                     Org.BouncyCastle.X509.X509Certificate Cert) MakeKeys()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2("NetOfd Defaults Signer", kp);
        return (kp, cert);
    }

    private static Dictionary<string, byte[]> ReadEntries(byte[] ofd)
    {
        var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        foreach (var e in za.Entries)
        {
            using var s = e.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            map[e.FullName] = buf.ToArray();
        }
        return map;
    }

    // ────────────────────────────────────────────────────────
    //  一、签名产物：声明齐备 + 签名有效
    // ────────────────────────────────────────────────────────

    [Fact]
    public void SignedOfd_AllOfdXmlHasDefaultsDeclaration_AndSignatureValid()
    {
        var unsigned = BuildSampleOfd("unsigned.ofd");
        var (kp, cert) = MakeKeys();

        var signed = OfdSigner.Sign(unsigned, kp, cert, "2026-08-08 10:00:00");
        Assert.NotNull(signed);

        // 1) 签名验证必须通过（注入若发生在杂凑之后，此处会失败）
        var verify = OfdValidator.Verify(signed!);
        Assert.True(verify.AllValid, "签名验证失败：声明注入与杂凑计算顺序不一致");
        Assert.Equal(1, verify.SignatureCount);
        Assert.Equal(0, verify.Signatures[0].ReferenceMismatch);

        // 2) 包内 OFD 命名空间的 XML 须带 defaults 声明
        var entries = ReadEntries(signed!);
        foreach (var (name, content) in entries)
        {
            if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var text = Encoding.UTF8.GetString(content);
            if (!text.Contains(Ns)) continue;

            Assert.True(text.Contains("xmlns:defaults=\"" + Ns + "\""),
                $"签名产物条目 {name} 缺少 xmlns:defaults 声明");
        }
    }

    [Fact]
    public void SignedOfd_ElementPrefixStaysOfd_NotDefaults()
    {
        var unsigned = BuildSampleOfd("prefix.ofd");
        var (kp, cert) = MakeKeys();
        var signed = OfdSigner.Sign(unsigned, kp, cert, "2026-08-08 10:00:00");

        foreach (var (name, content) in ReadEntries(signed!))
        {
            if (!name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var text = Encoding.UTF8.GetString(content);
            if (!text.Contains(Ns)) continue;

            // 元素前缀必须是 ofd: —— LINQ 序列化会把前缀改写为 defaults:，
            // 签名模块已做文本层还原，此处锁死该行为
            Assert.False(System.Text.RegularExpressions.Regex.IsMatch(text, @"<\s*/?defaults:"),
                $"签名产物条目 {name} 的元素前缀被改写为 defaults:");
        }
    }

    [Fact]
    public void SignedOfd_RootNodeDeclaresBothOfdAndDefaults()
    {
        var unsigned = BuildSampleOfd("rootdecl.ofd");
        var (kp, cert) = MakeKeys();
        var signed = OfdSigner.Sign(unsigned, kp, cert, "2026-08-08 10:00:00");

        var entries = ReadEntries(signed!);
        var ofdXml = Encoding.UTF8.GetString(entries["OFD.xml"]);

        // OFD.xml 经过签名重写（LINQ 路径），须仍满足「标识符宜为 ofd」
        Assert.Contains("<ofd:OFD", ofdXml);
        Assert.Contains("xmlns:ofd=\"" + Ns + "\"", ofdXml);
        Assert.Contains("xmlns:defaults=\"" + Ns + "\"", ofdXml);
    }

    // ────────────────────────────────────────────────────────
    //  二、杂凑与实际字节一致（篡改可检出）
    // ────────────────────────────────────────────────────────

    [Fact]
    public void SignedOfd_TamperedProtectedFile_IsDetected()
    {
        var unsigned = BuildSampleOfd("tamper.ofd");
        var (kp, cert) = MakeKeys();
        var signed = OfdSigner.Sign(unsigned, kp, cert, "2026-08-08 10:00:00");

        // 篡改受保护文件（Document.xml）
        var entries = ReadEntries(signed!);
        var docKey = entries.Keys.First(k => k.EndsWith("/Document.xml", StringComparison.OrdinalIgnoreCase));
        entries[docKey] = Encoding.UTF8.GetBytes(
            "<?xml version=\"1.0\" encoding=\"UTF-8\"?><ofd:Document xmlns:ofd=\"" + Ns + "\"/>");

        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
            foreach (var (name, content) in entries)
            {
                var e = za.CreateEntry(name);
                using var s = e.Open();
                s.Write(content, 0, content.Length);
            }
        var tampered = ms.ToArray();

        var verify = OfdValidator.Verify(tampered);
        Assert.False(verify.AllValid, "篡改后仍验证通过 → 杂凑未覆盖实际字节");
        Assert.True(verify.Signatures[0].ReferenceMismatch > 0);
    }

    [Fact]
    public void SignedOfd_AllProtectedFilesHashMatchesActualBytes()
    {
        var unsigned = BuildSampleOfd("hashmatch.ofd");
        var (kp, cert) = MakeKeys();
        var signed = OfdSigner.Sign(unsigned, kp, cert, "2026-08-08 10:00:00");

        var entries = ReadEntries(signed!);
        var verify = OfdValidator.Verify(signed!);

        // 逐条核对：签名描述文件中记录的杂凑须等于包内实际字节的 SM3
        var descKey = entries.Keys.First(k => k.EndsWith("/Signature.xml", StringComparison.OrdinalIgnoreCase));
        var desc = XDocument.Parse(Encoding.UTF8.GetString(entries[descKey]));
        var ofdNs = XNamespace.Get(Ns);
        var refs = desc.Descendants(ofdNs + "Reference").ToList();
        Assert.NotEmpty(refs);

        foreach (var r in refs)
        {
            string fileRef = (string)r.Attribute("FileRef")!;
            string checkValue = r.Element(ofdNs + "CheckValue")!.Value;

            var key = fileRef.TrimStart('/');
            Assert.True(entries.ContainsKey(key), $"签名引用了包内不存在的文件 {fileRef}");

            var actual = Convert.ToBase64String(GmCrypto.Sm3(entries[key]));
            Assert.Equal(actual, checkValue);
        }
    }
}
