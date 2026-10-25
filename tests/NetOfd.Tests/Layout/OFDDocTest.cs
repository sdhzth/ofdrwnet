using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Reader;
using Xunit;

namespace NetOfd.Tests.Layout;

/// <summary>
/// OFDDoc 功能测试（对应 Java ofdrw-layout OFDDocTest.java）
/// </summary>
public class OFDDocTest
{
    // ── 输出目录 ─────────────────────────────────────────────────
    private static string OutDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "netofd_layout_tests");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Out(string name) => Path.Combine(OutDir(), name);

    // ──────────────────────────────────────────────────────────────
    // 1. 基础文档创建
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 创建空文档，不抛出异常（对应 genDocAndGetDocInfo）
    /// </summary>
    [Fact]
    public void GenDoc_ShouldCreateDocumentWithoutException()
    {
        var outPath = Out("GenDoc.ofd");
        using var doc = new OFDDoc(outPath);
        doc.Add(new Paragraph().AddText("你好呀，OFD Reader & Writer！").SetFontSize(8d));
        doc.Add(new Paragraph().AddText("第二段：这是 netofd .NET 移植版").SetFontSize(5d));
        Assert.True(true, "创建文档不应抛出异常");
    }

    /// <summary>
    /// 创建文档后关闭，输出文件存在（对应 genDocAndGetDocInfo）
    /// </summary>
    [Fact]
    public void GenDoc_OutputFileShouldExistAfterClose()
    {
        var outPath = Out("GenDocClose.ofd");
        var doc = new OFDDoc(outPath);
        doc.Add(new Paragraph().AddText("Hello OFD").SetFontSize(6d));
        doc.Close();
        Assert.True(File.Exists(outPath), $"输出文件应存在: {outPath}");
    }

    // ──────────────────────────────────────────────────────────────
    // 2. Paragraph / Span 基础文本排版
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 添加多 Span 段落，各 Span 样式独立（对应 streamTestLineDiffPageSplit）
    /// </summary>
    [Fact]
    public void Paragraph_MultiSpanStyles_ShouldNotThrow()
    {
        var outPath = Out("MultiSpanStyles.ofd");
        using var doc = new OFDDoc(outPath);

        var sp1 = new Span("坚持就是胜利，").SetFontSize(5d).SetUnderline();
        var sp2 = new Span("加油！").SetFontSize(10d).SetBold();
        var sp3 = new Span("奥力给！").SetFontSize(15d)
                    .SetColor(new CT_Color { Value = "255,0,0" });

        var p = new Paragraph().Add(sp1).Add(sp2).Add(sp3);
        doc.Add(p);
    }

    /// <summary>
    /// 粗体、斜体、下划线属性设置不抛出（对应 streamTestLineDiffPageSplit）
    /// </summary>
    [Fact]
    public void Span_BoldItalicUnderline_ShouldBeSet()
    {
        var sp = new Span("测试文字")
            .SetFontSize(8d)
            .SetBold()
            .SetItalic()
            .SetUnderline();

        Assert.True(sp.Bold);
        Assert.True(sp.Italic);
        Assert.True(sp.Underline);
        Assert.Equal(8d, sp.FontSize);
    }

    /// <summary>
    /// 段落首行缩进属性设置（对应 streamTestParagraphPageSplit）
    /// </summary>
    [Fact]
    public void Paragraph_FirstLineIndent_ShouldBeSet()
    {
        var p = new Paragraph()
            .SetFirstLineIndent(2)
            .AddText("天上的云，姿态万千，变化无常。");

        Assert.Equal(2, p.FirstLineIndent);
        Assert.Single(p.Spans);
    }

    /// <summary>
    /// 段落换行 Span（对应 splitStrToParagraph2）
    /// </summary>
    [Fact]
    public void Paragraph_LineBreakSpan_ShouldBeRecognized()
    {
        var lineBreak = Span.LineBreak();
        Assert.True(lineBreak.IsLineBreak);
        Assert.Null(lineBreak.Content);

        var p = new Paragraph()
            .Add(new Span("第一行").SetFontSize(5d))
            .AddLineBreak()
            .Add(new Span("第二行").SetFontSize(5d));

        Assert.Equal(3, p.Spans.Count);
        Assert.True(p.Spans[1].IsLineBreak);
    }

    /// <summary>
    /// 长文本多段落写入（对应 streamTestParagraphPageSplit "看云识天气"）
    /// </summary>
    [Fact]
    public void OFDDoc_MultiParagraphLongText_ShouldNotThrow()
    {
        var outPath = Out("LongText.ofd");
        using var doc = new OFDDoc(outPath);

        string[] paragraphs = {
            "天上的云，姿态万千，变化无常：有的像羽毛，轻轻地飘在空中；有的像鱼鳞，一片片整整齐齐地排列着。",
            "经验告诉我们：天空的薄云，往往是天气晴朗的象征；那些低而厚密的云层，常常是阴雨风雪的预兆。",
            "夏天，雷雨到来之前，在天空先会出现积云。积云如果迅速向上凸起，形成高大的云山。",
            "云能够帮助我们识别阴晴风雨，预知天气变化，这对工农业生产有着重要的意义。"
        };

        foreach (var text in paragraphs)
            doc.Add(new Paragraph().AddText(text).SetFirstLineIndent(2));

        Assert.True(true);
    }

    // ──────────────────────────────────────────────────────────────
    // 3. Div 盒模型
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Div 盒模型属性（margin / border / padding）正确设置（对应 divBoxTest）
    /// </summary>
    [Fact]
    public void Div_BoxModel_PropertiesShouldBeSet()
    {
        var div = new Div()
            .SetWidth(10d).SetHeight(10d)
            .SetMargin(5d)
            .SetBorder(2d)
            .SetPadding(3d);

        Assert.Equal(10d, div.Width);
        Assert.Equal(10d, div.Height);
        Assert.Equal(5d, div.MarginTop);
        Assert.Equal(5d, div.MarginLeft);
        Assert.Equal(2d, div.BorderTop);
        Assert.Equal(2d, div.BorderRight);
        Assert.Equal(3d, div.PaddingTop);
        Assert.Equal(3d, div.PaddingLeft);
    }

    /// <summary>
    /// Div 背景色 / 边框色（对应 divBoxTest）
    /// </summary>
    [Fact]
    public void Div_Colors_ShouldBeSet()
    {
        var bg = new CT_Color { Value = "30,144,255" };
        var border = new CT_Color { Value = "255,0,0" };

        var div = new Div()
            .SetBackgroundColor(bg)
            .SetBorderColor(border);

        // GB/T 33190 §7.4：Color/@Value 通道值以空格分隔，写入时自动归一化（逗号/分号 → 空格）
        Assert.Equal("30 144 255", div.BackgroundColor?.Value);
        Assert.Equal("255 0 0", div.BorderColor?.Value);
    }

    /// <summary>
    /// Div 不同四边边框（对应 divBoxDiffBorderTest）
    /// </summary>
    [Fact]
    public void Div_FourSidedBorder_ShouldBeSet()
    {
        var div = new Div();
        div.BorderTop = 10d;
        div.BorderRight = 7d;
        div.BorderBottom = 3d;
        div.BorderLeft = 0.5d;

        Assert.Equal(10d, div.BorderTop);
        Assert.Equal(7d, div.BorderRight);
        Assert.Equal(3d, div.BorderBottom);
        Assert.Equal(0.5d, div.BorderLeft);
    }

    /// <summary>
    /// Div 浮动属性（对应 streamTestFloat）
    /// </summary>
    [Fact]
    public void Div_FloatAlignment_ShouldBeSet()
    {
        var left = new Div().SetFloat(AFloat.Left);
        var center = new Div().SetFloat(AFloat.Center);
        var right = new Div().SetFloat(AFloat.Right);

        Assert.Equal(AFloat.Left, left.Float);
        Assert.Equal(AFloat.Center, center.Float);
        Assert.Equal(AFloat.Right, right.Float);
    }

    /// <summary>
    /// Div 绝对定位（对应 divBoxTest）
    /// </summary>
    [Fact]
    public void Div_AbsolutePosition_ShouldBeSet()
    {
        var div = new Div()
            .SetPosition(Position.Absolute)
            .SetXY(70d, 113.5d);

        Assert.Equal(Position.Absolute, div.Position);
        Assert.Equal(70d, div.X);
        Assert.Equal(113.5d, div.Y);
    }

    /// <summary>
    /// 内容区域宽度计算（padding + border 消减）
    /// </summary>
    [Fact]
    public void Div_ContentWidth_ShouldSubtractPaddingAndBorder()
    {
        // width=100, padding=5(lr), border=2(lr) → 内容宽=100-5-5-2-2=86
        var div = new Div()
            .SetWidth(100d)
            .SetPadding(0d, 5d)   // top/bottom=0, left/right=5
            .SetBorder(2d);       // 全为 2

        var contentWidth = div.GetContentWidth(200d);
        Assert.Equal(86d, contentWidth);
    }

    // ──────────────────────────────────────────────────────────────
    // 4. 大批量 Div（分页测试）
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 添加大量 Div 元素不抛出异常（对应 streamTestDivPageSplit，500 个改为 20 个，保持轻量）
    /// </summary>
    [Fact]
    public void OFDDoc_ManyDivs_ShouldNotThrow()
    {
        var outPath = Out("ManyDivs.ofd");
        using var doc = new OFDDoc(outPath);

        for (int i = 0; i < 20; i++)
        {
            var div = new Div()
                .SetWidth(70d).SetHeight(30d)
                .SetFloat(AFloat.Center)
                .SetMargin(5d)
                .SetBorder(0.353d)
                .SetBackgroundColor(new CT_Color { Value = "255,0,0" });
            doc.Add(div);
        }
        Assert.True(true);
    }

    // ──────────────────────────────────────────────────────────────
    // 5. DrawContext 绘图
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 创建 DrawContext，绘制矩形不抛出（对应 canvasInflow / virtualPagePositioning）
    /// </summary>
    [Fact]
    public void DrawContext_FillRect_ShouldNotThrow()
    {
        var outPath = Out("DrawContext_Rect.ofd");
        using var doc = new OFDDoc(outPath);
        doc.AddPage();

        var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
        ctx.FillStyle = "#FF0000";
        ctx.FillRect(10, 10, 50, 30);

        Assert.True(true);
    }

    /// <summary>
    /// DrawContext 绘制路径（线段）不抛出
    /// </summary>
    [Fact]
    public void DrawContext_StrokePath_ShouldNotThrow()
    {
        var outPath = Out("DrawContext_Path.ofd");
        using var doc = new OFDDoc(outPath);
        doc.AddPage();

        var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
        ctx.StrokeStyle = "#0000FF";
        ctx.LineWidth = 0.5;
        ctx.BeginPath()
           .MoveTo(10, 10)
           .LineTo(100, 10)
           .LineTo(100, 100)
           .ClosePath()
           .Stroke();

        Assert.True(true);
    }

    /// <summary>
    /// DrawContext 绘制文字不抛出（对应 FillText）
    /// </summary>
    [Fact]
    public void DrawContext_FillText_ShouldNotThrow()
    {
        var outPath = Out("DrawContext_Text.ofd");
        using var doc = new OFDDoc(outPath);
        doc.AddPage();

        var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
        ctx.Font = "5mm SimSun";
        ctx.FillStyle = "#000000";
        ctx.FillText("看云识天气", 20, 50);

        Assert.True(true);
    }

    /// <summary>
    /// DrawContext Save/Restore 保留状态（对应 Canvas 上下文状态管理）
    /// </summary>
    [Fact]
    public void DrawContext_SaveRestore_ShouldRestoreState()
    {
        var outPath = Out("DrawContext_SaveRestore.ofd");
        using var doc = new OFDDoc(outPath);
        doc.AddPage();

        var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
        ctx.FillStyle = "#FF0000";

        ctx.Save();
        ctx.FillStyle = "#00FF00";

        ctx.Restore();

        // Save/Restore 保存并恢复 FillStyle
        Assert.Equal("#FF0000", ctx.FillStyle);
    }

    /// <summary>
    /// DrawContext 透明度设置（对应 elementOpacityTest / addAnnotation）
    /// </summary>
    [Fact]
    public void DrawContext_GlobalAlpha_ShouldBeSet()
    {
        var outPath = Out("DrawContext_Alpha.ofd");
        using var doc = new OFDDoc(outPath);
        doc.AddPage();

        var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
        ctx.GlobalAlpha = 0.53;

        Assert.Equal(0.53, ctx.GlobalAlpha);
    }

    /// <summary>
    /// DrawContext 变换（Translate / Rotate / Scale）不抛出
    /// </summary>
    [Fact]
    public void DrawContext_Transforms_ShouldNotThrow()
    {
        var outPath = Out("DrawContext_Transform.ofd");
        using var doc = new OFDDoc(outPath);
        doc.AddPage();

        var ctx = doc.CreateDrawContext(new STBox(0, 0, 210, 297));
        ctx.Translate(10, 20);
        ctx.Rotate(Math.PI / 4);
        ctx.Scale(2, 2);

        Assert.True(true);
    }

    // ──────────────────────────────────────────────────────────────
    // 6. 多页
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// AddPage 多次后 PageCount 正确（对应 Java 多页文档）
    /// </summary>
    [Fact]
    public void OFDDoc_AddMultiplePages_PageCountShouldMatch()
    {
        var outPath = Out("MultiPage.ofd");
        using var doc = new OFDDoc(outPath);

        doc.AddPage();
        doc.AddPage();
        doc.AddPage();

        Assert.Equal(3, doc.PageCount);
    }

    /// <summary>
    /// 添加元素后自动创建一页（流式布局分页）
    /// </summary>
    [Fact]
    public void OFDDoc_AddElement_AutoCreatePageIfEmpty()
    {
        var outPath = Out("AutoPage.ofd");
        using var doc = new OFDDoc(outPath);
        doc.Add(new Paragraph().AddText("自动分页测试").SetFontSize(5d));

        // Flush 后至少有 1 页
        doc.Close();

        // 再新建一个文档验证文件大小 > 0
        var info = new FileInfo(outPath);
        Assert.True(info.Length > 0, "输出 OFD 文件不应为空");
    }

    // ──────────────────────────────────────────────────────────────
    // 7. 字体
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Paragraph 字体设置（对应 testAddSysFont）
    /// </summary>
    [Fact]
    public void Paragraph_SetFont_ShouldBeSet()
    {
        var p = new Paragraph()
            .SetFont(NetOfd.Font.FontName.SimSun)
            .SetFontSize(10d)
            .AddText("字体名称：宋体");

        Assert.True(p.DefaultFont.IsBuiltIn);
        Assert.Equal(10d, p.DefaultFontSize);
    }

    /// <summary>
    /// Span 字体设置（对应 testSetSpanWeight）
    /// </summary>
    [Fact]
    public void Span_FontSettings_ShouldBeSet()
    {
        var sp = new Span("坚持就是胜利")
            .SetFontSize(5d)
            .SetBold();

        Assert.Equal(5d, sp.FontSize);
        Assert.True(sp.Bold);
    }

    // ──────────────────────────────────────────────────────────────
    // 8. VirtualPage
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// VirtualPage 尺寸和区域（对应 fillPageParagraph A5 横版）
    /// </summary>
    [Fact]
    public void VirtualPage_CustomSize_ShouldMatchArea()
    {
        var vPage = new VirtualPage { Area = new STBox(0, 0, 210, 140) };
        Assert.Equal(210d, vPage.Width);
        Assert.Equal(140d, vPage.Height);
    }

    /// <summary>
    /// VirtualPage 默认 A4 尺寸
    /// </summary>
    [Fact]
    public void VirtualPage_DefaultSize_ShouldBeA4()
    {
        var vPage = new VirtualPage();
        Assert.Equal(210d, vPage.Width);
        Assert.Equal(297d, vPage.Height);
    }

    // ──────────────────────────────────────────────────────────────
    // 9. OFDDoc 基础属性
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// OFDDoc 默认页面尺寸为 A4（210×297 mm）
    /// </summary>
    [Fact]
    public void OFDDoc_DefaultPageSize_ShouldBeA4()
    {
        using var doc = new OFDDoc(Out("DefaultA4.ofd"));
        Assert.Equal(210d, doc.Width);
        Assert.Equal(297d, doc.Height);
    }

    /// <summary>
    /// OFDDoc 可自定义页面尺寸
    /// </summary>
    [Fact]
    public void OFDDoc_CustomPageSize_ShouldBeSet()
    {
        using var doc = new OFDDoc(Out("CustomSize.ofd"))
        {
            Width = 148,
            Height = 210
        };
        Assert.Equal(148d, doc.Width);
        Assert.Equal(210d, doc.Height);
    }

    /// <summary>
    /// OFDDoc Dispose 两次不抛出异常（对应 try-with-resources）
    /// </summary>
    [Fact]
    public void OFDDoc_DisposeMultipleTimes_ShouldNotThrow()
    {
        var doc = new OFDDoc(Out("DoubleDispose.ofd"));
        doc.Dispose();
        doc.Dispose(); // 第二次不应抛出
        Assert.True(true);
    }

    // ──────────────────────────────────────────────────────────────
    // 10. VirtualPage 固定布局 / Img / Canvas / Watermark
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// AddVPage 虚拟页面固定布局（对应 Java addVPage）
    /// </summary>
    [Fact]
    public void OFDDoc_AddVPage_ShouldCreatePage()
    {
        var outPath = Out("VPage.ofd");
        using var doc = new OFDDoc(outPath);

        var vPage = new VirtualPage(PageLayout.A5());
        var div = new Div()
            .SetPosition(Position.Absolute)
            .SetXY(10, 10)
            .SetWidth(50)
            .SetHeight(20)
            .SetBackgroundColor(new CT_Color { Value = "0,0,255" });
        vPage.Add(div);
        doc.AddVPage(vPage);

        doc.Close();
        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    /// <summary>
    /// VirtualPage 模板支持（对应 Java VirtualPage.addTemplate）：
    /// 渲染时生成独立的 Doc_N/Tpl_0/Content.xml 模板页，
    /// 在 CommonData 注册 &lt;TemplatePage&gt;（BaseLoc 指向 Tpl_0/Content.xml），
    /// 并在主页面以 &lt;Template TemplateID=...&gt; 引用。
    /// </summary>
    [Fact]
    public void VirtualPage_WithTemplate_ShouldEmitTemplateReferenceAndTemplatePage()
    {
        var outPath = Out("VPage_template.ofd");
        using var doc = new OFDDoc(outPath);

        // 母版（模板）：一个蓝色背景矩形
        var tpl = new VirtualPage(PageLayout.A4());
        tpl.Add(new Div()
            .SetPosition(Position.Absolute)
            .SetXY(5, 5)
            .SetWidth(40)
            .SetHeight(15)
            .SetBackgroundColor(new CT_Color { Value = "0,0,255" }));

        // 主页面：引用模板 + 一个红色矩形
        var vPage = new VirtualPage(PageLayout.A4());
        vPage.AddTemplate(tpl);
        vPage.Add(new Div()
            .SetPosition(Position.Absolute)
            .SetXY(60, 60)
            .SetWidth(40)
            .SetHeight(15)
            .SetBackgroundColor(new CT_Color { Value = "255,0,0" }));

        doc.AddVPage(vPage);
        doc.Close();

        Assert.True(File.Exists(outPath));

        using var za = ZipFile.OpenRead(outPath);
        string Norm(string s) => s.Replace('\\', '/');

        // 1) 模板页内容文件应存在
        var tplEntry = za.Entries
            .FirstOrDefault(e => Norm(e.FullName).EndsWith("Tpl_0/Content.xml", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(tplEntry);

        // 2) Document.xml 应注册 TemplatePage 且 BaseLoc 指向 Tpl_0/Content.xml
        var docEntry = za.Entries
            .First(e => Norm(e.FullName).EndsWith("Doc_0/Document.xml", StringComparison.OrdinalIgnoreCase));
        string docXml;
        using (var sr = new StreamReader(docEntry.Open())) docXml = sr.ReadToEnd();
        Assert.Contains("TemplatePage", docXml);
        Assert.Contains("Tpl_0/Content.xml", docXml);

        // 3) 主页面 Content.xml 应含 <Template> 引用（带 TemplateID）
        bool pageHasTemplate = za.Entries
            .Where(e => Norm(e.FullName).Contains("/Pages/") && Norm(e.FullName).EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase))
            .Any(e =>
            {
                using var sr = new StreamReader(e.Open());
                var xml = sr.ReadToEnd();
                return xml.Contains("Template") && xml.Contains("TemplateID");
            });
        Assert.True(pageHasTemplate, "主页面 Content.xml 应包含 <Template> 模板引用");
    }

    /// <summary>
    /// VirtualPage 模板写出后经 OFDReader.GetTemplate 读回应还原模板页实体
    /// （验证写读闭环，对齐附件 reader 往返测试；此前仅测了写出侧 ZIP 结构）。
    /// </summary>
    [Fact]
    public void VirtualPage_WithTemplate_ShouldRoundTripViaReader()
    {
        var outPath = Out("VPage_template_read.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            // 母版（模板）：一个蓝色背景矩形
            var tpl = new VirtualPage(PageLayout.A4());
            tpl.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(5, 5)
                .SetWidth(40)
                .SetHeight(15)
                .SetBackgroundColor(new CT_Color { Value = "0,0,255" }));

            // 主页面：引用模板 + 一个红色矩形
            var vPage = new VirtualPage(PageLayout.A4());
            vPage.AddTemplate(tpl);
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(60, 60)
                .SetWidth(40)
                .SetHeight(15)
                .SetBackgroundColor(new CT_Color { Value = "255,0,0" }));

            doc.AddVPage(vPage);
        }

        // 从 Document.xml 解析模板页 ID
        string docXml;
        using (var za = ZipFile.OpenRead(outPath))
        {
            var docEntry = za.Entries.First(e =>
                e.FullName.Replace('\\', '/').EndsWith("Doc_0/Document.xml", StringComparison.OrdinalIgnoreCase));
            using var sr = new StreamReader(docEntry.Open());
            docXml = sr.ReadToEnd();
        }
        var tplId = XDocument.Parse(docXml)
            .Descendants().First(e => e.Name.LocalName == "TemplatePage")
            .Attribute("ID")?.Value;
        Assert.NotNull(tplId);

        // 经 reader 读回模板页实体
        using var reader = new OFDReader(outPath);
        var entity = reader.GetTemplate(tplId);
        Assert.NotNull(entity);
        Assert.Equal(tplId, entity!.ID!.Value.ToString());
        // BaseLoc 指向 Tpl_0/Content.xml（文档相对路径，reader 自 _docDir 下钻）
        Assert.Contains("Tpl_0/Content.xml", entity.BaseLoc!.Value);
        // 模板页内容应包含绘制图元（蓝色背景矩形渲染为 PathObject）
        Assert.NotNull(entity.Page);
        Assert.Contains("PathObject", entity.Page.Element.ToString());
    }

    /// <summary>
    /// 虚拟页面绝对定位元素必须设置 X/Y/Width（对应 Java 参数校验）
    /// </summary>
    [Fact]
    public void VirtualPage_AddWithoutPosition_ShouldThrow()
    {
        var vPage = new VirtualPage(PageLayout.A4());
        var div = new Div().SetWidth(10).SetHeight(10);
        Assert.Throws<ArgumentException>(() => vPage.Add(div));
    }

    /// <summary>
    /// PageLayout A4 默认尺寸与页边距
    /// </summary>
    [Fact]
    public void PageLayout_A4_Defaults()
    {
        var pl = PageLayout.A4();
        Assert.Equal(210d, pl.Width);
        Assert.Equal(297d, pl.Height);
        Assert.Equal(25.4, pl.MarginTop);
        Assert.Equal(31.7, pl.MarginLeft);
    }

    /// <summary>
    /// Canvas 元素通过 IDrawer 绘制内容（对应 Java Canvas）
    /// </summary>
    [Fact]
    public void OFDDoc_CanvasElement_ShouldRender()
    {
        var outPath = Out("Canvas.ofd");
        using var doc = new OFDDoc(outPath);

        var vPage = new VirtualPage(PageLayout.A4());
        var canvas = new Canvas(100, 50);
        canvas.Drawer = new TestDrawer();
        vPage.Add(canvas.SetPosition(Position.Absolute).SetXY(10, 10));
        doc.AddVPage(vPage);

        doc.Close();
        Assert.True(File.Exists(outPath));
    }

    /// <summary>
    /// 水印绘制（对应 Java addWatermark / WatermarkDrawer）
    /// </summary>
    [Fact]
    public void OFDDoc_AddWatermark_ShouldRender()
    {
        var outPath = Out("Watermark.ofd");
        using var doc = new OFDDoc(outPath);
        doc.AddPage();

        var watermark = new NetOfd.Layout.Edit.Watermark(0, 0, doc.Width, doc.Height)
            .SetValue("机密")
            .SetFontSize(6)
            .SetGlobalAlpha(0.3);
        doc.AddWatermark(watermark);

        doc.Close();
        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    /// <summary>
    /// Img 元素渲染（对应 Java Img + ImgRender）
    /// </summary>
    [Fact]
    public void OFDDoc_ImgElement_ShouldRender()
    {
        // 生成一张测试图片
        var imgPath = Path.Combine(OutDir(), "test.png");
        using (var bmp = new SkiaSharp.SKBitmap(50, 30))
        using (var canvas = new SkiaSharp.SKCanvas(bmp))
        {
            canvas.Clear(SkiaSharp.SKColors.Red);
            using var img = SkiaSharp.SKImage.FromBitmap(bmp);
            using var data = img.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var fs = File.Create(imgPath);
            data.SaveTo(fs);
        }

        var outPath = Out("Img.ofd");
        using var doc = new OFDDoc(outPath);
        var vPage = new VirtualPage(PageLayout.A4());
        var imgElement = new Img(50, 30, imgPath)
            .SetPosition(Position.Absolute)
            .SetXY(20, 20);
        vPage.Add(imgElement);
        doc.AddVPage(vPage);
        doc.Close();

        Assert.True(File.Exists(outPath));
        Assert.True(new FileInfo(outPath).Length > 0);
    }

    /// <summary>测试绘制器：绘制矩形与文字</summary>
    private sealed class TestDrawer : NetOfd.Layout.Element.Canvas.IDrawer
    {
        public void Draw(NetOfd.Layout.Element.Canvas.DrawContext ctx)
        {
            ctx.FillStyle = "#00FF00";
            ctx.FillRect(0, 0, 40, 20);
            ctx.FillStyle = "#000000";
            ctx.Font = "4mm SimSun";
            ctx.FillText("Canvas", 0, 25);
        }
    }

    // ──────────────────────────────────────────────────────────────
    // 边框虚线（Div.BorderDash，对齐 Java setBorderDash）
    // ──────────────────────────────────────────────────────────────

    /// <summary>
    /// 单参数虚线：SetBorderDash(3) 表示线长=空白=3，应写出规范属性 DashPattern="3 3"（无 DashOffset）。
    /// </summary>
    [Fact]
    public void DivBorderDash_SingleValue_ShouldWriteDashPatternOnly()
    {
        var outPath = Out("div_dash_single.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(30, 30)
                .SetWidth(80)
                .SetHeight(40)
                .SetBorder(1)
                .SetBorderDash(3));

            doc.AddVPage(vPage);
        }

        var content = ReadPageContent(outPath);
        var pathObj = content.Descendants()
            .First(e => e.Name.LocalName == "PathObject");
        Assert.Equal("3 3", pathObj.Attribute("DashPattern")?.Value);
        Assert.Null(pathObj.Attribute("DashOffset"));
    }

    /// <summary>
    /// 含偏移量虚线：SetBorderDash(1, 3, 2) 表示偏移 1、线长 3、空白 2，
    /// 应写出 DashOffset="1" + DashPattern="3 2"（对齐 Java Div.setBorderDash 三参语义）。
    /// </summary>
    [Fact]
    public void DivBorderDash_WithOffset_ShouldWriteDashOffsetAndDashPattern()
    {
        var outPath = Out("div_dash_offset.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(30, 30)
                .SetWidth(80)
                .SetHeight(40)
                .SetBorder(1)
                .SetBorderDash(1, 3, 2));

            doc.AddVPage(vPage);
        }

        var content = ReadPageContent(outPath);
        var pathObj = content.Descendants()
            .First(e => e.Name.LocalName == "PathObject");
        Assert.Equal("1", pathObj.Attribute("DashOffset")?.Value);
        Assert.Equal("3 2", pathObj.Attribute("DashPattern")?.Value);
    }

    /// <summary>偶数参数：SetBorderDash(3, 2) 内部补偏移 0 → 仅 DashPattern="3 2"。</summary>
    [Fact]
    public void DivBorderDash_TwoValues_ShouldNormalizeOffsetZero()
    {
        var outPath = Out("div_dash_two.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(30, 30)
                .SetWidth(80)
                .SetHeight(40)
                .SetBorder(1)
                .SetBorderDash(3, 2));

            doc.AddVPage(vPage);
        }

        var content = ReadPageContent(outPath);
        var pathObj = content.Descendants()
            .First(e => e.Name.LocalName == "PathObject");
        Assert.Equal("3 2", pathObj.Attribute("DashPattern")?.Value);
        Assert.Null(pathObj.Attribute("DashOffset"));
    }

    /// <summary>取消虚线（ClearBorderDash / null）→ 边框 PathObject 无 DashPattern。</summary>
    [Fact]
    public void DivBorderDash_Clear_ShouldFallBackToSolid()
    {
        var outPath = Out("div_dash_clear.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            var div = new Div()
                .SetPosition(Position.Absolute)
                .SetXY(30, 30)
                .SetWidth(80)
                .SetHeight(40)
                .SetBorder(1)
                .SetBorderDash(3);
            div.ClearBorderDash();
            vPage.Add(div);

            doc.AddVPage(vPage);
        }

        var content = ReadPageContent(outPath);
        var pathObj = content.Descendants()
            .First(e => e.Name.LocalName == "PathObject");
        Assert.Null(pathObj.Attribute("DashPattern"));
        Assert.Null(pathObj.Attribute("DashOffset"));
    }

    /// <summary>从 zip 读取第一个页面的 Content.xml。</summary>
    private static XElement ReadPageContent(string ofdPath)
    {
        using var za = ZipFile.OpenRead(ofdPath);
        var entry = za.Entries
            .First(e => e.FullName.Replace('\\', '/').Contains("/Pages/")
                        && e.FullName.Replace('\\', '/').EndsWith("Content.xml", StringComparison.OrdinalIgnoreCase));
        using var sr = new StreamReader(entry.Open());
        return XElement.Parse(sr.ReadToEnd());
    }

    // ── Div.Opacity（对齐 Java Div.setOpacity）─────────────────

    /// <summary>
    /// Div 不透明度：背景与边框 PathObject 均应写出 Alpha = opacity × 255（Java DivRender 语义：
    /// 把 opacity 乘到该元素自身生成的背景/边框图元 Alpha 上）。
    /// </summary>
    [Fact]
    public void DivOpacity_ShouldWriteAlphaOnBackgroundAndBorder()
    {
        var outPath = Out("div_opacity.ofd");
        using (var doc = new OFDDoc(outPath))
        {
            var vPage = new VirtualPage(PageLayout.A4());
            vPage.Add(new Div()
                .SetPosition(Position.Absolute)
                .SetXY(30, 30)
                .SetWidth(80)
                .SetHeight(40)
                .SetBackgroundColor(CT_Color.Red)
                .SetBorderColor(CT_Color.Black)
                .SetBorder(1)
                .SetOpacity(0.5));
            doc.AddVPage(vPage);
        }

        var content = ReadPageContent(outPath);
        var paths = content.Descendants()
            .Where(e => e.Name.LocalName == "PathObject").ToList();
        // 背景 + 边框各一个 PathObject
        Assert.Equal(2, paths.Count);
        foreach (var p in paths)
            Assert.Equal("127.5", p.Attribute("Alpha")?.Value);
    }

    /// <summary>
    /// SetOpacity 钳制语义（对齐 Java）：null 移除、&lt;0→0、&gt;1→1、区间内原样。
    /// </summary>
    [Fact]
    public void DivOpacity_ShouldClampToUnitInterval()
    {
        Assert.Equal(1d, new Div().SetOpacity(2).Opacity);
        Assert.Equal(0d, new Div().SetOpacity(-0.5).Opacity);
        Assert.Equal(0.5d, new Div().SetOpacity(0.5).Opacity);
        Assert.Null(new Div().SetOpacity(null).Opacity);
    }
}
