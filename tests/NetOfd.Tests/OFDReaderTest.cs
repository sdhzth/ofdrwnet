using Xunit;

namespace NetOfd.Tests;

public class OFDReaderTest
{
    [Fact]
    public void Read_SimpleOFD()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        Assert.NotNull(reader);

        var pages = reader.GetPages();
        Assert.NotNull(pages);

        reader.Dispose();
    }

    [Fact]
    public void Read_ChineseDirOFD()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/chineseDir_windows.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        var pages = reader.GetPages();
        Assert.NotNull(pages);

        reader.Dispose();
    }

    [Fact]
    public void Read_TextExtraction()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        var pages = reader.GetPages();
        Assert.NotNull(pages);

        var text = NetOfd.Reader.ContentExtractor.ExtractAllPagesText(pages);
        Assert.NotNull(text);

        reader.Dispose();
    }

    [Fact]
    public void Read_KeywordSearch()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/keyword.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        var finder = new NetOfd.Reader.KeywordExtractor(reader);

        var results = finder.FindKeyword("测试");
        Assert.NotNull(results);

        var results2 = finder.FindKeyword("OFD");
        Assert.NotNull(results2);

        reader.Dispose();
    }

    [Fact]
    public void GetPageInfo_OnHelloWorld()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);

        var info = reader.GetPageInfo(1);
        Assert.NotNull(info);
        Assert.Equal(1, info.Index);
        Assert.Equal(0, info.PageN);
        Assert.Equal(210, info.Size!.Width);
        Assert.Equal(297, info.Size.Height);
        Assert.NotNull(info.Id);
        Assert.NotNull(info.Obj);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", info.PageAbsLoc!.Value);
        Assert.Empty(info.Templates);
    }

    [Fact]
    public void GetPageInfo_OutOfRange_Throws()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        Assert.Throws<ArgumentException>(() => reader.GetPageInfo(0));
        Assert.Throws<ArgumentException>(() => reader.GetPageInfo(99));
    }

    [Fact]
    public void GetPageSize_And_PageArea()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);

        var size = reader.GetPageSize(1);
        Assert.Equal(210, size.Width);
        Assert.Equal(297, size.Height);

        var area = reader.GetPageArea(1);
        Assert.NotNull(area);
        Assert.Equal("0 0 210 297", area.PhysicalBox!.ToString());
    }

    [Fact]
    public void GetPageAbsLoc_And_ObjectId()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);

        var absLoc = reader.GetPageAbsLoc(1);
        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", absLoc.Value);

        var id = reader.GetPageObjectId(1);
        Assert.NotNull(id);
        Assert.Equal(1, id.Value);
    }

    [Fact]
    public void GetPageList_MatchesPageCount()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);

        var list = reader.GetPageList();
        Assert.Equal(reader.GetPageCount(), list.Count);
        Assert.Equal(1, list[0].Index);
    }

    [Fact]
    public void HasSignature()
    {
        var signedPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/SESV4SignDoc.ofd";
        var plainPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";

        if (File.Exists(signedPath))
        {
            using var signed = new NetOfd.Reader.OFDReader(signedPath);
            Assert.True(signed.HasSignature());
        }
        else
        {
            Assert.True(true, "Signed OFD file not found, skip test.");
        }

        if (File.Exists(plainPath))
        {
            using var plain = new NetOfd.Reader.OFDReader(plainPath);
            Assert.False(plain.HasSignature());
        }
        else
        {
            Assert.True(true, "Plain OFD file not found, skip test.");
        }
    }

    [Fact]
    public void GetTemplate_ReturnsNull_WhenNoTemplate()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/helloworld.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);
        Assert.Null(reader.GetTemplate("999"));
    }

    [Fact]
    public void GetAttachments_OnAddAttachment()
    {
        var ofdPath = "e:/电子病历/ofdrw-master/ofdrw-reader/src/test/resources/AddAttachment.ofd";
        if (!File.Exists(ofdPath))
        {
            Assert.True(true, "Test OFD file not found, skip test.");
            return;
        }

        using var reader = new NetOfd.Reader.OFDReader(ofdPath);

        var list = reader.GetAttachmentList();
        Assert.Single(list);

        var att = reader.GetAttachment("Gao");
        Assert.NotNull(att);
        Assert.Equal("Gao", att!.Name);
        Assert.Equal("/Doc_0/Res/eg_tulip.jpg", att.FileLoc!.Value);

        var data = reader.GetAttachmentFile("Gao");
        Assert.NotNull(data);
        Assert.True(data!.Length > 0);

        Assert.Null(reader.GetAttachment("NotFound"));
        Assert.Null(reader.GetAttachmentFile("NotFound"));
    }
}
