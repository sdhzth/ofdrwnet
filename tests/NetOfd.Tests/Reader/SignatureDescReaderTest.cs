using NetOfd.Reader;
using NetOfd.Reader.Model;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// 签名描述文件结构化解析测试——SESV4SignDoc.ofd 锚点。
/// <para>
/// 签名描述文件 Sample（ofdrw-sign 2.4.0 生成）：
/// <code>
/// &lt;Signature&gt;
///   &lt;SignedValue&gt;/Doc_0/Signs/Sign_0/SignedValue.dat&lt;/SignedValue&gt;
///   &lt;SignedInfo&gt;
///     &lt;Provider ProviderName="ofdrw-sign" Company="ofdrw" Version="2.4.0"/&gt;
///     &lt;SignatureMethod&gt;1.2.156.10197.1.501&lt;/SignatureMethod&gt;
///     &lt;SignatureDateTime&gt;2026-08-04 10:08:09&lt;/SignatureDateTime&gt;
///     &lt;Seal&gt;&lt;BaseLoc&gt;/Doc_0/Signs/Sign_0/Seal.esl&lt;/BaseLoc&gt;&lt;/Seal&gt;
///     &lt;StampAnnot ID="2" Boundary="50 50 40 40" PageRef="1"/&gt;
///     &lt;References CheckMethod="SM3"&gt;
///       &lt;Reference FileRef="/OFD.xml"&gt;...SM3 base64...&lt;/Reference&gt;
///       ... 5 条 Reference ...
///     &lt;/References&gt;
///   &lt;/SignedInfo&gt;
/// &lt;/Signature&gt;
/// </code>
/// </para>
/// </summary>
public class SignatureDescReaderTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private static bool Has(string name) => File.Exists(Path.Combine(Res, name));

    private static void SkipIfMissing(string name)
    {
        if (!Has(name)) Assert.True(true, $"Java 测试资源 {name} 不存在，跳过。");
    }

    private static OFDReader Open(string name) => new(Path.Combine(Res, name));

    // ── SESV4SignDoc.ofd：完整 SignedInfo 锚定 ──

    [Fact]
    public void SignedDoc_GetDescription_ShouldParseSignedInfoTopLevel()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        var desc = SignatureDescReader.GetDescription(reader, 0);

        Assert.NotNull(desc);
        // Signature 根元素无 ID 属性 → null（容忍语义）
        Assert.Null(desc!.Id);
        Assert.Equal("/Doc_0/Signs/Sign_0/SignedValue.dat", desc.SignedValueLoc);
        Assert.NotNull(desc.SignedInfo);
    }

    [Fact]
    public void SignedDoc_GetDescription_ProviderFieldsShouldMatch()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        var desc = SignatureDescReader.GetDescription(reader, 0);
        var info = desc!.SignedInfo!;
        Assert.NotNull(info.Provider);
        Assert.Equal("ofdrw-sign", info.Provider!.ProviderName);
        Assert.Equal("ofdrw", info.Provider.Company);
        Assert.Equal("2.4.0", info.Provider.Version);

        // SM3 OID（GM/T 0003.4 SM3withSM2）
        Assert.Equal("1.2.156.10197.1.501", info.SignatureMethod);
        Assert.Equal("2026-08-04 10:08:09", info.SignatureDateTime);
        Assert.Equal("/Doc_0/Signs/Sign_0/Seal.esl", info.SealLoc);
    }

    [Fact]
    public void SignedDoc_GetDescription_StampAnnotShouldMatch()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        var desc = SignatureDescReader.GetDescription(reader, 0);
        var annot = Assert.Single(desc!.SignedInfo!.StampAnnots);
        Assert.Equal("2", annot.Id);
        Assert.Equal("1", annot.PageRef);
        // Boundary 50 50 40 40 → STBox(topLeftX=50, topLeftY=50, width=40, height=40)
        Assert.NotNull(annot.Boundary);
        Assert.Equal(50.0, annot.Boundary!.TopLeftX);
        Assert.Equal(50.0, annot.Boundary.TopLeftY);
        Assert.Equal(40.0, annot.Boundary.Width);
        Assert.Equal(40.0, annot.Boundary.Height);
    }

    [Fact]
    public void SignedDoc_GetDescription_ReferencesShouldHave5EntriesWithSM3()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        var desc = SignatureDescReader.GetDescription(reader, 0);
        var info = desc!.SignedInfo!;

        Assert.Equal("SM3", info.ReferencesCheckMethod);
        Assert.Equal(5, info.References.Count);

        // FileRef 集合断言
        var fileRefs = info.References.Select(r => r.FileRef).ToHashSet();
        Assert.Contains("/OFD.xml", fileRefs);
        Assert.Contains("/Doc_0/PublicRes.xml", fileRefs);
        Assert.Contains("/Doc_0/Pages/Page_0/Content.xml", fileRefs);
        Assert.Contains("/Doc_0/Signs/Sign_0/Seal.esl", fileRefs);
        Assert.Contains("/Doc_0/Document.xml", fileRefs);

        // 每条 Reference.CheckValue 解码后 32 字节（SM3 输出长度）
        foreach (var r in info.References)
        {
            Assert.NotEmpty(r.CheckValueBase64);
            Assert.Equal(32, r.CheckValue.Length);
        }
    }

    // ── 整体便捷 API：GetDescriptions ──

    [Fact]
    public void SignedDoc_GetDescriptions_ShouldReturnOne()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        var descs = SignatureDescReader.GetDescriptions(reader);

        Assert.Single(descs);
    }

    // ── 无签名文档 → 空集合 / null ──

    [Fact]
    public void NoSignatureDoc_GetDescriptions_ShouldBeEmpty()
    {
        if (!Has("helloworld.ofd")) { SkipIfMissing("helloworld.ofd"); return; }
        using var reader = Open("helloworld.ofd");

        Assert.Empty(SignatureDescReader.GetDescriptions(reader));
        Assert.Null(SignatureDescReader.GetDescription(reader, 0));
    }

    // ── 索引越界 → null（容忍语义） ──

    [Fact]
    public void OutOfRangeIndex_ShouldReturnNull()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        Assert.Null(SignatureDescReader.GetDescription(reader, -1));
        Assert.Null(SignatureDescReader.GetDescription(reader, 99));
    }
}