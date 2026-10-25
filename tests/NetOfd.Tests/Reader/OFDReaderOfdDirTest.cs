using NetOfd.Pkg.Container;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Reader;

/// <summary>
/// OFDReader.GetOfdDir() 测试——对齐 Java <c>OFDReader.getOFDDir()</c>。
/// <para>Java 锚点：ofdrw-reader OFDReader.getOFDDir() 直接 <c>return ofdDir;</c>
/// （L173-175），无额外逻辑。NetOfd 侧等价：返回构造时解出的 <see cref="OFDDir"/> 容器，
/// 使调用方可通过 <c>GetDocByIndex(int)</c> / <c>ObtainDocDir</c> / <c>ObtainOfd</c>
/// 直接做包级操作。</para>
/// <para>样本：SESV4SignDoc.ofd / helloworld.ofd（DocRoot 均为 Doc_0/Document.xml）。</para>
/// </summary>
public class OFDReaderOfdDirTest
{
    private const string Res = "E:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources";

    private static bool Has(string name) => File.Exists(Path.Combine(Res, name));

    private static OFDReader Open(string name) => new(Path.Combine(Res, name));

    [Fact]
    public void GetOfdDir_ShouldReturnNonNullContainer()
    {
        if (!Has("helloworld.ofd")) { Assert.True(true, "Java 测试资源不存在，跳过。"); return; }
        using var reader = Open("helloworld.ofd");

        OFDDir ofdDir = reader.GetOfdDir();

        Assert.NotNull(ofdDir);
        // 容器可见 OFD.xml 根元素；且与 Reader.GetOfd 共享同一底层 XElement
        // （VirtualContainer.FileCache 容器级缓存命中，OFD 包装对象每次 new 但 Element 同一实例）
        var viaContainer = ofdDir.GetOfd();
        Assert.NotNull(viaContainer);
        Assert.Same(viaContainer!.Element, reader.GetOfd().Element);
    }

    [Fact]
    public void GetOfdDir_GetDocByIndex0_ShouldMatchCurrentDocDir()
    {
        if (!Has("SESV4SignDoc.ofd")) { Assert.True(true, "Java 测试资源不存在，跳过。"); return; }
        using var reader = Open("SESV4SignDoc.ofd");

        OFDDir ofdDir = reader.GetOfdDir();
        DocDir byIndex = ofdDir.GetDocByIndex(0);

        // Reader 以 DocRoot（Doc_0）初始化：GetDocByIndex(0) 与当前文档目录
        // 命中同一缓存实例（OFDDir 以 "Doc_{index}" 为键、ObtainDocDir 同键缓存）
        Assert.Same(byIndex, reader.GetDocDir());

        // 且能取出同一份 Document.xml（OFDElement 包装每次 new，
        // 但底层 XElement 经 VirtualContainer.FileCache 命中同一实例）
        Assert.Same(byIndex.GetDocument()!.Element, reader.GetDocument().Element);
    }
}
