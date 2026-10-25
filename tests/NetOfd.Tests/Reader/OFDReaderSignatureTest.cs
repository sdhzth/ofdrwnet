using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// OFDReader 签名摘要读取 API 测试——对齐 Java <c>OFDReader.getDefaultDocSignaturesPath()</c> /
/// <c>getDefaultSignatures()</c>，用 Java 官方测试资源做锚点。
/// <para>样本：SESV4SignDoc.ofd（reader 官方资源，含 1 条 Seal 签名摘要，
/// Java OFDReaderTest.getStampAnnots 亦用此文档断言 size==1）；
/// helloworld.ofd（无签名 → null / 空集合）。</para>
/// </summary>
public class OFDReaderSignatureTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private static bool Has(string name) => File.Exists(Path.Combine(Res, name));

    private static void SkipIfMissing(string name)
    {
        if (!Has(name)) Assert.True(true, $"Java 测试资源 {name} 不存在，跳过。");
    }

    // ── SESV4SignDoc.ofd：Java 锚点（含 1 条 Seal 签名摘要） ──

    [Fact]
    public void SignedDoc_HasSignature_ShouldBeTrue()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }

        using var reader = new OFDReader(Path.Combine(Res, "SESV4SignDoc.ofd"));
        Assert.True(reader.HasSignature());
    }

    [Fact]
    public void SignedDoc_GetDefaultDocSignaturesPath_ShouldPointSignaturesXml()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }

        using var reader = new OFDReader(Path.Combine(Res, "SESV4SignDoc.ofd"));
        var path = reader.GetDefaultDocSignaturesPath();

        Assert.NotNull(path);
        Assert.Equal("/Doc_0/Signs/Signatures.xml", path!.Value);
    }

    [Fact]
    public void SignedDoc_GetDefaultSignatures_ShouldReturnSealEntry()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }

        using var reader = new OFDReader(Path.Combine(Res, "SESV4SignDoc.ofd"));
        var sigs = reader.GetDefaultSignatures();

        Assert.Single(sigs);
        Assert.Equal("1", sigs[0].Id);
        Assert.Equal("Seal", sigs[0].Type);
        Assert.Equal("/Doc_0/Signs/Sign_0/Signature.xml", sigs[0].BaseLoc);
    }

    // ── helloworld.ofd：无签名文档 → HasSignature false、路径 null、列表空 ──

    [Fact]
    public void PlainDoc_NoSignature_ShouldReturnNullAndEmpty()
    {
        if (!Has("helloworld.ofd")) { SkipIfMissing("helloworld.ofd"); return; }

        using var reader = new OFDReader(Path.Combine(Res, "helloworld.ofd"));
        Assert.False(reader.HasSignature());
        Assert.Null(reader.GetDefaultDocSignaturesPath());
        Assert.Empty(reader.GetDefaultSignatures());
    }
}
