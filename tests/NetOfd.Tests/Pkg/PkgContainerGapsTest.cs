using System.Xml.Linq;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicType;
using NetOfd.Core.Signatures;
using NetOfd.Pkg.Container;
using Xunit;

namespace NetOfd.Tests.Pkg;

/// <summary>
/// 增量 V：Pkg 容器目录补齐（Signs/Sign_N/Annots/Temps）
/// + Core 签名标准类型（Signatures/Signature）的回归测试。
/// </summary>
public class PkgContainerGapsTest
{
    private static readonly XNamespace Ns = "http://www.ofdspec.org/2016";

    // ────────────────────────────────────────────────────────
    //  Core: Signatures
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Signatures_DefaultRootIsSignatures()
    {
        var sig = new Signatures();
        Assert.Equal("Signatures", sig.Element.Name.LocalName);
        Assert.Empty(sig.GetSignatures());
        Assert.Null(sig.MaxSignId);
    }

    [Fact]
    public void Signatures_MaxSignIdRoundTripAndRemoval()
    {
        var sig = new Signatures();
        sig.MaxSignId = "s003";
        Assert.Equal("s003", sig.MaxSignId);
        Assert.Equal("s003", sig.Element.Element(Ns + "MaxSignId")?.Value);

        // 设为 null 应移除该子元素（对齐 Java setMaxSignId(null)）
        sig.MaxSignId = null;
        Assert.Null(sig.MaxSignId);
        Assert.Null(sig.Element.Element(Ns + "MaxSignId"));
    }

    [Fact]
    public void Signatures_AddSignatureAppendsMultiple()
    {
        var sig = new Signatures();
        var s1 = new Signature();
        s1.SignedValue = new STLoc("SignedValue.dat");
        var s2 = new Signature();

        sig.AddSignature(s1).AddSignature(s2);

        Assert.Equal(2, sig.GetSignatures().Count);
        Assert.Equal("SignedValue.dat", sig.GetSignatures()[0].SignedValue?.Value);
    }

    [Fact]
    public void Signatures_AddNullSignatureIsIgnored()
    {
        var sig = new Signatures();
        sig.AddSignature(null!);
        Assert.Empty(sig.GetSignatures());
    }

    [Fact]
    public void Signatures_ParsesExistingXml()
    {
        var xml = new XElement(Ns + "Signatures",
            new XElement(Ns + "MaxSignId", "s012"),
            new XElement(Ns + "Signature",
                new XElement(Ns + "SignedValue", "SignedValue.dat")));

        var sig = new Signatures(xml);
        Assert.Equal("s012", sig.MaxSignId);
        Assert.Single(sig.GetSignatures());
        Assert.Equal("SignedValue.dat", sig.GetSignatures()[0].SignedValue?.Value);
    }

    // ────────────────────────────────────────────────────────
    //  Core: Signature
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Signature_SignedValueUsesStLoc()
    {
        var s = new Signature();
        Assert.Null(s.SignedValue);

        s.SignedValue = new STLoc("/Doc_0/Signs/Sign_0/SignedValue.dat");
        Assert.Equal("/Doc_0/Signs/Sign_0/SignedValue.dat", s.SignedValue?.Value);
        Assert.IsType<STLoc>(s.SignedValue);
    }

    [Fact]
    public void Signature_SignedInfoAccessorsCreateTree()
    {
        var s = new Signature();
        Assert.Null(s.SignedInfo);

        s.Method = "1.2.156.10197.1.501";
        s.ProviderName = "NetOfdProvider";
        s.ProviderVersion = "1.0";
        s.SignatureTime = "2026-09-15T12:00:00";

        var info = s.SignedInfo;
        Assert.NotNull(info);
        Assert.Equal("1.2.156.10197.1.501", s.Method);
        Assert.Equal("NetOfdProvider", s.ProviderName);
        Assert.Equal("1.0", s.ProviderVersion);
        Assert.Equal("2026-09-15T12:00:00", s.SignatureTime);

        // SignedInfo/Provider 下同时存在 ProviderName 与 Version
        var provider = info!.Element(Ns + "Provider");
        Assert.NotNull(provider);
        Assert.Equal("NetOfdProvider", provider!.Element(Ns + "ProviderName")?.Value);
        Assert.Equal("1.0", provider.Element(Ns + "Version")?.Value);
    }

