using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NetOfd.Core.Annotation;
using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Layout.Edit;
using NetOfd.Sign;
using Xunit;

namespace NetOfd.Tests.Robustness;

/// <summary>
/// 健壮性测试：损坏/恶意输入应抛出语义明确的异常（或返回带错误信息的验证结果），
/// 而非裸的底层异常（InvalidDataException/XmlException 直接暴露给调用方可接受，但须可区分）。
/// </summary>
public class MalformedInputTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_robust_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>最小注释绘制器（健壮性测试不关心内容）。</summary>
    private class NoOpDrawer : NetOfd.Layout.Element.Canvas.IDrawer
    {
        public void Draw(NetOfd.Layout.Element.Canvas.DrawContext ctx) { }
    }

    private static byte[] BuildSampleOfdBytes()
    {
        var path = Out("robust_base.ofd");
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph().AddText("健壮性测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    [Fact]
    public void NotAZip_ShouldThrowInvalidOperationExceptionWithClearMessage()
    {
        var garbage = Enumerable.Range(0, 4096).Select(i => (byte)(i * 31 % 251)).ToArray();
        var ex = Assert.Throws<InvalidOperationException>(
            () => OfdAnnotator.AddAnnotation(garbage, 1,
                new Annotation(1, 1, 10, 10,
                    AnnotType.Path, new NoOpDrawer())));
        Assert.Contains("ZIP", ex.Message);
    }

    [Fact]
    public void EmptyBytes_ShouldThrowInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => OfdTagger.AddCustomTag(
            Array.Empty<byte>(), "urn:test", Encoding.UTF8.GetBytes("<T/>")));
        Assert.Throws<InvalidOperationException>(() => OfdEditor.AppendPage(Array.Empty<byte>()));
    }

    [Fact]
    public void NullBytes_ShouldThrowArgumentExceptionOrInvalidOperation()
    {
        Assert.ThrowsAny<Exception>(() => OfdVersioner.GetVersions(null!));
    }

    [Fact]
    public void TruncatedZip_ShouldThrowInvalidOperationException()
    {
        var ofd = BuildSampleOfdBytes();
        var truncated = ofd.Take(ofd.Length / 2).ToArray();
        var attFile = Out("robust_att.bin");
        File.WriteAllBytes(attFile, new byte[] { 1, 2, 3 });
        Assert.Throws<InvalidOperationException>(
            () => OfdAttacher.AddAttachment(truncated, new Attachment("附件", attFile)));
    }

    [Fact]
    public void ZipWithoutOfdXml_ShouldReportMissingOfdStructure()
    {
        // 合法 zip 但缺少 OFD.xml
        var path = Out("robust_no_ofdxml.zip");
        using (var za = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
        {
            var e = za.CreateEntry("Doc_0/hello.txt");
            using var s = e.Open();
            var b = Encoding.UTF8.GetBytes("hi");
            s.Write(b, 0, b.Length);
        }
        var bytes = File.ReadAllBytes(path);

        var verifyResult = OfdValidator.Verify(bytes);
        Assert.False(verifyResult.AllValid);
        Assert.NotNull(verifyResult.Error);

        var ex = Assert.ThrowsAny<Exception>(
            () => OfdAnnotator.AddAnnotation(bytes, 1,
                new Annotation(1, 1, 10, 10,
                    AnnotType.Path, new NoOpDrawer())));
        Assert.NotEmpty(ex.Message);
    }

    [Fact]
    public void CorruptOfdXml_ShouldThrowRatherThanSilentCorruption()
    {
        var path = Out("robust_badxml.ofd");
        using (var za = new ZipArchive(File.Create(path), ZipArchiveMode.Create))
        {
            var e = za.CreateEntry("OFD.xml");
            using var s = e.Open();
            var b = Encoding.UTF8.GetBytes("<OFD><DocBody>截断的非法 XML...");
            s.Write(b, 0, b.Length);
        }
        var bytes = File.ReadAllBytes(path);

        Assert.ThrowsAny<Exception>(() => OfdValidator.Verify(bytes));
        Assert.ThrowsAny<Exception>(() => OfdEditor.AppendPage(bytes));
    }

    [Fact]
    public void Verify_GarbageInput_ShouldReturnErrorResultNotThrow()
    {
        var garbage = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var result = OfdValidator.Verify(garbage);
        Assert.False(result.AllValid);
        Assert.NotNull(result.Error);
        Assert.Equal(0, result.SignatureCount);
    }

    [Fact]
    public void Verify_MissingFile_ShouldReturnErrorResultNotThrow()
    {
        var result = OfdValidator.Verify(Path.Combine(OutDir(), "不存在的文件.ofd"));
        Assert.False(result.AllValid);
        Assert.NotNull(result.Error);
    }
}
