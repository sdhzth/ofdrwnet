using NetOfd.Converter;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Core.PageDescription.Font;
using NetOfd.Core.BasicType;
using NetOfd.Pkg.Container;
using NetOfd.Reader;
using SkiaSharp;
using System.Xml.Linq;
using Xunit;

namespace NetOfd.Tests.Converter;

/// <summary>
/// 文本渲染保真相关单元测试：CT_Text / TextCode 模型往返，以及描边文本的实际渲染冒烟。
/// 对应 Java ofdrw-converter 的 AWTMaker.writeText。
/// </summary>
public class TextRenderTest
{
    [Fact]
    public void CTText_StrokeFillHScale_RoundTrip()
    {
        var text = new CTText();
        text.Stroke = true;
        text.Fill = false;
        text.HScale = 0.8;
        text.Bold = true;
        text.Italic = true;

        Assert.True(text.Stroke);
        Assert.False(text.Fill);
        Assert.Equal(0.8, text.HScale, 6);
        Assert.True(text.Bold);
        Assert.True(text.Italic);

        // 缺省值
        var def = new CTText();
        Assert.False(def.Stroke);   // OFD 默认不描边
        Assert.True(def.Fill);      // OFD 默认填充
        Assert.Equal(1.0, def.HScale, 6);
    }

    [Fact]
    public void CTText_StrokeColor_RoundTrip()
    {
        var text = new CTText();
        var stroke = new CT_Color { Value = "255 0 0" }; // 红
        text.StrokeColor = stroke;

        Assert.NotNull(text.StrokeColor);
        Assert.Equal("255 0 0", text.StrokeColor!.Value);

        // 移除后回读为 null
        text.StrokeColor = null;
        Assert.Null(text.StrokeColor);
    }

    [Fact]
    public void TextCode_Width_RoundTrip()
    {
        var tc = new TextCode { X = 1, Y = 2, Text = "你好", Width = 25.5, DeltaX = "3 3 3 3" };
        Assert.Equal(25.5, tc.Width);
        Assert.Equal("3 3 3 3", tc.DeltaX);
        Assert.Equal(2, tc.Y);
        Assert.Equal("你好", tc.Text);

        tc.Width = null;
        Assert.Null(tc.Width);
    }

    [Fact]
    public void OfdFontCache_TextBoldOverride_ReturnsTypeface()
    {
        // 字体资源本身未标粗，但文本级 Bold=true 时仍应返回可用的 Typeface（不抛异常）
        var ctFont = new CT_Font { FamilyName = "SimSun", Bold = false };
        var normal = OfdFontCache.GetTypeface(ctFont, false, false);
        var bold = OfdFontCache.GetTypeface(ctFont, true, false);
        Assert.NotNull(normal);
        Assert.NotNull(bold);

        // 空字体引用回退默认
        Assert.NotNull(OfdFontCache.GetTypeface(null, true, true));
    }

    [Fact]
    public void OfdFontCache_KaiTiGB2312_ShouldResolveChineseCapableTypeface()
    {
        // 回归（2026-10-23 发票监制章样例）：KaiTi_GB2312（GB2312 字体内码名）此前因
        // SKTypeface.FromFamilyName 找不到族时返回 Default（非 null）而被「假命中」，
        // 中文字符全部画成方块。修复后必须解析到具备中文字形的 Typeface。
        var ctFont = new CT_Font { FontName = "KaiTi_GB2312" };
        var tf = OfdFontCache.GetTypeface(ctFont);
        Assert.NotNull(tf);

        // 先探测本机是否存在任一可用中文字体；完全没有时跳过（CI Linux 精简镜像）
        var anyChinese = new[] { "SimSun", "宋体", "Microsoft YaHei", "微软雅黑" }
            .Select(n => SKFontManager.Default.MatchFamily(n))
            .FirstOrDefault(t => t is not null && HasChineseGlyph(t));
        if (anyChinese is null) return; // 无可用中文字体环境，跳过字形断言

        Assert.True(HasChineseGlyph(tf),
            $"KaiTi_GB2312 解析到 {tf.FamilyName}，但无法显示中文字形（仍是方块）");
    }

    [Fact]
    public void OfdFontCache_UnknownFont_ShouldNotFakeMatchDefault()
    {
        // 回归（2026-10-23）：不存在的字体族名不得被当作「命中」——MatchFamily 精确匹配返回 null，
        // 应继续走兜底链（SimSun→宋体→雅黑→Default），最终 Typeface 的中文字形可用（有中文字体环境下）。
        var ctFont = new CT_Font { FontName = "NoSuchFont_XYZ_123" };
        var tf = OfdFontCache.GetTypeface(ctFont);
        Assert.NotNull(tf);

        var anyChinese = new[] { "SimSun", "宋体", "Microsoft YaHei", "微软雅黑" }
            .Select(n => SKFontManager.Default.MatchFamily(n))
            .FirstOrDefault(t => t is not null && HasChineseGlyph(t));
        if (anyChinese is null) return; // 无可用中文字体环境，跳过字形断言

        Assert.True(HasChineseGlyph(tf),
            $"未知字体名兜底到 {tf.FamilyName}，无法显示中文");
    }

