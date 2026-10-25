using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NetOfd.Archive;
using Xunit;

namespace NetOfd.Tests.Archive;

/// <summary>
/// OFD-A 合规检查器测试。以最小合规 OFD 骨架为基线，针对性注入各类违规并断言对应规则触发。
/// </summary>
public class OFDArchiveCheckerTest
{
    private const string Ns = "http://www.ofdspec.org/2016";

    /// <summary>生成最小合规 OFD 骨架（DocType=OFD-A、单文档、单页、无违规）。</summary>
    private static Dictionary<string, string> Skeleton(string docType = "OFD-A", int docBodies = 1)
    {
        var files = new Dictionary<string, string>();
        var bodies = new StringBuilder();
        for (var i = 0; i < docBodies; i++)
        {
            bodies.Append($@"
    <DocBody>
      <DocInfo><DocID>Doc{i}</DocID></DocInfo>
      <DocRoot>Doc_{i}/Document.xml</DocRoot>
    </DocBody>");
        }
        files["OFD.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<OFD xmlns=""{Ns}"" DocType=""{docType}"" Version=""1.1"">
  {bodies}
</OFD>";

        files["Doc_0/Document.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Document xmlns=""{Ns}"">
  <CommonData>
    <PageArea><PhysicalBox>0 0 210 297</PhysicalBox></PageArea>
    <PublicRes>PublicRes.xml</PublicRes>
    <DocumentRes>DocumentRes.xml</DocumentRes>
  </CommonData>
  <Pages>
    <Page ID=""1"" BaseLoc=""Pages/Page_0/Content.xml""/>
  </Pages>
</Document>";

        files["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><Fonts/></Res>";

        files["Doc_0/DocumentRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""/>";

        files["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock>
  <TextObject ID=""1"" Size=""10""><TextCode>Hello</TextCode></TextObject>
</PageBlock></Page>";

        return files;
    }

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

    private static IReadOnlyList<ArchiveViolation> Check(Dictionary<string, string> files, Dictionary<string, byte[]>? binaries = null)
    {
        var path = WriteOfd(files, binaries);
        try
        {
            return new OFDArchiveChecker().Check(path).ToList();
        }
        finally
        {
            File.Delete(path);
        }
    }

    private static bool Has(IReadOnlyList<ArchiveViolation> vs, string rule, ArchiveViolation.Severity? sev = null)
        => vs.Any(v => v.RuleName == rule && (sev == null || v.Level == sev));

    // ---- 基线：完全合规应为 0 违规 ----

    [Fact]
    public void Compliant_Baseline_NoViolations()
    {
        var vs = Check(Skeleton());
        Assert.Empty(vs);
    }

    // ---- 文件结构规则 ----

    [Fact]
    public void DocType_NotOfdA_Fails()
    {
        var vs = Check(Skeleton(docType: "OFD"));
        Assert.True(Has(vs, "DOC_TYPE", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void DocType_OfdA_Passes()
    {
        var vs = Check(Skeleton(docType: "OFD-A"));
        Assert.False(Has(vs, "DOC_TYPE"));
    }

    [Fact]
    public void SingleDoc_MultipleBodies_Fails()
    {
        var vs = Check(Skeleton(docBodies: 2));
        Assert.True(Has(vs, "SINGLE_DOC", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void ExternalResource_HttpUrl_Fails()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</CommonData>", "<X Href=\"http://example.com/evil.png\"/></CommonData>");
        var vs = Check(f);
        Assert.True(Has(vs, "EXTERNAL_RESOURCE", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void Permission_Present_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Document>", "<Permissions/></Document>");
        var vs = Check(f);
        Assert.True(Has(vs, "PERMISSION", ArchiveViolation.Severity.Warn));
    }

    [Fact]
    public void Extension_Present_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Document>", "<Extensions><Extension/></Extensions></Document>");
        var vs = Check(f);
        Assert.True(Has(vs, "EXTENSION", ArchiveViolation.Severity.Warn));
    }

    [Fact]
    public void NonGotoAction_InDocument_Fails()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Document>", "<Actions><Action ID=\"1\" Type=\"URI\"><URI>javascript:void</URI></Action></Actions></Document>");
        var vs = Check(f);
        Assert.True(Has(vs, "NON_GOTO_ACTION", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void OutlineAction_NonGoto_Fails()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Document>", "<Outlines><OutlineElem><Actions><Action ID=\"1\" Type=\"URI\"><URI>x</URI></Action></Actions></OutlineElem></Outlines></Document>");
        var vs = Check(f);
        Assert.True(Has(vs, "OUTLINE_ACTION", ArchiveViolation.Severity.Error));
    }

    // ---- 资源规则 ----

    [Fact]
    public void AudioVideo_AudioInPublicRes_Fails()
    {
        var f = Skeleton();
        f["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><MultiMedias><MultiMedia ID=""1"" Type=""Audio"" MediaFile=""Res/a.mp3""/></MultiMedias></Res>";
        var vs = Check(f);
        Assert.True(Has(vs, "AUDIO_VIDEO", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void ImageFormat_BadMagic_Warns()
    {
        var f = Skeleton();
        f["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><MultiMedias><MultiMedia ID=""1"" Type=""Image"" MediaFile=""Res/bad.bin""/></MultiMedias></Res>";
        var binaries = new Dictionary<string, byte[]> { ["Doc_0/Res/bad.bin"] = new byte[] { 0x00, 0x01, 0x02, 0x03 } };
        var vs = Check(f, binaries);
        Assert.True(Has(vs, "IMAGE_FORMAT")); // WARN（无法识别）或 ERROR（格式不在允许范围）
    }

    [Fact]
    public void FontSubset_MissingFontFile_Fails()
    {
        var f = Skeleton();
        f["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><Fonts><Font ID=""1"" FontName=""SimSun""/></Fonts></Res>";
        var vs = Check(f);
        Assert.True(Has(vs, "FONT_SUBSET", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void ColorSpace_NoType_Warns()
    {
        var f = Skeleton();
        f["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><ColorSpaces><ColorSpace ID=""1""/></ColorSpaces></Res>";
        var vs = Check(f);
        Assert.True(Has(vs, "COLOR_SPACE", ArchiveViolation.Severity.Warn));
    }

    [Fact]
    public void ColorProfile_NoProfile_Infos()
    {
        var f = Skeleton();
        f["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><ColorSpaces><ColorSpace ID=""1"" Type=""RGB""/></ColorSpaces></Res>";
        var vs = Check(f);
        Assert.True(Has(vs, "COLOR_PROFILE", ArchiveViolation.Severity.Info));
    }

    [Fact]
    public void ResourcePlacement_MultiMediaInPublicRes_Warns()
    {
        var f = Skeleton();
        f["Doc_0/PublicRes.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Res xmlns=""{Ns}""><MultiMedias><MultiMedia ID=""1"" Type=""Image"" MediaFile=""Res/x.png""/></MultiMedias></Res>";
        var vs = Check(f);
        Assert.True(Has(vs, "RESOURCE_PLACEMENT", ArchiveViolation.Severity.Warn));
    }

    // ---- 页面内容规则 ----

    [Fact]
    public void PageBlockDepth_TooDeep_Fails()
    {
        var f = Skeleton();
        f["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock><PageBlock><PageBlock><PageBlock>
  <TextObject ID=""1"" Size=""10""><TextCode>x</TextCode></TextObject>
</PageBlock></PageBlock></PageBlock></PageBlock></Page>";
        var vs = Check(f);
        Assert.True(Has(vs, "PAGEBLOCK_DEPTH", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void ClipArea_ZeroArea_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock>
  <ClipArea Boundary=""0 0 0 0""/>
  <TextObject ID=""1"" Size=""10""><TextCode>x</TextCode></TextObject>
</PageBlock></Page>";
        var vs = Check(f);
        Assert.True(Has(vs, "CLIP_AREA", ArchiveViolation.Severity.Warn));
    }

    [Fact]
    public void TextSize_Missing_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock>
  <TextObject ID=""1""><TextCode>x</TextCode></TextObject>
</PageBlock></Page>";
        var vs = Check(f);
        Assert.True(Has(vs, "TEXT_SIZE", ArchiveViolation.Severity.Warn));
    }

    [Fact]
    public void TextHScale_PureHorizontalWithoutHScale_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock>
  <TextObject ID=""1"" Size=""10"" CTM=""2 0 0 1 0 0""><TextCode>x</TextCode></TextObject>
</PageBlock></Page>";
        var vs = Check(f);
        Assert.True(Has(vs, "TEXT_HSCALE", ArchiveViolation.Severity.Warn));
    }

    [Fact]
    public void ImageInterpolate_True_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock>
  <ImageObject ID=""1"" ResourceID=""1"" Interpolate=""true""/>
</PageBlock></Page>";
        var vs = Check(f);
        Assert.True(Has(vs, "IMAGE_INTERPOLATE", ArchiveViolation.Severity.Warn));
    }

    // ---- 文档对象规则 ----

    [Fact]
    public void Annotation_ReadOnlyFalse_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Document>", "<Annotations>Annotations.xml</Annotations></Document>");
        f["Doc_0/Annotations.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Annotations xmlns=""{Ns}""><Annot ID=""1"" ReadOnly=""false"" NoZoom=""true"" NoRotate=""true""/></Annotations>";
        var vs = Check(f);
        Assert.True(Has(vs, "ANNOTATION", ArchiveViolation.Severity.Warn));
    }

    [Fact]
    public void Attachment_BadFormat_Fails()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Document>", "<Attachments>Attachments.xml</Attachments></Document>");
        f["Doc_0/Attachments.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Attachments xmlns=""{Ns}""><Attachment ID=""1"" Name=""doc.pdf"" Format=""PDF"" FileLoc=""a.pdf""/></Attachments>";
        var vs = Check(f);
        Assert.True(Has(vs, "ATTACHMENT", ArchiveViolation.Severity.Error));
    }

    [Fact]
    public void Attachment_NameFromAttribute_IsReported()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Document>", "<Attachments>Attachments.xml</Attachments></Document>");
        // GB/T 33190 CT_Attachment 的名称在 Name 属性上，违规描述应读出该名称而非 "?"
        f["Doc_0/Attachments.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Attachments xmlns=""{Ns}""><Attachment ID=""1"" Name=""心电图影像.png"" Format=""PNG"" FileLoc=""ecg.png""/></Attachments>";
        var vs = Check(f);
        Assert.True(Has(vs, "ATTACHMENT", ArchiveViolation.Severity.Error));
        Assert.Contains(vs, v => v.RuleName == "ATTACHMENT" && v.Description.Contains("心电图影像.png"));
    }

    [Fact]
    public void Signature_Present_Fails()
    {
        var f = Skeleton();
        f["OFD.xml"] = f["OFD.xml"]
            .Replace("<DocRoot>Doc_0/Document.xml</DocRoot>",
                     "<DocRoot>Doc_0/Document.xml</DocRoot><Signatures>Doc_0/Signs/Signatures.xml</Signatures>");
        f["Doc_0/Signs/Signatures.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Signatures xmlns=""{Ns}""><Signature ID=""1"" Type=""Seal""/></Signatures>";
        var vs = Check(f);
        Assert.True(Has(vs, "SIGNATURE", ArchiveViolation.Severity.Error));
    }

    // ---- 多页图像注册 ----

    [Fact]
    public void ImageResourceReg_SharedImage_Warns()
    {
        var f = Skeleton();
        f["Doc_0/Document.xml"] = f["Doc_0/Document.xml"]
            .Replace("</Pages>", "<Page ID=\"2\" BaseLoc=\"Pages/Page_1/Content.xml\"/></Pages>");
        f["Doc_0/Pages/Page_1/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock>
  <ImageObject ID=""2"" ResourceID=""1""/>
</PageBlock></Page>";
        f["Doc_0/Pages/Page_0/Content.xml"] = $@"<?xml version=""1.0"" encoding=""UTF-8""?>
<Page xmlns=""{Ns}""><PageBlock>
  <ImageObject ID=""1"" ResourceID=""1""/>
</PageBlock></Page>";
        var vs = Check(f);
        Assert.True(Has(vs, "IMAGE_RESOURCE_REG", ArchiveViolation.Severity.Warn));
    }
}
