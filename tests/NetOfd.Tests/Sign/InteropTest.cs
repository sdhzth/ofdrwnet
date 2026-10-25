using System;
using System.IO;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Sign;

/// <summary>
/// 与 Java ofdrw 生成的真实样本互操作测试。
/// 样本文件来自 ofdrw-master 测试资源：
///  - SESV4SignDoc.ofd：SES_V4 电子印章签名文档
///  - UserV4.esl：SES_V4 电子印章
///  - SESealData.DER：SESeal 原始 DER
/// </summary>
public class InteropTest
{
    private static byte[] Res(string name)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Resources", name);
        if (!File.Exists(path)) throw new FileNotFoundException(path);
        return File.ReadAllBytes(path);
    }

    [Fact]
    public void Validate_RealJavaSesV4Document_ShouldPass()
    {
        var result = OfdValidator.Verify(Res("SESV4SignDoc.ofd"));
        Assert.True(result.AllValid, result.Error);
        Assert.Equal(1, result.SignatureCount);
        var s = result.Signatures[0];
        Assert.True(s.Valid, s.Error);
        Assert.False(string.IsNullOrEmpty(s.CertSubject));
        Assert.True(s.SignTime.HasValue, "应能解析签章时间");
    }

    [Fact]
    public void Parse_RealJavaV4Seal_ShouldExtractAndVerify()
    {
        var seal = SesSeal.Parse(Res("UserV4.esl"));
        Assert.Equal(4, seal.Version);
        Assert.Equal("ES", seal.HeaderId);
        Assert.False(string.IsNullOrEmpty(seal.Name));
        Assert.False(string.IsNullOrEmpty(seal.EsId));
        Assert.NotEmpty(seal.Certs);
        Assert.Equal("PNG", seal.PictureType);
        Assert.True(seal.Verify(), "真实印章自身签名应验证通过");
    }

    [Fact]
    public void Parse_RealSealDer_ShouldExtract()
    {
        // 注：该生产印章的签章证书由外部印章制作机构签发（未内嵌对应私钥/CA），
        // 此处仅验证结构解析与字段提取，不要求自身验签通过。
        var seal = SesSeal.Parse(Res("SESealData.DER"));
        Assert.False(string.IsNullOrEmpty(seal.Name));
        Assert.NotEmpty(seal.PictureData);
        Assert.NotEmpty(seal.Certs);
        Assert.NotNull(seal.Cert);
    }
}