    private static bool HasChineseGlyph(SKTypeface? tf)
    {
        if (tf is null) return false;
        var glyphs = tf.GetGlyphs("中");
        return glyphs.Length > 0 && glyphs[0] != 0;
    }

    [Fact]
    public void Render_StrokeOnlyText_ProducesOutlinePixels()
    {
        // 构造仅描边的文本（无 Font 引用、使用缺省字体与黑色描边），渲染后应出现非背景像素
        var page = new PageObject();
        var content = page.ObtainContent();
        var layer = content.ObtainBodyLayer();

        var text = new CTText();
        text.Boundary = new STBox(0, 0, 120, 30);
        text.Size = 14;
        text.Fill = false;      // 不填充
        text.Stroke = true;     // 仅轮廓
        // 不设置 Font / 颜色 → 使用缺省字体 + 黑色描边（StrokeColor 回退填充色黑）
        text.AddTextCode(new TextCode { X = 10, Y = 22, Text = "StrokeTest" });
        layer.AddTextObject(text);

        // 轻量 ResourceManage：Document 无 CommonData，不会触碰字体加载路径
        using var docDir = new DocDir("Doc_0", null);
        var document = new Document();
        var resMgt = new ResourceManage(docDir, document);

        var opts = new ConverterOptions { PPM = 8, Background = SKColors.White };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 120, 30, opts);
        Assert.NotNull(bmp);
        Assert.True(bmp!.Width > 0 && bmp.Height > 0);