    [Fact]
    public void Signature_SignedInfoChildRemovalOnNull()
    {
        var s = new Signature();
        s.Method = "M1";
        s.ProviderName = "P1";
        Assert.NotNull(s.SignedInfo);

        s.Method = null;
        Assert.Null(s.Method);
        Assert.Null(s.SignedInfo!.Element(Ns + "Method"));

        // 清空 ProviderName 不应连带删除 Provider 节点本身
        s.ProviderName = null;
        Assert.Null(s.ProviderName);
        Assert.NotNull(s.SignedInfo!.Element(Ns + "Provider"));
    }

    [Fact]
    public void Signature_ReadsExistingSignedInfo()
    {
        var xml = new XElement(Ns + "Signature",
            new XElement(Ns + "SignedInfo",
                new XElement(Ns + "Provider",
                    new XElement(Ns + "ProviderName", "LegacyProvider"),
                    new XElement(Ns + "Version", "2.0")),
                new XElement(Ns + "Method", "1.2.156.10197.1.501"),
                new XElement(Ns + "SignatureTime", "2026-01-01T00:00:00")),
            new XElement(Ns + "SignedValue", "SignedValue.dat"));

        var s = new Signature(xml);
        Assert.Equal("LegacyProvider", s.ProviderName);
        Assert.Equal("2.0", s.ProviderVersion);
        Assert.Equal("1.2.156.10197.1.501", s.Method);
        Assert.Equal("2026-01-01T00:00:00", s.SignatureTime);
        Assert.Equal("SignedValue.dat", s.SignedValue?.Value);
    }

    // ────────────────────────────────────────────────────────
    //  Pkg: SignsDir / SignDir
    // ────────────────────────────────────────────────────────

    [Fact]
    public void SignsDir_NewSignDirIncrementsIndexFromZero()
    {
        var signs = new SignsDir();

        var s0 = signs.NewSignDir();
        var s1 = signs.NewSignDir();

        Assert.Equal("Sign_0", s0.Name);
        Assert.Equal("Sign_1", s1.Name);
        Assert.Equal(0, s0.Index);
        Assert.Equal(1, s1.Index);
    }

    [Fact]
    public void SignsDir_NewSignDirReturnsSignDirType()
    {
        var signs = new SignsDir();
        var dir = signs.NewSignDir();

        Assert.IsType<SignDir>(dir);
        Assert.True(signs.DirExists("Sign_0"));
    }

    [Fact]
    public void SignsDir_GetByIndexZeroIsReachable()
    {
        // Java 版 getByIndex 要求 index > 0，与 newSignDir 生成的 Sign_0 自相矛盾；
        // 本实现按标准允许 index >= 0。
        var signs = new SignsDir();
        signs.NewSignDir();

        var got = signs.GetByIndex(0);
        Assert.NotNull(got);
        Assert.Equal("Sign_0", got!.Name);
        Assert.Null(signs.GetByIndex(-1));
        Assert.Null(signs.GetByIndex(99));
    }

    [Fact]
    public void SignsDir_SignaturesFileRoundTrip()
    {
        var signs = new SignsDir();
        Assert.Null(signs.GetSignatures());

        var list = signs.ObtainSignatures();
        list.MaxSignId = "s001";

        var reread = signs.GetSignatures();
        Assert.NotNull(reread);
        Assert.Equal("s001", reread!.MaxSignId);

        // 再次 Obtain 应复用同一底层 XML（而不是重建一个空的 Signatures.xml 覆盖掉 MaxSignId）
        Assert.Equal("s001", signs.ObtainSignatures().MaxSignId);
    }

    [Fact]
    public void SignDir_SignatureAndBinaryFiles()
    {
        var signs = new SignsDir();
        var sign = signs.NewSignDir();

        var sig = new Signature();
        sig.SignedValue = new STLoc("SignedValue.dat");
        sign.SetSignature(sig);

        sign.SetSeal(new byte[] { 0x01, 0x02 });
        sign.SetSignedValue(new byte[] { 0xAA, 0xBB });

        Assert.NotNull(sign.GetSignature());
        Assert.Equal("SignedValue.dat", sign.GetSignature()!.SignedValue?.Value);
        Assert.Equal(new byte[] { 0x01, 0x02 }, sign.GetSeal());
        Assert.Equal(new byte[] { 0xAA, 0xBB }, sign.GetSignedValue());
    }

    [Fact]
    public void SignDir_IndexStrToleratesNonNumericName()
    {
        var sign = new SignDir("Sign_001");
        Assert.Equal("001", sign.IndexStr);
        Assert.Equal(1, sign.Index);

        var odd = new SignDir("Sign_unknown");
        Assert.Equal("unknown", odd.IndexStr);
        Assert.Equal(-1, odd.Index); // 不抛异常，容忍第三方非规范命名
    }

