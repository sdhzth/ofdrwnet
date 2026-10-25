using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Converter;
using NetOfd.Layout;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// Text→OFD 转换器测试。
/// 对齐 Java: org.ofdrw.converter.ofdconverter.TextConverter
/// </summary>
public class TextConverterTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_txtconv_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string AllPageText(string ofd)
    {
        var sb = new StringBuilder();
        using var za = ZipFile.OpenRead(ofd);
        var contents = za.Entries
            .Where(e => e.FullName.Contains("/Pages/", StringComparison.OrdinalIgnoreCase) && e.FullName.EndsWith("/Content.xml", StringComparison.OrdinalIgnoreCase))
            .OrderBy(e => e.FullName, StringComparer.Ordinal)
            .ToList();
        foreach (var e in contents)
        {
            using var s = e.Open();
            using var ms = new MemoryStream();
            s.CopyTo(ms);
            var doc = XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
            foreach (var tc in doc.Descendants().Where(d => d.Name.LocalName == "TextCode"))
                sb.Append(tc.Value);
        }
        return sb.ToString();
    }

    /// <summary>逐行追加文本，每个文本行生成一个段落并落版为 TextObject。</summary>
    [Fact]
    public void Convert_LinesBecomeTextObjects()
    {
        var ofdPath = Path.Combine(OutDir(), "txt_convert.ofd");
        var txtPath = Path.Combine(OutDir(), "input.txt");
        File.WriteAllText(txtPath, "Hello World\r\n第二行内容\r\n", Encoding.UTF8);

        using (var conv = new TextConverter(ofdPath))
        {
            conv.Convert(txtPath);
        }

        var text = AllPageText(ofdPath);
        Assert.Contains("Hello World", text);
        Assert.Contains("第二行内容", text);
    }

    /// <summary>Append 直接追加文本，逐次调用均新起一行。</summary>
    [Fact]
    public void Append_AddsLines()
    {
        var ofdPath = Path.Combine(OutDir(), "txt_append.ofd");
        using (var conv = new TextConverter(ofdPath))
        {
            conv.Append("一行");
            conv.Append("二行");
        }
        var text = AllPageText(ofdPath);
        Assert.Contains("一行", text);
        Assert.Contains("二行", text);
    }

    /// <summary>SetFontSize 生效（>3pt 不回落；非正回落为 3pt）。</summary>
    [Fact]
    public void SetFontSize_AppliesToParagraphs()
    {
        var ofdPath = Path.Combine(OutDir(), "txt_size.ofd");
        using (var conv = new TextConverter(ofdPath))
        {
            conv.SetFontSize(12);
            conv.Append("粗字");
            conv.SetFontSize(0);
            conv.Append("回落");
        }
        // 12pt 与 3pt 两段落落版，验证字号写入 OFD（Font 子资源属性）
        using var za = ZipFile.OpenRead(ofdPath);
        var ct = za.Entries.FirstOrDefault(e => e.FullName.EndsWith("/Pages/Page_0/Content.xml", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(ct);
        using var s = ct!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var doc = XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
        var textObjs = doc.Descendants().Where(e => e.Name.LocalName == "TextObject").ToList();
        Assert.Equal(2, textObjs.Count);
    }

    /// <summary>缺失文件 / 目录不做转换（跳过，不新增内容）。</summary>
    [Fact]
    public void Convert_MissingFile_Skips()
    {
        var ofdPath = Path.Combine(OutDir(), "txt_missing.ofd");
        using (var conv = new TextConverter(ofdPath))
        {
            conv.Append("存在");
            conv.Convert(Path.Combine(OutDir(), "no_such.txt"));
        }
        var text = AllPageText(ofdPath);
        Assert.Contains("存在", text);
        Assert.Equal("存在", text);
    }

    /// <summary>SetPageSize 改变页面尺寸。</summary>
    [Fact]
    public void SetPageSize_Applied()
    {
        var ofdPath = Path.Combine(OutDir(), "txt_pagesize.ofd");
        var layout = new PageLayout(100, 200);
        using (var conv = new TextConverter(ofdPath))
        {
            conv.SetPageSize(layout);
            conv.Append("内容");
        }
        using var za = ZipFile.OpenRead(ofdPath);
        var docXml = za.GetEntry("Doc_0/Document.xml");
        Assert.NotNull(docXml);
        using var s = docXml!.Open();
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var doc = XDocument.Parse(Encoding.UTF8.GetString(ms.ToArray()));
        var pageArea = doc.Descendants().First(e => e.Name.LocalName == "PageArea");
        var q = pageArea.Descendants().First(e => e.Name.LocalName == "PhysicalBox").Value
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(p => double.Parse(p, System.Globalization.CultureInfo.InvariantCulture)).ToArray();
        Assert.Equal(100, q[2], 1); // 宽度
        Assert.Equal(200, q[3], 1); // 高度
    }
}
