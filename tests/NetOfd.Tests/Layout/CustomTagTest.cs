using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.CustomTags;
using NetOfd.Layout.Edit;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// 自定义标引（CustomTags）功能测试。
/// 对应 Java: ofdrw-core customTags 包（GB/T 33190-2016 第 16 章）
/// </summary>
public class CustomTagTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_tag_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    /// <summary>构建单页样例文档并返回字节。</summary>
    private static byte[] BuildSampleOfdBytes(string name)
    {
        var path = Out(name);
        using (var doc = new NetOfd.Layout.OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new NetOfd.Layout.Element.Paragraph()
                .AddText("自定义标引测试样本").SetFontSize(5d));
        }
        return File.ReadAllBytes(path);
    }

    /// <summary>构造标引内容 XML 字节（引用页面图元的“非接触式”关联示例）。</summary>
    private static byte[] MakeTagContent(string keyword) => Encoding.UTF8.GetBytes(
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <ofd:TagInfo xmlns:ofd="http://www.ofdspec.org/2016">
          <ofd:Keyword>{keyword}</ofd:Keyword>
        </ofd:TagInfo>
        """);

    private static byte[]? ReadEntry(byte[] ofd, string entryName)
    {
        using var ms = new MemoryStream(ofd);
        using var za = new ZipArchive(ms, ZipArchiveMode.Read);
        var e = za.GetEntry(entryName);
        if (e == null) return null;
        using var s = e.Open();
        using var buf = new MemoryStream();
        s.CopyTo(buf);
        return buf.ToArray();
    }

    private static XDocument EntryXml(byte[] ofd, string entryName)
        => XDocument.Parse(Encoding.UTF8.GetString(ReadEntry(ofd, entryName)!));

    // ──────────────────────────────────────────────────────────────

    /// <summary>添加标引应创建索引文件与标引文件并登记文档入口。</summary>
    [Fact]
    public void AddCustomTag_ShouldCreateStructureAndEntry()
    {
        var ofd = BuildSampleOfdBytes("Tag_Basic.ofd");

        var tagged = OfdTagger.AddCustomTag(ofd, "urn:example:keyword",
            MakeTagContent("诊断"), nameSpace: "http://example.com/schema/tag");

        // 标引文件与索引文件存在
        Assert.NotNull(ReadEntry(tagged, "Doc_0/Tags/Custom_1.xml"));
        Assert.NotNull(ReadEntry(tagged, "Doc_0/CustomTags.xml"));

        // Document.xml 登记标引列表入口
        var docXml = EntryXml(tagged, "Doc_0/Document.xml");
        var tagsLoc = docXml.Descendants().FirstOrDefault(e => e.Name.LocalName == "CustomTags")?.Value;
        Assert.Equal("/Doc_0/CustomTags.xml", tagsLoc);

        // 索引条目属性校验
        var tags = new CustomTags(EntryXml(tagged, "Doc_0/CustomTags.xml").Root!);
        var tag = Assert.Single(tags.GetCustomTags());
        Assert.Equal("urn:example:keyword", tag.TypeID);
        Assert.Equal("http://example.com/schema/tag", tag.NameSpace);
        Assert.Null(tag.SchemaLoc);
        Assert.Equal("/Doc_0/Tags/Custom_1.xml", tag.FileLoc.Value);

        // 标引文件内容一致
        Assert.Equal(MakeTagContent("诊断"), ReadEntry(tagged, "Doc_0/Tags/Custom_1.xml"));
    }

    /// <summary>多次添加标引应追加到同一索引并递增文件序号。</summary>
    [Fact]
    public void AddCustomTag_Twice_ShouldAppendToIndex()
    {
        var ofd = BuildSampleOfdBytes("Tag_Twice.ofd");
        var once = OfdTagger.AddCustomTag(ofd, "t1", MakeTagContent("甲"));
        var twice = OfdTagger.AddCustomTag(once, "t2", MakeTagContent("乙"),
            schemaLoc: "Schemas/tag.xsd");

        var tags = new CustomTags(EntryXml(twice, "Doc_0/CustomTags.xml").Root!);
        var list = tags.GetCustomTags();
        Assert.Equal(2, list.Count);
        Assert.Equal("t1", list[0].TypeID);
        Assert.Equal("t2", list[1].TypeID);
        Assert.Null(list[0].SchemaLoc);
        Assert.Equal("Schemas/tag.xsd", list[1].SchemaLoc!.Value);

        Assert.NotNull(ReadEntry(twice, "Doc_0/Tags/Custom_1.xml"));
        Assert.NotNull(ReadEntry(twice, "Doc_0/Tags/Custom_2.xml"));
    }

    /// <summary>从文件路径添加标引。</summary>
    [Fact]
    public void AddCustomTag_FromFilePath()
    {
        var ofd = BuildSampleOfdBytes("Tag_File.ofd");
        var tagFile = Out("tag_source.xml");
        File.WriteAllBytes(tagFile, MakeTagContent("处方"));

        var inputPath = Out("Tag_File_in.ofd");
        File.WriteAllBytes(inputPath, ofd);
        var outputPath = Out("Tag_File_out.ofd");
        OfdTagger.AddCustomTag(inputPath, outputPath, "urn:example:rx", tagFile);

        var outBytes = File.ReadAllBytes(outputPath);
        var tags = new CustomTags(EntryXml(outBytes, "Doc_0/CustomTags.xml").Root!);
        var tag = Assert.Single(tags.GetCustomTags());
        Assert.Equal("urn:example:rx", tag.TypeID);
        Assert.Equal(MakeTagContent("处方"), ReadEntry(outBytes, tag.FileLoc.Value.TrimStart('/')));
    }

    /// <summary>读取已有 OFD 中的全部自定义标引及其内容。</summary>
    [Fact]
    public void GetCustomTags_ShouldReturnEntriesWithContent()
    {
        var ofd = BuildSampleOfdBytes("Tag_Read.ofd");
        var withA = OfdTagger.AddCustomTag(ofd, "k1", MakeTagContent("检查"));
        var withB = OfdTagger.AddCustomTag(withA, "k2", MakeTagContent("检验"));

        var readBack = OfdTagger.GetCustomTags(withB);
        Assert.Equal(2, readBack.Count);
        Assert.Equal("k1", readBack[0].Tag.TypeID);
        Assert.Equal(MakeTagContent("检查"), readBack[0].Content);
        Assert.Equal("k2", readBack[1].Tag.TypeID);
        Assert.Equal(MakeTagContent("检验"), readBack[1].Content);

        // 无标引文档：返回空集合且不抛异常
        Assert.Empty(OfdTagger.GetCustomTags(BuildSampleOfdBytes("Tag_Empty.ofd")));
    }

    /// <summary>标引模型属性序列化往返。</summary>
    [Fact]
    public void CustomTag_ModelRoundtrip()
    {
        var model = new CustomTags(new XElement(NetOfd.Core.OFDElement.OfdNs + "CustomTags"))
            .AddCustomTag(new CustomTag
            {
                TypeID = "urn:example:type",
                NameSpace = "http://example.com/ns",
                SchemaLoc = new NetOfd.Core.BasicType.STLoc("Schemas/a.xsd"),
                FileLoc = new NetOfd.Core.BasicType.STLoc("Doc_0/Tags/Custom_1.xml")
            });

        var parsed = new CustomTags(XDocument.Parse(
            Encoding.UTF8.GetString(OfdAnnotator.ToUtf8Bytes(model.Element))).Root!);
        var tag = Assert.Single(parsed.GetCustomTags());
        Assert.Equal("urn:example:type", tag.TypeID);
        Assert.Equal("http://example.com/ns", tag.NameSpace);
        Assert.Equal("Schemas/a.xsd", tag.SchemaLoc!.Value);
        Assert.Equal("Doc_0/Tags/Custom_1.xml", tag.FileLoc.Value);
    }
}
