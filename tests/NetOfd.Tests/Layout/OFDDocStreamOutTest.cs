using System;
using System.IO;
using System.Linq;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// OFDDoc 输出目标与包体出口测试（对齐 Java OFDDoc(OutputStream) / getOfdDir / getOfdDocument）。
/// </summary>
public class OFDDocStreamOutTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_streamout_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    [Fact]
    public void CloseToMemoryStream_ShouldProduceReadableOfd()
    {
        using var ms = new MemoryStream();
        var doc = new OFDDoc(ms);
        doc.Add(new Paragraph().AddText("流式输出验证：内存目标").SetFontSize(5d));
        doc.Close();

        // 内存流应收到完整 zip（PK 头）
        var bytes = ms.ToArray();
        Assert.True(bytes.Length > 100, "输出不应为空");
        Assert.Equal((byte)'P', bytes[0]);
        Assert.Equal((byte)'K', bytes[1]);

        // 字节可被 Reader 读回并抽取到中文
        using var reader = new OFDReader(new MemoryStream(bytes));
        Assert.Equal(1, reader.GetPageCount());
        var text = string.Join("", NetOfd.Reader.ContentExtractor.ExtractAllPagesText(reader.GetPages()));
        Assert.Contains("流式输出验证", text);
    }

    [Fact]
    public void FileOutput_ShouldEqualStreamOutput()
    {
        // 同一内容写文件与写流，读回的文本应一致
        var filePath = Out("stream_equiv.ofd");
        var docA = new OFDDoc(filePath);
        docA.Add(new Paragraph().AddText("文件与流输出等价").SetFontSize(5d));
        docA.Close();

        using var ms = new MemoryStream();
        var docB = new OFDDoc(ms);
        docB.Add(new Paragraph().AddText("文件与流输出等价").SetFontSize(5d));
        docB.Close();

        using var r1 = new OFDReader(filePath);
        using var r2 = new OFDReader(new MemoryStream(ms.ToArray()));
        var t1 = string.Join("", NetOfd.Reader.ContentExtractor.ExtractAllPagesText(r1.GetPages()));
        var t2 = string.Join("", NetOfd.Reader.ContentExtractor.ExtractAllPagesText(r2.GetPages()));
        Assert.Equal(t1, t2);
        Assert.Contains("文件与流输出等价", t2);
    }

    [Fact]
    public void GetOfdDir_ShouldExposeContainer()
    {
        var doc = new OFDDoc(Out("getofddir.ofd"));
        try
        {
            var dir = doc.GetOfdDir();
            Assert.NotNull(dir);
            // 构造后包内已有 OFD.xml（DocBody/DocRoot）与 Doc_0 目录
            var ofd = dir.GetOfd();
            Assert.NotNull(ofd);
            var bodies = ofd.GetDocBodies();
            Assert.Single(bodies);
            Assert.Equal("Doc_0/Document.xml", bodies[0].DocRoot?.Value);

            // 加一页保证 Close（Flush 要求至少一页）可正常收尾
            doc.Add(new Paragraph().AddText("出口检查").SetFontSize(5d));
        }
        finally { doc.Close(); }
    }

    [Fact]
    public void GetOfdDocument_ShouldExposeDocumentTree()
    {
        var doc = new OFDDoc(Out("getofddocument.ofd"));
        try
        {
            var document = doc.GetOfdDocument();
            Assert.NotNull(document);
            Assert.NotNull(document.CommonData);
            Assert.NotNull(document.Pages);
            // 与包容器 Doc_0 内的 Document 同底层 XElement（容器缓存）
            var fromDir = doc.GetOfdDir().GetDocByIndex(0)?.GetDocument();
            Assert.NotNull(fromDir);
            Assert.Same(document.Element, fromDir.Element);

            doc.Add(new Paragraph().AddText("出口检查").SetFontSize(5d));
        }
        finally { doc.Close(); }
    }

    [Fact]
    public void StreamConstructor_ShouldRejectNullAndReadOnly()
    {
        Assert.Throws<ArgumentNullException>(() => new OFDDoc((Stream)null!));
        using var ro = new MemoryStream(new byte[] { 1, 2, 3 }, writable: false);
        Assert.Throws<ArgumentException>(() => new OFDDoc(ro));
    }
}
