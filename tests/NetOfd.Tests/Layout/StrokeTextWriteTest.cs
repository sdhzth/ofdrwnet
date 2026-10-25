using NetOfd.Core.BasicType;
using NetOfd.Layout;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// DrawContext.StrokeText 写侧语义：应生成真正「只描边不填充」的轮廓字
/// （Stroke=true / Fill=false / StrokeColor / LineWidth 属性），而非旧实现的普通填充字。
/// </summary>
public class StrokeTextWriteTest
{
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_stroketext_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    private static XElement ReadFirstPageContent(string ofdPath)
    {
        using var za = ZipFile.OpenRead(ofdPath);
        var entry = za.Entries
            .First(e => e.FullName.Replace('\\', '/').Contains("/Pages/")
                        && e.FullName.Replace('\\', '/').EndsWith("Content.xml", System.StringComparison.OrdinalIgnoreCase));
        using var sr = new StreamReader(entry.Open());
        return XElement.Parse(sr.ReadToEnd());
    }

    [Fact]
    public void StrokeText_ShouldEmitStrokeOnlyOutlineText()
    {
        var ofdPath = Out("stroke_write.ofd");
        using (var doc = new OFDDoc(ofdPath))
        {
            doc.AddPage();
            var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
            ctx.Font = "40mm SimSun";
            ctx.FillStyle = "#ff0000";   // 红填充（不应生效于描边字填充）
            ctx.StrokeStyle = "#00ff00"; // 绿描边
            ctx.LineWidth = 2;
            ctx.StrokeText("描边文字", 20, 30);
            ctx.FillText("普通文字", 20, 90);
        }

        var content = ReadFirstPageContent(ofdPath);
        var texts = content.Descendants().Where(e => e.Name.LocalName == "TextObject").ToList();
        Assert.Equal(2, texts.Count);

        // StrokeText → 只描边不填充的轮廓字
        var stroke = texts[0];
        Assert.Equal("true", stroke.Attribute("Stroke")?.Value);
        Assert.Equal("false", stroke.Attribute("Fill")?.Value);
        Assert.Equal("2", stroke.Attribute("LineWidth")?.Value);   // 属性形式（对齐 Java）
        var strokeColor = stroke.Elements().FirstOrDefault(e => e.Name.LocalName == "StrokeColor");
        Assert.NotNull(strokeColor);
        // 颜色值在子元素 Value 属性（CT_Color.Value 序列化为 attr 而非子文本）
        var strokeColorVal = strokeColor!.Attribute("Value")?.Value;
        Assert.NotNull(strokeColorVal);
        Assert.Contains("255", strokeColorVal);                // #00ff00 的 G=255
        Assert.Contains("0", strokeColorVal);

        // FillText → 保持 OFD 默认，不显式写 Stroke/Fill attr（既有产物不变）
        var fill = texts[1];
        Assert.Null(fill.Attribute("Stroke"));
        Assert.Null(fill.Attribute("Fill"));
        Assert.Null(fill.Attribute("LineWidth"));
        Assert.Null(fill.Elements().FirstOrDefault(e => e.Name.LocalName == "StrokeColor"));
    }
}
