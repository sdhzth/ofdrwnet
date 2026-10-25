using NetOfd.Reader;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// OFD 签章信息（印章图片）提取测试——对齐 Java
/// <c>OFDReader.getStampAnnots()</c> + <c>reader.model.StampAnnotEntity</c>。
/// <para>Java 锚点：OFDReaderTest.getStampAnnots 用 SESV4SignDoc.ofd 断言 size==1。
/// 由于 NetOfd 依赖方向为 Sign → Reader（与 Java reader → gm 相反），
/// 对应能力落于 <see cref="StampAnnotExtractor.GetStampAnnots"/>（Sign 侧、接收 OFDReader）。</para>
/// <para>样本：SESV4SignDoc.ofd（含 1 条 V4 印章，SignedValue.dat 为 SES_V4 DER）；
/// helloworld.ofd（无签名 → 空集合）。</para>
/// </summary>
public class OFDReaderStampAnnotTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private static bool Has(string name) => File.Exists(Path.Combine(Res, name));

    private static void SkipIfMissing(string name)
    {
        if (!Has(name)) Assert.True(true, $"Java 测试资源 {name} 不存在，跳过。");
    }

    private static OFDReader Open(string name) => new(Path.Combine(Res, name));

    // ── SESV4SignDoc.ofd：Java 锚点（getStampAnnots size==1） ──

    [Fact]
    public void SignedDoc_GetStampAnnots_ShouldReturnOneV4Seal()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        var annots = StampAnnotExtractor.GetStampAnnots(reader);

        // Java OFDReaderTest.getStampAnnots 断言 size==1
        Assert.Single(annots);
        var seal = annots[0];
        Assert.Equal(SesVersion.V4, seal.Version);
        Assert.False(string.IsNullOrWhiteSpace(seal.ImgType));
        Assert.NotEmpty(seal.ImageByte);
    }

    [Fact]
    public void SignedDoc_ExtractedSeal_ShouldBeRasterImage()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        var annots = StampAnnotExtractor.GetStampAnnots(reader);
        var seal = Assert.Single(annots);
        byte[] img = seal.ImageByte;

        // 印章图片应为常见光栅格式（PNG / GIF / JPEG / BMP），带文件魔数
        bool png = img.Length > 8 && img[0] == 0x89 && img[1] == (byte)'P' && img[2] == (byte)'N' && img[3] == (byte)'G';
        bool gif = img.Length > 6 && img[0] == (byte)'G' && img[1] == (byte)'I' && img[2] == (byte)'F' && img[3] == (byte)'8';
        bool jpg = img.Length > 3 && img[0] == 0xFF && img[1] == 0xD8 && img[2] == 0xFF;
        bool bmp = img.Length > 2 && img[0] == (byte)'B' && img[1] == (byte)'M';
        Assert.True(png || gif || jpg || bmp,
            $"印章图片无已知光栅魔数，ImgType={seal.ImgType}，前 8 字节={BitConverter.ToString(img.Take(8).ToArray())}");

        // 落盘供目检（输出目录：测试 bin 下 stamp_extracted.&lt;type&gt;）
        string ext = new(seal.ImgType.Where(char.IsLetterOrDigit).ToArray());
        if (ext.Length == 0) ext = "img";
        string outFile = Path.Combine(AppContext.BaseDirectory, $"stamp_extracted.{ext.ToLowerInvariant()}");
        File.WriteAllBytes(outFile, img);
    }

    // ── helloworld.ofd：无签名 → 空集合 ──

    [Fact]
    public void NoSignatureDoc_GetStampAnnots_ShouldBeEmpty()
    {
        if (!Has("helloworld.ofd")) { SkipIfMissing("helloworld.ofd"); return; }
        using var reader = Open("helloworld.ofd");

        var annots = StampAnnotExtractor.GetStampAnnots(reader);

        Assert.Empty(annots);
    }

    // ── 纯字节级提取：SESV4SignDoc.ofd 的 SignedValue.dat 直提 ──

    [Fact]
    public void ExtractSealPicture_FromSignatureValueDer_ShouldReturnSeal()
    {
        if (!Has("SESV4SignDoc.ofd")) { SkipIfMissing("SESV4SignDoc.ofd"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        // 经签名摘要定位签名值文件并读取原始 DER
        var sigs = reader.GetDefaultSignatures();
        var sig = Assert.Single(sigs);
        var rl = reader.GetResourceLocator();
        var svXml = rl.Get(new NetOfd.Core.BasicType.STLoc(sig.BaseLoc));
        Assert.NotNull(svXml);
        var svText = svXml!.Descendants().First(e => e.Name.LocalName == "SignedValue").Value.Trim();
        var svLoc = NetOfd.Core.BasicType.STLoc.Resolve(sig.BaseLoc, svText);
        byte[]? der = rl.GetRaw(svLoc);
        Assert.NotNull(der);
        Assert.NotEmpty(der);

        var entity = StampAnnotExtractor.ExtractSealPicture(der!);
        Assert.NotNull(entity);
        Assert.Equal(SesVersion.V4, entity!.Version);
        Assert.NotEmpty(entity.ImageByte);

        // 非法输入 → null（对齐 Java 忽略语义，不抛异常）
        Assert.Null(StampAnnotExtractor.ExtractSealPicture(Array.Empty<byte>()));
        Assert.Null(StampAnnotExtractor.ExtractSealPicture(new byte[] { 0x30, 0x03, 0x02, 0x01, 0x01 }));
    }
}