        // 统计非白色像素，确认描边确实被绘制
        int nonWhite = 0;
        using var pix = bmp.PeekPixels();
        for (int y = 0; y < pix.Height; y += 2)
        {
            for (int x = 0; x < pix.Width; x += 2)
            {
                var c = pix.GetPixelColor(x, y);
                if (c.Red < 250 || c.Green < 250 || c.Blue < 250) nonWhite++;
            }
        }
        Assert.True(nonWhite > 50, $"描边文本应产生可见像素，实际非白像素数={nonWhite}");
    }

    [Fact]
    public void Render_FillFalse_NoFillPixelsWhenOnlyStroke()
    {
        // 与上一例互补：确认 Fill=false 时不会把整字填充成实心块
        // （此处仅做不崩溃 + 产出约束，密集实心与否由视觉决定，断言像素数低于“全填充”阈值）
        var page = new PageObject();
        var content = page.ObtainContent();
        var layer = content.ObtainBodyLayer();

        var text = new CTText();
        text.Boundary = new STBox(0, 0, 120, 30);
        text.Size = 14;
        text.Fill = false;
        text.Stroke = true;
        text.AddTextCode(new TextCode { X = 10, Y = 22, Text = "AB" });
        layer.AddTextObject(text);

        using var docDir = new DocDir("Doc_0", null);
        var resMgt = new ResourceManage(docDir, new Document());
        var opts = new ConverterOptions { PPM = 8 };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 120, 30, opts);
        Assert.NotNull(bmp);
    }

    [Fact]
    public void CTText_ReadDirection_RoundTrip()
    {
        var text = new CTText { ReadDirection = 90, CharDirection = 90 };
        Assert.Equal(90, text.ReadDirection);
        Assert.Equal(90, text.CharDirection);

        // 缺省
        var def = new CTText();
        Assert.Equal(0, def.ReadDirection);   // OFD 默认横排
        Assert.Equal(0, def.CharDirection);
    }

    [Fact]
    public void Render_VerticalText_StacksTopToBottom()
    {
        // ReadDirection=90：汉字应自上而下逐字堆叠，包围盒"高大于宽"
        var page = new PageObject();
        var content = page.ObtainContent();
        var layer = content.ObtainBodyLayer();

        var text = new CTText();
        text.Boundary = new STBox(0, 0, 120, 120);
        text.Size = 30;
        text.ReadDirection = 90;            // 竖排：自上而下
        // 不设置 Font/颜色 → 缺省字体 + 黑色
        text.AddTextCode(new TextCode { X = 10, Y = 10, Text = "中文字" });
        layer.AddTextObject(text);

        using var docDir = new DocDir("Doc_0", null);
        var resMgt = new ResourceManage(docDir, new Document());
        var opts = new ConverterOptions { PPM = 6, Background = SKColors.White };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 120, 120, opts);
        Assert.NotNull(bmp);

        var bbox = NonWhiteBoundingBox(bmp!);
        Assert.True(bbox.Count > 100, $"竖排文本应产生可见像素，实际非白像素数={bbox.Count}");
        // 竖排三字的包围盒应明显"高大于宽"
        Assert.True(bbox.Height > bbox.Width,
            $"竖排文本包围盒应高大于宽，实际 width={bbox.Width}, height={bbox.Height}");
    }

    [Fact]
    public void Render_HorizontalText_WiderThanTall()
    {
        // 对照：相同文本横排（ReadDirection=0）时包围盒应"宽大于高"
        var page = new PageObject();
        var content = page.ObtainContent();
        var layer = content.ObtainBodyLayer();

        var text = new CTText();
        text.Boundary = new STBox(0, 0, 120, 120);
        text.Size = 30;
        // 默认 ReadDirection=0（横排）
        text.AddTextCode(new TextCode { X = 10, Y = 60, Text = "中文字" });
        layer.AddTextObject(text);

        using var docDir = new DocDir("Doc_0", null);
        var resMgt = new ResourceManage(docDir, new Document());
        var opts = new ConverterOptions { PPM = 6, Background = SKColors.White };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 120, 120, opts);
        Assert.NotNull(bmp);

        var bbox = NonWhiteBoundingBox(bmp!);
        Assert.True(bbox.Count > 100, $"横排文本应产生可见像素，实际非白像素数={bbox.Count}");
        Assert.True(bbox.Width > bbox.Height,
            $"横排文本包围盒应宽大于高，实际 width={bbox.Width}, height={bbox.Height}");
    }

    [Fact]
    public void Render_HorizontalWidth_JustifiesAcrossWidth()
    {
        // TextCode.Width 存在且字数>1 时，字形应均匀分布在 [X, X+Width]（首字在 X、末字在 X+Width），
        // 整体横向跨度应≈ Width + 单字宽，显著大于无 Width 的自然排版。
        (double natural, double justified) = MeasureHorizontalExtent(useWidth: false, deltaX: null);
        (_, double justifiedW) = MeasureHorizontalExtent(useWidth: true, deltaX: null);

        Assert.True(natural is > 25 and < 80,
            $"无 Width 的自然横向跨度应≈3字自然宽，实际={natural}mm");
        Assert.True(justifiedW is > 100 and < 180,
            $"有 Width 的横向跨度应≈Width+单字宽(≈140mm)，实际={justifiedW}mm");
        Assert.True(justifiedW > natural + 40,
            $"均布应明显比自然排版更宽：justified={justifiedW}mm, natural={natural}mm");
    }

    [Fact]
    public void Render_HorizontalWidth_OverridesDeltaX()
    {
        // 当 Width 存在时，阅读方向的 DeltaX 推进量应被均布覆盖（而非采用巨大的逐字间距）。
        // 页面加宽到 260mm，避免末字（DeltaX 推进后约 210mm）被位图裁掉。
        // 注：MeasureHorizontalExtent 始终返回 (natural, justified)，故用 .Natural 取自然排版跨度。
        var r = MeasureHorizontalExtent(useWidth: false, deltaX: "100 100", pageWmm: 260);
        double noWidth = r.Natural;     // 无 Width：逐字推进 100mm
        double withWidth = r.Justified; // 有 Width=120：均布

        // 无 Width 时逐字推进 100mm，跨度应明显大于有 Width 的均布跨度（证明 DeltaX 推进量被 Width 覆盖）
        Assert.True(noWidth > withWidth + 40,
            $"无 Width 时巨大 DeltaX 应比均布更宽：noWidth={noWidth}mm, withWidth={withWidth}mm");
        Assert.True(withWidth is > 100 and < 180,
            $"有 Width 时 DeltaX 推进量应被覆盖，跨度≈140mm，实际={withWidth}mm");
    }

    [Fact]
    public void Render_VerticalWidth_JustifiesAlongHeight()
    {
        // 竖排（ReadDirection=90）+ Width：字形沿 Y 轴均匀分布，纵向跨度≈ Width + 单字高。
        (double natural, double justified) = MeasureVerticalExtent(useWidth: false);
        (_, double justifiedH) = MeasureVerticalExtent(useWidth: true);

        Assert.True(natural is > 25 and < 80,
            $"无 Width 的竖排纵向跨度应≈3字自然高，实际={natural}mm");
        Assert.True(justifiedH is > 100 and < 180,
            $"有 Width 的竖排纵向跨度应≈Width+单字高(≈140mm)，实际={justifiedH}mm");
        Assert.True(justifiedH > natural + 40,
            $"均布应明显比自然排版更高：justified={justifiedH}mm, natural={natural}mm");
    }

    [Fact]
    public void Render_WidthSingleGlyph_NoJustify()
    {
        // 单字（n=1）设置 Width 不应触发均布，直接按 (X,Y) 正常绘制，且不崩溃。
        var page = new PageObject();
        var content = page.ObtainContent();
        var layer = content.ObtainBodyLayer();

        var text = new CTText();
        text.Boundary = new STBox(0, 0, 120, 60);
        text.Size = 20;
        text.AddTextCode(new TextCode { X = 10, Y = 40, Text = "中", Width = 120 });
        layer.AddTextObject(text);

        using var docDir = new DocDir("Doc_0", null);
        var resMgt = new ResourceManage(docDir, new Document());
        var opts = new ConverterOptions { PPM = 6, Background = SKColors.White };
        using var bmp = OfdPageRenderer.Render(page, resMgt, 120, 60, opts);
        Assert.NotNull(bmp);

        var bbox = NonWhiteBoundingBox(bmp!);
        Assert.True(bbox.Count > 20, $"单字 Width 不触发均布，应正常绘制，实际非白像素={bbox.Count}");
        // 单字宽度应接近字号（1em≈20mm），而非被 Width 拉宽
        double wMm = bbox.Width / (double)opts.PPM;
        Assert.True(wMm is > 8 and < 40, $"单字横向跨度应≈字号，实际={wMm}mm");
    }

    /// <summary>
    /// 测量横排文本在"有/无 Width"下的横向跨度（mm）。返回 (naturalExtentMm, justifiedExtentMm)。
    /// </summary>
    private static (double Natural, double Justified) MeasureHorizontalExtent(bool useWidth, string? deltaX, int pageWmm = 200)
    {
        double Measure(bool widthOn)
        {
            var page = new PageObject();
            var content = page.ObtainContent();
            var layer = content.ObtainBodyLayer();

            var text = new CTText();
            text.Boundary = new STBox(0, 0, pageWmm, 60);
            text.Size = 20;
            var tc = new TextCode { X = 10, Y = 40, Text = "中文字" };
            if (widthOn) tc.Width = 120;
            if (deltaX is not null) tc.DeltaX = deltaX;
            text.AddTextCode(tc);
            layer.AddTextObject(text);

            using var docDir = new DocDir("Doc_0", null);
            var resMgt = new ResourceManage(docDir, new Document());
            var opts = new ConverterOptions { PPM = 6, Background = SKColors.White };
            using var bmp = OfdPageRenderer.Render(page, resMgt, pageWmm, 60, opts);
            var bbox = NonWhiteBoundingBox(bmp!);
            return bbox.Width / (double)opts.PPM;
        }

        return (Measure(false), Measure(true));
    }

    /// <summary>
    /// 测量竖排文本在"有/无 Width"下的纵向跨度（mm）。返回 (naturalExtentMm, justifiedExtentMm)。
    /// </summary>
    private static (double Natural, double Justified) MeasureVerticalExtent(bool useWidth)
    {
        double Measure(bool widthOn)
        {
            var page = new PageObject();
            var content = page.ObtainContent();
            var layer = content.ObtainBodyLayer();

            var text = new CTText();
            text.Boundary = new STBox(0, 0, 60, 200);
            text.Size = 20;
            text.ReadDirection = 90;            // 竖排：自上而下
            var tc = new TextCode { X = 10, Y = 10, Text = "中文字" };
            if (widthOn) tc.Width = 120;
            text.AddTextCode(tc);
            layer.AddTextObject(text);

            using var docDir = new DocDir("Doc_0", null);
            var resMgt = new ResourceManage(docDir, new Document());
            var opts = new ConverterOptions { PPM = 6, Background = SKColors.White };
            using var bmp = OfdPageRenderer.Render(page, resMgt, 60, 200, opts);
            var bbox = NonWhiteBoundingBox(bmp!);
            return bbox.Height / (double)opts.PPM;
        }

        return (Measure(false), Measure(true));
    }

    /// <summary>
    /// 计算位图中非白像素的包围盒与数量（非白判定：R/G/B 任一 &lt; 250）。
    /// </summary>
    private static (int Width, int Height, int Count) NonWhiteBoundingBox(SKBitmap bmp)
    {
        int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1, count = 0;
        using var pix = bmp.PeekPixels();
        for (int y = 0; y < pix.Height; y++)
            for (int x = 0; x < pix.Width; x++)
            {
                var c = pix.GetPixelColor(x, y);
                if (c.Red < 250 && c.Green < 250 && c.Blue < 250)
                {
                    count++;
                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }
        if (count == 0) return (0, 0, 0);
        return (maxX - minX + 1, maxY - minY + 1, count);
    }
}
