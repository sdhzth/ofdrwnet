using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Pkg.Container;
using NetOfd.Reader;
using NetOfd.Reader.Model;
using NetOfd.Reader.Tools;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// 解压工具 ZipUtil 测试：正常解压 / Zip Slip 路径穿越防护 / 异常结尾容错。
/// 对齐 Java: org.ofdrw.reader.ZipUtil
/// </summary>
public class ZipUtilTest : IDisposable
{
    private readonly string _root;

    public ZipUtilTest()
    {
        _root = Path.Combine(Path.GetTempPath(), "netofd_zut_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* 忽略清理失败 */ }
        ZipUtil.DefaultEncoding = Encoding.UTF8; // 复位，避免影响其它测试
    }

    private string NewDir(string name)
    {
        var d = Path.Combine(_root, name);
        Directory.CreateDirectory(d);
        return d;
    }

    /// <summary>构造一个含多级目录与文件的 zip，返回其字节。</summary>
    private static byte[] BuildZip(params (string name, string content)[] entries)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var e = za.CreateEntry(name);
                using var w = new StreamWriter(e.Open(), new UTF8Encoding(false));
                w.Write(content);
            }
        }
        return ms.ToArray();
    }

    [Fact]
    public void UnZipFiles_Normal_ExtractsAllEntriesWithContent()
    {
        var zip = BuildZip(
            ("OFD.xml", "<ofd/>"),
            ("Doc_0/Document.xml", "<doc/>"),
            ("Doc_0/Pages/Page_0/Content.xml", "<page/>"));

        var dest = NewDir("normal");
        using var src = new MemoryStream(zip);
        ZipUtil.UnZipFiles(src, dest);

        Assert.True(File.Exists(Path.Combine(dest, "OFD.xml")));
        Assert.Equal("<ofd/>", File.ReadAllText(Path.Combine(dest, "OFD.xml")));
        Assert.Equal("<doc/>", File.ReadAllText(Path.Combine(dest, "Doc_0", "Document.xml")));
        Assert.Equal("<page/>",
            File.ReadAllText(Path.Combine(dest, "Doc_0", "Pages", "Page_0", "Content.xml")));
    }

    [Fact]
    public void UnZipFiles_FromFilePath_WorksToo()
    {
        var zipPath = Path.Combine(_root, "sample.zip");
        File.WriteAllBytes(zipPath, BuildZip(("OFD.xml", "<ofd/>")));

        var dest = NewDir("fromfile");
        ZipUtil.UnZipFiles(zipPath, dest);

        Assert.Equal("<ofd/>", File.ReadAllText(Path.Combine(dest, "OFD.xml")));
    }

    [Fact]
    public void UnZipFiles_ZipSlip_ThrowsIOException()
    {
        // 恶意条目：尝试写到解压根目录之外
        var zip = BuildZip(("../evil.txt", "pwned"), ("OFD.xml", "<ofd/>"));
        var dest = NewDir("slip");
        var parentMarker = Path.Combine(_root, "evil.txt");

        using var src = new MemoryStream(zip);
        var ex = Assert.Throws<IOException>(() => ZipUtil.UnZipFiles(src, dest));
        Assert.Contains("不合法的路径", ex.Message);
        Assert.False(File.Exists(parentMarker), "路径穿越文件不应被写出到解压根目录之外");
    }

    [Fact]
    public void UnZipFiles_TruncatedTail_DoesNotThrowAndKeepsExtracted()
    {
        // 模拟"部分 OFD 压缩包结尾不完整"：在合法 zip 尾部追加垃圾字节
        var good = BuildZip(("OFD.xml", "<ofd/>"), ("Doc_0/Document.xml", "<doc/>"));
        var broken = good.Concat(Encoding.ASCII.GetBytes("GARBAGE-TAIL-1234567890")).ToArray();

        var dest = NewDir("trunc");
        using var src = new MemoryStream(broken);

        // 关键点：不得抛出；已成功解压的内容仍可用
        ZipUtil.UnZipFiles(src, dest);
        Assert.True(File.Exists(Path.Combine(dest, "OFD.xml")));
        Assert.Equal("<ofd/>", File.ReadAllText(Path.Combine(dest, "OFD.xml")));
    }

    [Fact]
    public void UnZipFiles_MissingFile_ThrowsIOException()
    {
        var ex = Assert.Throws<IOException>(() =>
            ZipUtil.UnZipFiles(Path.Combine(_root, "nope.zip"), NewDir("x")));
        Assert.Contains("解压文件不存在", ex.Message);
    }
}

/// <summary>
/// Reader 补齐类型测试：ErrorPathException / KeywordResource / SealDataVo / NameSpaceCleaner。
/// </summary>
public class ReaderModelTest
{
    [Fact]
    public void ErrorPathException_HasStandardCtors()
    {
        var inner = new InvalidOperationException("inner");
        Assert.IsAssignableFrom<Exception>(new ErrorPathException());
        Assert.Equal("m", new ErrorPathException("m").Message);
        Assert.Same(inner, new ErrorPathException("m", inner).InnerException);
        Assert.Same(inner, new ErrorPathException(inner).InnerException);
    }

    [Fact]
    public void KeywordResource_PropertiesRoundTrip()
    {
        var r = new KeywordResource { Page = 3 };
        Assert.Equal(3, r.Page);
        Assert.Null(r.Font);
        Assert.Null(r.Text);

        r.Font = new NetOfd.Core.PageDescription.Font.CT_Font();
        r.Text = new NetOfd.Core.PageDescription.CTText();
        Assert.NotNull(r.Font);
        Assert.NotNull(r.Text);
    }

    [Fact]
    public void SealDataVo_PropertiesRoundTrip()
    {
        var v = new SealDataVo { SealBytes = new byte[] { 1, 2, 3 }, SealType = "png" };
        Assert.Equal(3, v.SealBytes!.Length);
        Assert.Equal("png", v.SealType);
    }

    [Fact]
    public void NameSpaceCleaner_StripsNamespacesFromElementsAndDeclarations()
    {
        XNamespace ofd = "http://www.ofdspec.org/2016";
        var doc = new XDocument(new XElement(ofd + "Page",
            new XAttribute(XNamespace.Xmlns + "ofd", ofd.NamespaceName),
            new XElement(ofd + "Content",
                new XAttribute(ofd + "ID", "7"),
                new XElement(ofd + "Text", "你好"))));

        // 清理前：带命名空间，按 ofd: 前缀能查到
        Assert.NotNull(doc.Root!.Element(ofd + "Content"));

        NameSpaceCleaner.Clean(doc);

        // 清理后：无命名空间，按本地名可查；xmlns 声明与带命名空间的属性均已去除
        Assert.Equal(XNamespace.None, doc.Root!.Name.Namespace);
        var content = doc.Root.Element("Content");
        Assert.NotNull(content);
        Assert.Equal("7", content!.Attribute("ID")!.Value);
        Assert.Equal("你好", content.Element("Text")!.Value);
        Assert.DoesNotContain("ofdspec", doc.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void NameSpaceCleaner_HandlesDocumentWithoutNamespace()
    {
        var doc = new XDocument(new XElement("Page", new XElement("Text", "x")));
        NameSpaceCleaner.Clean(doc); // 不应抛异常
        Assert.Equal("x", doc.Root!.Element("Text")!.Value);
    }
}
