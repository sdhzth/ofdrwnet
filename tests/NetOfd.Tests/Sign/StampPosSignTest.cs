using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Reader;
using NetOfd.Sign;
using NetOfd.Sign.StampPos;
using Xunit;

namespace NetOfd.Tests.Sign;

public class StampPosSignTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_sign_pos_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>生成带多页内容的 OFD 文件。</summary>
    private static string BuildMultiPageOfd(int pages)
    {
        var path = Out("MultiPage.ofd");
        if (File.Exists(path)) File.Delete(path);
        using (var doc = new OFDDoc(path))
        {
            for (int i = 0; i < pages; i++)
            {
                if (i > 0)
                {
                    var vPage = new VirtualPage(PageLayout.A4());
                    doc.AddVPage(vPage);
                }
                doc.Add(new Paragraph().AddText($"第 {i + 1} 页测试内容").SetFontSize(8d));
            }
        }
        return path;
    }

    private static (Org.BouncyCastle.Crypto.AsymmetricCipherKeyPair KeyPair, Org.BouncyCastle.X509.X509Certificate Cert)
        MakeKeys()
    {
        var kp = GmCrypto.GenerateSm2KeyPair();
        var cert = CertUtil.GenerateSelfSignedSm2("NetOfd StampPos Test", kp);
        return (kp, cert);
    }

    /// <summary>解析 ST_Box 字符串的左上角 X 坐标。</summary>
    private static double XOf(string box)
    {
        var parts = box.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return double.Parse(parts[0], System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public void StandFormatAtomicSignId_Sequence()
    {
        var id = new StandFormatAtomicSignId();
        Assert.Equal("s001", id.IncrementAndGet());
        Assert.Equal("s002", id.IncrementAndGet());
        Assert.Equal("s002", id.Get());
        Assert.Equal(2, id.Parse("s002"));
        Assert.Equal(2, id.Parse("2"));
    }

    [Fact]
    public void StandFormatAtomicSignId_FromMax()
    {
        var id = new StandFormatAtomicSignId("s005");
        id.SetCurrentMaxSignId("s003");
        Assert.Equal("s004", id.IncrementAndGet());
    }

    [Fact]
    public void NumberFormatAtomicSignId_NoPrefix()
    {
        var id = new NumberFormatAtomicSignId();
        Assert.Equal("1", id.IncrementAndGet());
        Assert.Equal("2", id.IncrementAndGet());
    }

    [Fact]
    public void NumberFormatAtomicSignId_ZeroPrefix()
    {
        var id = new NumberFormatAtomicSignId(enableZeroPrefix: true);
        Assert.Equal("001", id.IncrementAndGet());
        Assert.Equal("002", id.IncrementAndGet());
    }

    [Fact]
    public void SignIdParser_Formats()
    {
        Assert.Equal(1, SignIdParser.ParseIndex("s001"));
        Assert.Equal(1, SignIdParser.ParseIndex("s1"));
        Assert.Equal(5, SignIdParser.ParseIndex("5"));
    }

    [Fact]
    public void NormalStampPos_GeneratesAppearance()
    {
        var ofdPath = BuildMultiPageOfd(1);
        using var reader = new OFDReader(ofdPath);

        var pos = new NormalStampPos(1, 100, 120, 50, 50);
        var annots = pos.GetAppearance(reader, new StandFormatAtomicSignId());

        Assert.Single(annots);
        var a = annots[0];
        Assert.Equal("s001", a.Id);
        Assert.Equal("100 120 50 50", a.Boundary);
        Assert.False(string.IsNullOrEmpty(a.PageRef));
        Assert.Null(a.Clip);
    }

    [Fact]
    public void RidingStampPos_GeneratesAppearance_ForEachPage()
    {
        var ofdPath = BuildMultiPageOfd(3);
        using var reader = new OFDReader(ofdPath);

        var pos = new RidingStampPos(60, 60); // 默认右侧居中
        var annots = pos.GetAppearance(reader, new StandFormatAtomicSignId());

        Assert.Equal(3, annots.Count);
        Assert.All(annots, a =>
        {
            Assert.False(string.IsNullOrEmpty(a.PageRef));
            Assert.NotNull(a.Clip);
        });

        // 三页右骑缝：第1页的 Boundary.x 最大，随页递减
        var xs = annots.Select(a => XOf(a.Boundary)).ToList();
        Assert.True(xs[0] > xs[1] && xs[1] > xs[2], $"骑缝 X 应递减，实际: {string.Join(", ", xs)}");
    }

    [Fact]
    public void CuttingRideStampPos_GeneratesAppearance()
    {
        var ofdPath = BuildMultiPageOfd(2);
        using var reader = new OFDReader(ofdPath);

        var ratios = new List<CuttingRatio> { new() { Left = 0.4, Right = 0.6 } };
        var pos = new CuttingRideStampPos(Side.Right, null, 60, 60, 0, ratios);
        var annots = pos.GetAppearance(reader, new StandFormatAtomicSignId());

        Assert.Equal(2, annots.Count);
        Assert.All(annots, a => Assert.NotNull(a.Clip));
    }

    [Fact]
    public void SignWithAppearance_EndToEnd_VerifyPass()
    {
        var ofdPath = BuildMultiPageOfd(2);
        var (kp, cert) = MakeKeys();

        var signedBytes = OfdSigner.SignWithAppearance(
            File.ReadAllBytes(ofdPath), kp, cert,
            new List<IStampAppearance>
            {
                new NormalStampPos(1, 100, 100, 50, 50),
                new NormalStampPos(2, 120, 120, 50, 50)
            },
            signatureDateTime: "2026-08-12 09:30:00");

        // 签名描述文件中应有 2 个 StampAnnot
        byte[] sigXml;
        using (var ms = new MemoryStream(signedBytes))
        using (var za = new System.IO.Compression.ZipArchive(ms, System.IO.Compression.ZipArchiveMode.Read))
        {
            var entry = za.Entries.First(e => e.FullName.Replace('\\', '/') == "Doc_0/Signs/Sign_1/Signature.xml");
            using var s = entry.Open();
            using var buf = new MemoryStream();
            s.CopyTo(buf);
            sigXml = buf.ToArray();
        }
        var doc = XDocument.Parse(System.Text.Encoding.UTF8.GetString(sigXml));
        XNamespace ns = "http://www.ofdspec.org/2016";
        var stamps = doc.Root?.Element(ns + "SignedInfo")?.Elements(ns + "StampAnnot").ToList();
        Assert.NotNull(stamps);
        Assert.Equal(2, stamps!.Count);

        // 验证应通过
        var result = OfdValidator.Verify(signedBytes);
        Assert.True(result.AllValid, result.Error);
        Assert.Equal(1, result.SignatureCount);
    }
}