    [Fact]
    public void DocDir_ObtainSignsAnnotsTempsAreCached()
    {
        var doc = new DocDir("Doc_0");

        var signs = doc.ObtainSigns();
        var annots = doc.ObtainAnnots();
        var temps = doc.ObtainTemps();

        Assert.Same(signs, doc.ObtainSigns());
        Assert.Same(annots, doc.ObtainAnnots());
        Assert.Same(temps, doc.ObtainTemps());

        Assert.Equal("Signs", signs.Name);
        Assert.Equal("Annots", annots.Name);
        Assert.Equal("Temps", temps.Name);
    }

    [Fact]
    public void SignsDir_ScansExistingSignDirsFromDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "netofd_signs_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "Sign_0"));
        Directory.CreateDirectory(Path.Combine(root, "Sign_1"));
        Directory.CreateDirectory(Path.Combine(root, "Sign_5"));
        try
        {
            var signs = new SignsDir("Signs", root);

            // 已存在 0/1/5 → 下一个应为 Sign_6
            Assert.Equal("Sign_6", signs.NewSignDir().Name);
            Assert.NotNull(signs.GetByIndex(5));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    // ────────────────────────────────────────────────────────
    //  Pkg: AnnotsDir / TempsDir
    // ────────────────────────────────────────────────────────

    [Fact]
    public void AnnotsDir_ObtainByIndexReturnsPageDir()
    {
        var annots = new AnnotsDir();
        var page = annots.ObtainByIndex(3);

        Assert.IsType<PageDir>(page);
        Assert.Equal("Page_3", page.Name);
        Assert.NotNull(annots.GetPageDir("Page_3"));
        Assert.Null(annots.GetPageDir("Page_9"));
    }

    [Fact]
    public void AnnotsDir_AnnotationsFileRoundTrip()
    {
        var annots = new AnnotsDir();
        Assert.Null(annots.GetAnnotations());

        var a = annots.ObtainAnnotations();
        a.AddPage(new NetOfd.Core.Annotation.PageAnnot.AnnPage { PageID = new STRefID(1) });

        Assert.NotNull(annots.GetAnnotations());
        // 再次 Obtain 应复用同一底层 XML（页条目不丢）
        Assert.Single(annots.ObtainAnnotations().GetPages());
        Assert.True(annots.FileExists(DocDir.ANNOTATIONS_FILE));
    }

    [Fact]
    public void TempsDir_AddPageAutoNamesAndReturnsLoc()
    {
        var temps = new TempsDir();

        var p1 = new Page { ID = new STID(1) };
        var loc1 = temps.Add(p1);
        var loc2 = temps.Add(new Page { ID = new STID(2) });

        Assert.EndsWith("Temp_0.xml", loc1.Value);
        Assert.EndsWith("Temp_1.xml", loc2.Value);
        Assert.Equal(1, temps.GetMaxTempIndex());
        Assert.NotNull(temps.Get("Temp_0.xml"));
        Assert.Equal(1L, temps.Get("Temp_0.xml")!.ID.Value);
    }

    [Fact]
    public void TempsDir_AddWithExplicitFileName()
    {
        var temps = new TempsDir();
        var loc = temps.Add("Custom.xml", new Page { ID = new STID(7) });

        Assert.EndsWith("Custom.xml", loc.Value);
        Assert.NotNull(temps.Get("Custom.xml"));
        Assert.Null(temps.Get("Missing.xml"));
    }

    [Fact]
    public void TempsDir_EmptyContainerMaxIndexIsMinusOne()
    {
        var temps = new TempsDir();
        Assert.Equal(-1, temps.GetMaxTempIndex());

        temps.Add(new Page());
        Assert.Equal(0, temps.GetMaxTempIndex());
    }

    [Fact]
    public void TempsDir_ScansExistingTempFilesFromDisk()
    {
        var root = Path.Combine(Path.GetTempPath(), "netofd_temps_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "Temp_0.xml"), "<Page/>");
        File.WriteAllText(Path.Combine(root, "Temp_4.xml"), "<Page/>");
        File.WriteAllText(Path.Combine(root, "Other.xml"), "<Page/>"); // 非模板文件，应忽略
        try
        {
            var temps = new TempsDir("Temps", root);
            Assert.Equal(4, temps.GetMaxTempIndex());

            // 下一个自增为 5
            var loc = temps.Add(new Page());
            Assert.EndsWith("Temp_5.xml", loc.Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
