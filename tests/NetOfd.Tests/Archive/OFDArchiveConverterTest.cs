using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Archive;
using NetOfd.Archive.Convert;
using NetOfd.Archive.Convert.Handler;
using Xunit;

namespace NetOfd.Tests.Archive;

/// <summary>
/// OFD-A 去技术化转换器测试。
/// <para>核心用例为「往返验证」：构造一份满身违规的 OFD → 转换 → 用合规检查器复检，违规应全部消失。
/// 其余用例针对单个处理器的边界行为。</para>
/// </summary>
public class OFDArchiveConverterTest
{
    private const string Ns = "http://www.ofdspec.org/2016";

    /// <summary>1×1 PNG（仅需魔数正确即可通过格式检测）。</summary>
    private static readonly byte[] Png1X1 =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48, 0x44, 0x52,
        0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00, 0x00, 0x1F, 0x15, 0xC4,
        0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78, 0x9C, 0x63, 0x00, 0x01, 0x00, 0x00,
        0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00, 0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE,
        0x42, 0x60, 0x82
    };

    /// <summary>1×1 GIF89a（非许可格式，用于验证转码为 PNG）。</summary>
    private static readonly byte[] Gif1X1 =
    {
        0x47, 0x49, 0x46, 0x38, 0x39, 0x61, 0x01, 0x00, 0x01, 0x00, 0x80, 0x00, 0x00, 0x00, 0x00, 0x00,
        0xFF, 0xFF, 0xFF, 0x21, 0xF9, 0x04, 0x01, 0x00, 0x00, 0x00, 0x00, 0x2C, 0x00, 0x00, 0x00, 0x00,
        0x01, 0x00, 0x01, 0x00, 0x00, 0x02, 0x02, 0x44, 0x01, 0x00, 0x3B
    };

    // ---- 构造「问题文档」 ----

    /// <summary>构造一份集中体现各类违规的 OFD（XML 部分）。</summary>
    private static Dictionary<string, string> DirtyXml()
    {
        var files = new Dictionary<string, string>
        {
            ["OFD.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<OFD xmlns=""{Ns}"" DocType=""OFD"" Version=""1.1"">
  <DocBody>
    <DocInfo><DocID>Doc0</DocID></DocInfo>
    <DocRoot>Doc_0/Document.xml</DocRoot>
    <Signatures>Doc_0/Signs/Signatures.xml</Signatures>
  </DocBody>
  <DocBody>
    <DocInfo><DocID>Doc1</DocID></DocInfo>
    <DocRoot>Doc_1/Document.xml</DocRoot>
  </DocBody>
</OFD>",

            ["Doc_1/Document.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Document xmlns=""{Ns}"">
  <CommonData><PageArea><PhysicalBox>0 0 210 297</PhysicalBox></PageArea></CommonData>
  <Pages/>
</Document>",

            ["Doc_0/Document.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Document xmlns=""{Ns}"">
  <CommonData>
    <PageArea><PhysicalBox>0 0 210 297</PhysicalBox></PageArea>
    <PublicRes>PublicRes.xml</PublicRes>
    <DocumentRes>DocumentRes.xml</DocumentRes>
  </CommonData>
  <Pages>
    <Page ID=""1"" BaseLoc=""Pages/Page_0/Content.xml""/>
    <Page ID=""2"" BaseLoc=""Pages/Page_1/Content.xml""/>
  </Pages>
  <Outlines>
    <OutlineElem Title=""章一"">
      <Actions><Action Event=""CLICK""><Sound ResourceID=""99""/></Action></Actions>
      <OutlineElem Title=""节一"">
        <Actions><Action Event=""CLICK""><Movie ResourceID=""98""/></Action></Actions>
      </OutlineElem>
    </OutlineElem>
  </Outlines>
  <Permissions><Print Printable=""false""/></Permissions>
  <VPreferences><PageMode>None</PageMode></VPreferences>
  <Extensions><Extension AppName=""SomeApp""/></Extensions>
  <Actions><Action Event=""DO""><Sound ResourceID=""97""/></Action></Actions>
  <Annotations>Annotations.xml</Annotations>
  <Attachments>Attachments.xml</Attachments>
</Document>",

            // MultiMedias 放错位置（应在 DocumentRes）
            ["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}"">
  <Fonts><Font ID=""1"" FontName=""宋体""><FontFile>Res/font.ttf</FontFile></Font></Fonts>
  <MultiMedias>
    <MultiMedia ID=""5"" Type=""Image"" Format=""PNG""><MediaFile>Res/shared.png</MediaFile></MultiMedia>
  </MultiMedias>
</Res>",

            // 音视频资源 + 外链资源 + 带自定义扩展的图像
            ["Doc_0/DocumentRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}"">
  <MultiMedias>
    <MultiMedia ID=""8"" Type=""Audio"" Format=""MP3""><MediaFile>Res/bgm.mp3</MediaFile></MultiMedia>
    <MultiMedia ID=""9"" Type=""Image"" Format=""PNG""><MediaFile>http://evil.example.com/x.png</MediaFile></MultiMedia>
    <MultiMedia ID=""10"" Type=""Image"" Format=""PNG"" xmlns:ext=""urn:vendor:ext"">
      <MediaFile>Res/ext.png</MediaFile>
      <ext:Meta>vendor-only</ext:Meta>
    </MultiMedia>
  </MultiMedias>
</Res>",

            // ColorSpaces 放错位置（应在 PublicRes）+ 多页共用图像却登记在 PageRes
            ["Doc_0/PageRes_0.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}"">
  <ColorSpaces><ColorSpace ID=""7"" Type=""RGB"" BitsPerComponent=""8""/></ColorSpaces>
  <MultiMedias>
    <MultiMedia ID=""6"" Type=""Image"" Format=""PNG""><MediaFile>Res/shared2.png</MediaFile></MultiMedia>
  </MultiMedias>
</Res>",

            // 5 层 PageBlock 嵌套 / 零面积裁剪区 / Interpolate=true / 冗余勾边填充属性 / 重名图层
            ["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}"">
  <Content>
    <Layer ID=""10"" Name=""正文层"">
      <PageBlock ID=""100""><PageBlock ID=""101""><PageBlock ID=""102""><PageBlock ID=""103""><PageBlock ID=""104"">
        <TextObject ID=""105"" Size=""10"" Stroke=""false"" LineWidth=""0.5"" StrokeColor=""1"">
          <TextCode>深层文字</TextCode>
        </TextObject>
      </PageBlock></PageBlock></PageBlock></PageBlock></PageBlock>
      <ImageObject ID=""110"" ResourceID=""6"" Interpolate=""true"" Fill=""true"" FillColor=""0""/>
      <ImageObject ID=""111"" ResourceID=""5"" Interpolate=""true""/>
      <PathObject ID=""120"" Clip=""130"" Stroke=""false"" LineWidth=""1"" DashOffset=""2""/>
      <ClipArea ID=""130"" Boundary=""0 0 0 0""/>
    </Layer>
    <Layer ID=""11"" Name=""正文层""/>
  </Content>
</Page>",

            ["Doc_0/Pages/Page_1/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}"">
  <Content>
    <Layer ID=""20"" Name=""正文层"">
      <ImageObject ID=""210"" ResourceID=""6""/>
      <ImageObject ID=""211"" ResourceID=""5""/>
    </Layer>
  </Content>
</Page>",

            ["Doc_0/Annotations.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Annotations xmlns=""{Ns}"">
  <Page PageID=""1"">
    <Annot ID=""300"" Type=""Stamp"">
      <Appearance Boundary=""0 0 10 10"">
        <PageBlock ID=""301""><PageBlock ID=""302"">
          <TextObject ID=""303"" Size=""8""><TextCode>批注</TextCode></TextObject>
        </PageBlock></PageBlock>
      </Appearance>
    </Annot>
  </Page>
</Annotations>",

            ["Doc_0/Attachments.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Attachments xmlns=""{Ns}"">
  <Attachment ID=""400"" Format=""ZIP"" FileLoc=""Attachs/data.zip""><AttachmentName>数据包</AttachmentName></Attachment>
  <Attachment ID=""401"" Format=""TXT"" FileLoc=""Attachs/readme.txt""><AttachmentName>说明</AttachmentName></Attachment>
</Attachments>",

            ["Doc_0/Signs/Signatures.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Signatures xmlns=""{Ns}"" MaxSignId=""1"">
  <Signature ID=""1"" Type=""Seal"" BaseLoc=""Sign_0/Signature.xml""/>
</Signatures>",

            ["Doc_0/Signs/Sign_0/Signature.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Signature xmlns=""{Ns}""><SignedInfo><Provider ProviderName=""X""/></SignedInfo></Signature>",
        };
        return files;
    }

    private static Dictionary<string, byte[]> DirtyBinaries() => new()
    {
        ["Doc_0/Res/shared.png"] = Png1X1,
        ["Doc_0/Res/shared2.png"] = Png1X1,
        ["Doc_0/Res/ext.png"] = Png1X1,
        ["Doc_0/Res/bgm.mp3"] = new byte[] { 0x49, 0x44, 0x33, 0x04 },
        ["Doc_0/Res/font.ttf"] = new byte[] { 0x00, 0x01, 0x00, 0x00 },
        ["Doc_0/Attachs/data.zip"] = new byte[] { 0x50, 0x4B, 0x03, 0x04 },
        ["Doc_0/Attachs/readme.txt"] = Encoding.UTF8.GetBytes("hello"),
        ["Doc_0/Signs/Sign_0/SignedValue.dat"] = new byte[] { 0x30, 0x82, 0x01 },
        // 非法夹带：不被任何 ST_Loc 引用
        ["Doc_0/stowaway.bin"] = new byte[] { 1, 2, 3, 4 },
        ["extra/hidden.txt"] = Encoding.UTF8.GetBytes("stowaway"),
    };

    // ---- 打包 / 解包工具 ----

    private static string WriteOfd(Dictionary<string, string> xmlFiles, Dictionary<string, byte[]>? binaries = null)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ofd");
        using var fs = File.Create(path);
        using var zip = new ZipArchive(fs, ZipArchiveMode.Create);
        foreach (var kv in xmlFiles)
        {
            var e = zip.CreateEntry(kv.Key);
            var bytes = Encoding.UTF8.GetBytes(kv.Value);
            using var s = e.Open();
            s.Write(bytes, 0, bytes.Length);
        }
        if (binaries != null)
        {
            foreach (var kv in binaries)
            {
                var e = zip.CreateEntry(kv.Key);
                using var s = e.Open();
                s.Write(kv.Value, 0, kv.Value.Length);
            }
        }
        return path;
    }

    private static byte[] ConvertBytes(Dictionary<string, string> xml, Dictionary<string, byte[]>? bin = null)
    {
        var src = WriteOfd(xml, bin);
        try { return new OFDArchiveConverter().Convert(File.ReadAllBytes(src)); }
        finally { File.Delete(src); }
    }

    private static List<string> EntryNames(byte[] ofd)
    {
        using var ms = new MemoryStream(ofd);
        using var zip = new ZipArchive(ms, ZipArchiveMode.Read);
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    private static XElement XmlOf(byte[] ofd, string entry)
    {
        using var ctx = new ArchiveContext(ofd);
        var root = ctx.Xml(entry);
        Assert.NotNull(root);
        return root!;
    }

    private static IReadOnlyList<ArchiveViolation> CheckBytes(byte[] ofd)
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".ofd");
        File.WriteAllBytes(path, ofd);
        try { return new OFDArchiveChecker().Check(path).ToList(); }
        finally { File.Delete(path); }
    }

    // ---- 往返验证：转换后应无 Error / Warn ----

    [Fact]
    public void RoundTrip_DirtyDocument_AllViolationsFixed()
    {
        var src = WriteOfd(DirtyXml(), DirtyBinaries());
        IReadOnlyList<ArchiveViolation> before;
        try { before = new OFDArchiveChecker().Check(src).ToList(); }
        finally { File.Delete(src); }

        // 前置断言：问题文档确实触发了大量违规
        Assert.True(before.Count(v => v.Level != ArchiveViolation.Severity.Info) >= 10,
            "问题文档应触发足够多的违规，实际: " + before.Count);

        var converted = ConvertBytes(DirtyXml(), DirtyBinaries());
        var after = CheckBytes(converted);

        var remaining = after.Where(v => v.Level != ArchiveViolation.Severity.Info).ToList();
        Assert.True(remaining.Count == 0,
            "转换后仍有违规:\n" + string.Join("\n", remaining.Select(v => v.Level + " " + v.RuleName + " " + v.Description)));
    }

    // ---- 逐项处理器验证（复用同一份问题文档） ----

    [Fact]
    public void Convert_StructureAndMetadata_Cleaned()
    {
        var ofd = ConvertBytes(DirtyXml(), DirtyBinaries());

        var ofdRoot = XmlOf(ofd, "/OFD.xml");
        Assert.Equal("OFD-A", ofdRoot.AttrTrim("DocType"));
        Assert.Single(ofdRoot.LocalElems("DocBody"));                 // 单文档化
        Assert.Empty(ofdRoot.LocalDescendants("Signatures"));          // 签名引用摘除
        Assert.DoesNotContain(EntryNames(ofd), n => n.StartsWith("Doc_1/"));

        var doc = XmlOf(ofd, "/Doc_0/Document.xml");
        Assert.Null(doc.LocalElem("Permissions"));
        Assert.Null(doc.LocalElem("VPreferences"));
        Assert.Null(doc.LocalElem("Extensions"));
        Assert.Null(doc.LocalElem("Actions"));
        // 大纲中的非 Goto 动作（含二级大纲）全部清除
        Assert.Empty(doc.LocalDescendants("Actions"));
    }

    [Fact]
    public void Convert_Resources_RelocatedAndPurged()
    {
        var ofd = ConvertBytes(DirtyXml(), DirtyBinaries());

        var pub = XmlOf(ofd, "/Doc_0/PublicRes.xml");
        Assert.Null(pub.LocalElem("MultiMedias"));                      // 迁出 PublicRes
        Assert.NotNull(pub.LocalElem("ColorSpaces"));                   // 从 PageRes 迁入

        var docRes = XmlOf(ofd, "/Doc_0/DocumentRes.xml");
        var ids = docRes.LocalDescendants("MultiMedia").Select(m => m.AttrTrim("ID")).ToList();
        Assert.Contains("5", ids);                                      // PublicRes → DocumentRes
        Assert.Contains("6", ids);                                      // PageRes 共用图像 → DocumentRes
        Assert.DoesNotContain("8", ids);                                // 音频资源删除
        Assert.DoesNotContain("9", ids);                                // 外链资源删除
        Assert.DoesNotContain(EntryNames(ofd), n => n.EndsWith("bgm.mp3"));

        // 图像资源上的厂商扩展被剔除
        var ext = docRes.LocalDescendants("MultiMedia").First(m => m.AttrTrim("ID") == "10");
        Assert.DoesNotContain(ext.Elements(), e => e.Name.Namespace.NamespaceName == "urn:vendor:ext");
    }

    [Fact]
    public void Convert_PageContent_Normalized()
    {
        var ofd = ConvertBytes(DirtyXml(), DirtyBinaries());
        var page = XmlOf(ofd, "/Doc_0/Pages/Page_0/Content.xml");

        // PageBlock 嵌套压到 3 层以内
        Assert.Equal(3, MaxPageBlockDepth(page));

        // 零面积裁剪区删除，引用它的图元置 Visible=false
        Assert.Empty(page.LocalDescendants("ClipArea"));
        var path120 = page.LocalDescendants("PathObject").First(e => e.AttrTrim("ID") == "120");
        Assert.Equal("false", path120.AttrTrim("Visible"));

        // 勾边/填充冗余属性清理
        Assert.Null(path120.AttrTrim("LineWidth"));
        Assert.Null(path120.AttrTrim("DashOffset"));
        var text105 = page.LocalDescendants("TextObject").First(e => e.AttrTrim("ID") == "105");
        Assert.Null(text105.AttrTrim("StrokeColor"));

        // 图像对象：Interpolate=false 且无 Fill/FillColor
        foreach (var io in page.LocalDescendants("ImageObject"))
        {
            Assert.Equal("false", io.AttrTrim("Interpolate"));
            Assert.Null(io.AttrTrim("Fill"));
            Assert.Null(io.AttrTrim("FillColor"));
        }

        // 同页图层名唯一
        var names = page.LocalDescendants("Layer").Select(l => l.AttrTrim("Name")).ToList();
        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.Contains("正文层_2", names);
    }

    [Fact]
    public void Convert_AnnotationSignatureAttachment_Processed()
    {
        var ofd = ConvertBytes(DirtyXml(), DirtyBinaries());

        var ann = XmlOf(ofd, "/Doc_0/Annotations.xml");
        var annot = ann.LocalDescendants("Annot").Single();
        Assert.Equal("true", annot.AttrTrim("ReadOnly"));
        Assert.Equal("true", annot.AttrTrim("NoZoom"));
        Assert.Equal("true", annot.AttrTrim("NoRotate"));
        Assert.Empty(annot.LocalDescendants("PageBlock"));                 // 外观展平
        Assert.Single(annot.LocalElem("Appearance")!.LocalElems("TextObject")); // 图元保留

        // 签名技术数据整体剥离
        Assert.DoesNotContain(EntryNames(ofd), n => n.StartsWith("Doc_0/Signs"));

        // 非文本附件移出，文本附件保留
        var att = XmlOf(ofd, "/Doc_0/Attachments.xml");
        Assert.Single(att.LocalDescendants("Attachment"));
        Assert.Equal("TXT", att.LocalDescendants("Attachment").Single().AttrTrim("Format"));
        Assert.DoesNotContain(EntryNames(ofd), n => n.EndsWith("data.zip"));
        Assert.Contains(EntryNames(ofd), n => n.EndsWith("readme.txt"));
    }

    [Fact]
    public void Convert_Stowaways_Removed()
    {
        var ofd = ConvertBytes(DirtyXml(), DirtyBinaries());
        var names = EntryNames(ofd);

        Assert.DoesNotContain(names, n => n.EndsWith("stowaway.bin"));
        Assert.DoesNotContain(names, n => n.StartsWith("extra/"));
        // 正常内容必须完整保留
        Assert.Contains("OFD.xml", names);
        Assert.Contains("Doc_0/Document.xml", names);
        Assert.Contains("Doc_0/Pages/Page_0/Content.xml", names);
        Assert.Contains("Doc_0/Pages/Page_1/Content.xml", names);
        Assert.Contains(names, n => n.EndsWith("shared.png"));
    }

    /// <summary>OFD.xml 必须是包内首个条目（GB/T 33190-2016 7.1）。</summary>
    [Fact]
    public void Convert_OfdXml_IsFirstEntry()
    {
        var ofd = ConvertBytes(DirtyXml(), DirtyBinaries());
        Assert.Equal("OFD.xml", EntryNames(ofd)[0]);
    }

    // ---- 单处理器边界用例 ----

    /// <summary>非许可图像格式应转码为 PNG 并同步更新 MediaFile / Format。</summary>
    [Fact]
    public void ImageConvert_NonAllowedFormat_TranscodedToPng()
    {
        var xml = DirtyXml();
        xml["Doc_0/DocumentRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}"">
  <MultiMedias>
    <MultiMedia ID=""5"" Type=""Image"" Format=""GIF""><MediaFile>Res/anim.gif</MediaFile></MultiMedia>
  </MultiMedias>
</Res>";
        xml["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><Fonts><Font ID=""1"" FontName=""宋体""><FontFile>Res/font.ttf</FontFile></Font></Fonts></Res>";

        var bin = DirtyBinaries();
        bin["Doc_0/Res/anim.gif"] = Gif1X1;

        var ofd = ConvertBytes(xml, bin);
        var docRes = XmlOf(ofd, "/Doc_0/DocumentRes.xml");
        var mm = docRes.LocalDescendants("MultiMedia").First(m => m.AttrTrim("ID") == "5");

        Assert.Equal("PNG", mm.AttrTrim("Format"));
        Assert.EndsWith(".png", mm.LocValue("MediaFile"));
        Assert.DoesNotContain(EntryNames(ofd), n => n.EndsWith("anim.gif"));
        Assert.Contains(EntryNames(ofd), n => n.EndsWith("anim.png"));
    }

    /// <summary>Goto 动作必须保留，其余动作删除。</summary>
    [Fact]
    public void NonGotoAction_GotoPreserved()
    {
        var xml = DirtyXml();
        xml["Doc_0/Pages/Page_1/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}"">
  <Content>
    <Layer ID=""20"" Name=""正文层"">
      <ImageObject ID=""210"" ResourceID=""6""/>
      <ImageObject ID=""211"" ResourceID=""5""/>
      <PathObject ID=""220"">
        <Actions>
          <Action Event=""CLICK""><Goto><Dest Type=""XYZ"" PageID=""1""/></Goto></Action>
          <Action Event=""CLICK""><Sound ResourceID=""96""/></Action>
        </Actions>
      </PathObject>
    </Layer>
  </Content>
</Page>";
        var ofd = ConvertBytes(xml, DirtyBinaries());
        var page = XmlOf(ofd, "/Doc_0/Pages/Page_1/Content.xml");
        var actions = page.LocalDescendants("Actions").Single();
        var action = actions.LocalElems("Action").Single();
        Assert.NotNull(action.LocalElem("Goto"));
    }

    /// <summary>未配置解密器时遇到加密文档应中断，而不是产出半成品。</summary>
    [Fact]
    public void Encryption_WithoutDecryptor_Throws()
    {
        var xml = DirtyXml();
        xml["Encryptions.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Encryptions xmlns=""{Ns}""><Encryption ID=""1""/></Encryptions>";

        var ex = Assert.Throws<InvalidOperationException>(() => ConvertBytes(xml, DirtyBinaries()));
        Assert.Contains("解密器", ex.Message);
    }

    /// <summary>自定义管道：只跑指定处理器，其余违规原样保留。</summary>
    [Fact]
    public void CustomPipeline_RunsOnlyGivenHandlers()
    {
        var src = WriteOfd(DirtyXml(), DirtyBinaries());
        byte[] ofd;
        try
        {
            var converter = new OFDArchiveConverter(new ArchiveHandler[] { new DocTypeHandler() });
            using var ws = new ArchiveWorkspace(File.ReadAllBytes(src));
            foreach (var h in converter.Handlers) h.Handle(ws);
            ofd = ws.ToBytes();
        }
        finally { File.Delete(src); }

        Assert.Equal("OFD-A", XmlOf(ofd, "/OFD.xml").AttrTrim("DocType"));
        // 未执行 PermissionHandler，权限声明仍在
        Assert.NotNull(XmlOf(ofd, "/Doc_0/Document.xml").LocalElem("Permissions"));
    }

    /// <summary>自定义管道中无 EncryptionHandler 时，AddDecryptor 应明确报错。</summary>
    [Fact]
    public void CustomPipeline_AddDecryptor_Throws()
    {
        var converter = new OFDArchiveConverter(new ArchiveHandler[] { new DocTypeHandler() });
        Assert.Throws<InvalidOperationException>(() => converter.AddDecryptor(null!));
    }

    private static int MaxPageBlockDepth(XElement page)
    {
        var max = 0;
        foreach (var pb in page.TopLevelLocal("PageBlock")) Walk(pb, 1);
        return max;

        void Walk(XElement e, int d)
        {
            if (d > max) max = d;
            foreach (var c in e.LocalElems("PageBlock")) Walk(c, d + 1);
        }
    }
}
