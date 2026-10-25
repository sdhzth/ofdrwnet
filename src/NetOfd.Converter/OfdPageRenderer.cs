using NetOfd.Core;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Core.CompositeObj;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Reader;
using NetOfd.Reader.Model;
using SkiaSharp;
using System.Globalization;
using System.Xml.Linq;

namespace NetOfd.Converter;

/// <summary>
/// OFD 页面渲染器（对应 Java ofdrw-converter 的 AWTMaker）。
/// 将一页 <see cref="PageObject"/> 的内容（文字 / 路径 / 图像）绘制到 SkiaSharp 画布。
/// </summary>
public static class OfdPageRenderer
{
    /// <summary>
    /// 将指定页渲染为位图。
    /// </summary>
    /// <param name="page">页对象（含图层与图元）</param>
    /// <param name="resMgt">资源管理器（解析字体 / 图像）</param>
    /// <param name="pageWmm">页面宽度（mm）</param>
    /// <param name="pageHmm">页面高度（mm）</param>
    /// <param name="opts">配置项</param>
    /// <param name="annots">页面注释实体（可为 null）；非 null 时在图层之后绘制注释外观（对齐 Java AWTMaker）</param>
    /// <returns>渲染后的位图；页为空时返回 null</returns>
    public static SKBitmap? Render(
        PageObject? page,
        ResourceManage resMgt,
        double pageWmm,
        double pageHmm,
        ConverterOptions opts,
        IReadOnlyList<AnnotionEntity>? annots = null)
    {
        if (page is null) return null;

        int w = (int)Math.Max(1, Math.Round(pageWmm * opts.PPM));
        int h = (int)Math.Max(1, Math.Round(pageHmm * opts.PPM));

        var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(opts.Background);
        RenderTo(canvas, page, resMgt, opts, annots);
        return bitmap;
    }

    /// <summary>
    /// 将指定页绘制到任意 <see cref="SKCanvas"/>（位图画布、SVG 画布、PDF 画布皆可）。
    /// <para>
    /// 方法内部先按 <see cref="ConverterOptions.PPM"/> 缩放画布，
    /// 之后所有绘制均以**毫米**为单位——与 OFD 内部单位一致。
    /// 调用方需自行准备画布尺寸与背景。
    /// </para>
    /// </summary>
    /// <param name="canvas">目标画布（不会被本方法释放）</param>
    /// <param name="page">页对象</param>
    /// <param name="resMgt">资源管理器</param>
    /// <param name="opts">配置项</param>
    /// <param name="annots">页面注释实体（可为 null）；非 null 时在图层之后绘制注释外观（对齐 Java AWTMaker）</param>
    public static void RenderTo(
        SKCanvas canvas,
        PageObject? page,
        ResourceManage resMgt,
        ConverterOptions opts,
        IReadOnlyList<AnnotionEntity>? annots = null)
    {
        if (page is null) return;

        int restoreTo = canvas.Save();
        try
        {
            var content = page.Content;
            if (content is null) return;

            // 按图层 Z 序绘制（Background → Body → Foreground）
            foreach (var layer in content.GetLayersInZOrder())
            {
                if (layer is null) continue;

                // 图层级 DrawParam 级联（透明度乘积 + 可见性）
                double layerAlpha = 1.0;
                bool layerVisible = true;
                if (layer.DrawParam is not null)
                {
                    var dp = resMgt.GetDrawParamResolved(layer.DrawParam);
                    if (dp is not null)
                    {
                        layerVisible = dp.Visible;
                        if (dp.RawAlpha is not null) layerAlpha *= dp.RawAlpha.Value / 255.0;
                    }
                }
                if (!layerVisible) continue;

                // GetGraphicObjects 已递归展开 PageBlock，顺序即 Z 序
                foreach (var obj in layer.GetGraphicObjects())
                {
                    if (obj is null) continue;

                    // 图元级 DrawParam 级联（继承图层级 α）
                    double objAlpha = layerAlpha;
                    bool objVisible = true;
                    CT_DrawParam? dp = null;
                    if (obj.DrawParam is not null)
                    {
                        dp = resMgt.GetDrawParamResolved(obj.DrawParam);
                        if (dp is not null)
                        {
                            if (!dp.Visible) objVisible = false;
                            if (dp.RawAlpha is not null) objAlpha *= dp.RawAlpha.Value / 255.0;
                        }
                    }
                    // 图元自身 Visible（缺省 true）与 Alpha（缺省 255）
                    if (!obj.Visible) objVisible = false;
                    var ownAlpha = obj.Alpha;
                    if (ownAlpha is not null) objAlpha *= ownAlpha.Value / 255.0;

                    if (!objVisible || objAlpha <= 0) continue;

                    DrawGraphicUnit(canvas, obj, resMgt, opts, objAlpha, dp);
                }
            }

            // 页面注释外观：图层全部画完后绘制（对齐 Java AWTMaker.writePage 末尾的
            // 「绘制注解对象」循环——电子印章、水印等注释均走此路径）。
            if (annots is not null && annots.Count > 0)
                RenderAnnots(canvas, annots, resMgt, opts);
        }
        finally
        {
            canvas.RestoreToCount(restoreTo);
        }
    }

    /// <summary>
    /// 绘制单个图元（文字 / 路径 / 图像 / 复合对象）：含 DrawParam 透明度、裁剪区域与完整变换矩阵。
    /// 供页面图层与注释外观渲染共用。
    /// </summary>
    private static void DrawGraphicUnit(SKCanvas canvas, CTGraphicUnit obj, ResourceManage resMgt,
        ConverterOptions opts, double objAlpha, CT_DrawParam? dp)
    {
        canvas.Save();

        ApplyClips(canvas, obj, opts);
        ApplyGraphicUnitMatrix(canvas, obj, opts);

        switch (obj)
        {
            case CTText text:
                DrawText(canvas, text, resMgt, opts, objAlpha, dp);
                break;
            case CTPath path:
                DrawPath(canvas, path, resMgt, opts, objAlpha, dp);
                break;
            case CTImage image:
                DrawImage(canvas, image, resMgt, obj.Boundary, obj.CTM is { Count: >= 6 }, objAlpha);
                break;
            case CT_Composite composite:
                // 复合对象：矩阵/裁剪已由上方统一施加，此处只需解引用并递归其内容
                RenderCompositeContent(canvas, composite, resMgt, opts, 0);
                break;
        }

        canvas.Restore();
    }

    /// <summary>应用图元的裁剪区域（页坐标 mm；同一 Clip 内多 Area 取并集，PPM 烘焙进路径）。</summary>
    private static void ApplyClips(SKCanvas canvas, CTGraphicUnit obj, ConverterOptions opts)
    {
        // 注意：本渲染器不设全局缩放，各图元矩阵自带 PPM——
        // 因此这里把 PPM 直接烘焙进裁剪路径，再以像素坐标应用。
        var clips = obj.GetClips();
        if (clips is null) return;

        foreach (var clip in clips.Clips)
        {
            SKPath? union = null;
            foreach (var area in clip.Areas)
            {
                if (string.IsNullOrWhiteSpace(area.AbbreviatedData)) continue;
                using var sp = OfdPathParser.Parse(area.AbbreviatedData);
                if (sp.IsEmpty) continue;
                if (union is null)
                {
                    union = new SKPath(sp);
                }
                else
                {
                    using var merged = OpUnion(union, sp);
                    union.Dispose();
                    union = new SKPath(merged);
                }
            }
            if (union is not null)
            {
                using var scaled = new SKPath();
                union.Transform(SKMatrix.CreateScale((float)opts.PPM, (float)opts.PPM), scaled);
                canvas.ClipPath(scaled, SKClipOperation.Intersect, opts.AntiAlias);
                union.Dispose();
            }
        }
    }

    /// <summary>
    /// 应用图元的完整变换矩阵（Skia 列向量约定，p' = M · p）：
    /// M = Scale(PPM) · Translate(Boundary) · CTM，
    /// 即点先在图元坐标系内经 CTM 形变，再平移到 Boundary 左上角，最后整体乘 PPM。
    /// <para>
    /// 必须用 SetMatrix 直接写入“绝对矩阵”，不能依赖 canvas.Concat/Translate
    /// 的前乘/后乘语义——实测 Skia 的 Concat(M) 等价于 M · current（传入矩阵
    /// 作用在最外层）。若改用 Translate(Boundary) 后 Concat(CTM) 会得到
    /// CTM · T，平移量被 CTM 二次缩放，使带 CTM 的图元落到错误位置。
    /// </para>
    /// </summary>
    private static void ApplyGraphicUnitMatrix(SKCanvas canvas, CTGraphicUnit obj, ConverterOptions opts)
    {
        var m = canvas.TotalMatrix;
        m = SKMatrix.Concat(m, SKMatrix.CreateScale((float)opts.PPM, (float)opts.PPM));
        var b = obj.Boundary;
        if (b is not null)
            m = SKMatrix.Concat(m, SKMatrix.CreateTranslation((float)b.TopLeftX, (float)b.TopLeftY));
        if (obj.CTM is { Count: >= 6 } ctm)
            m = SKMatrix.Concat(m, ToSKMatrix(ctm));
        canvas.SetMatrix(m);
    }

    // ── 页面注释（对齐 Java AWTMaker.writePage 注解对象循环）────

    /// <summary>
    /// 渲染页面注释外观：每个 <see cref="NetOfd.Core.Annotation.PageAnnot.Annot.Appearance"/>（CT_PageBlock）按 Boundary
    /// 平移到页面位置后，递归绘制其中的图元（文字 / 路径 / 图像 / 复合对象）。
    /// 电子印章（Stamp）、水印等注释均走此路径。
    /// </summary>
    private static void RenderAnnots(SKCanvas canvas, IReadOnlyList<AnnotionEntity> annots,
        ResourceManage resMgt, ConverterOptions opts)
    {
        foreach (var entity in annots)
        {
            if (entity.Annots is null) continue;
            foreach (var annot in entity.Annots)
            {
                var appearance = annot.Appearance;
                if (appearance is null) continue;

                canvas.Save();
                try
                {
                    // Appearance.Boundary 即注释在页面上的位置（mm）；
                    // 本渲染器画布无全局缩放，故按 PPM 换算为像素平移。
                    var b = appearance.Boundary;
                    if (b is not null)
                        canvas.Translate((float)(b.TopLeftX * opts.PPM), (float)(b.TopLeftY * opts.PPM));
                    RenderBlockChildren(canvas, appearance, resMgt, opts, 0);
                }
                finally
                {
                    canvas.Restore();
                }
            }
        }
    }

    /// <summary>
    /// 递归渲染页块容器内的子元素：图元直接绘制，PageBlock/Content/Layer 下钻，
    /// CompositeObject 经 <see cref="ResourceManage.GetCompositeGraphicUnit"/> 解引用后递归。
    /// </summary>
    /// <param name="canvas">Skia 画布。</param>
    /// <param name="block">页块容器（Appearance / CT_PageBlock / Content 等任意 OFDElement）</param>
    /// <param name="resMgt">资源管理器，用于解引用复合对象等资源引用。</param>
    /// <param name="opts">转换选项（PPM 等）。</param>
    /// <param name="depth">当前递归深度，用于防御自引用或超深嵌套。</param>
    private static void RenderBlockChildren(SKCanvas canvas, OFDElement block,
        ResourceManage resMgt, ConverterOptions opts, int depth)
    {
        // 防御：异常文件可能构造出自引用或超深嵌套
        if (depth > 32) return;

        foreach (var e in block.Element.Elements())
        {
            var local = e.Name.LocalName;
            switch (local)
            {
                case "TextObject":
                    DrawGraphicUnit(canvas, new CTText(e), resMgt, opts, 1.0, null);
                    break;
                case "ImageObject":
                    DrawGraphicUnit(canvas, new CTImage(e), resMgt, opts, 1.0, null);
                    break;
                case "PathObject":
                    DrawGraphicUnit(canvas, new CTPath(e), resMgt, opts, 1.0, null);
                    break;
                case "PageBlock":
                case "Content":
                case "Layer":
                    RenderBlockChildren(canvas, new CT_PageBlock(e), resMgt, opts, depth + 1);
                    break;
                case "CompositeObject":
                    // 复合对象经 DrawGraphicUnit 统一路径：施加自身矩阵/裁剪后解引用内容
                    DrawGraphicUnit(canvas, new CT_Composite(e), resMgt, opts, 1.0, null);
                    break;
            }
        }
    }

    /// <summary>
    /// 渲染复合对象：按 ResourceID 解引用矢量图像资源（CT_VectorG），
    /// 将其 Content 内的图元按 CompositeObject 自身的 Boundary/CTM 绘制。
    /// 对齐 Java AWTMaker.writeComposite（VectorG 的 Width/Height 为裁剪语义，不做额外缩放；
    /// 缩放由图元自身 CTM 承担）。
    /// </summary>
    /// <summary>
    /// 解引用并渲染复合对象内容：按 ResourceID 取矢量图像资源（CT_VectorG），
    /// 递归绘制其 Content 内的图元。调用方需已施加复合对象自身的矩阵与裁剪
    /// （<see cref="DrawGraphicUnit"/> 或 <see cref="RenderBlockChildren"/> 均已处理）。
    /// 对齐 Java AWTMaker.writeComposite：VectorG 的 Width/Height 为裁剪语义，不做额外缩放，
    /// 缩放由图元自身 CTM 承担。
    /// </summary>
    private static void RenderCompositeContent(SKCanvas canvas, CT_Composite composite,
        ResourceManage resMgt, ConverterOptions opts, int depth)
    {
        var refId = composite.ResourceID;
        if (refId is null) return;

        var vectorG = resMgt.GetCompositeGraphicUnit(refId);
        if (vectorG is null) return;

        RenderBlockChildren(canvas, vectorG.GetContent(), resMgt, opts, depth + 1);
    }

    // ── 文字 ────────────────────────────────────────────────
    private static void DrawText(SKCanvas canvas, CTText text, ResourceManage resMgt, ConverterOptions opts,
        double effAlpha = 1.0, CT_DrawParam? dp = null)
    {
        var ctFont = text.Font is not null ? resMgt.GetFont(text.Font.Value) : null;

        // 文本级 Bold/Italic 覆盖字体资源设置（很多 OFD 在 Text 上标粗而非 Font 上）
        bool bold = text.Bold || (ctFont?.Bold ?? false);
        bool italic = text.Italic || (ctFont?.Italic ?? false);

        // 填充色来自 TextColor(FillColor)，缺省回退 DrawParam 继承链，再退黑色；OFD 默认 Fill=true
        var fillColor = OfdColorUtil.ToSKColor(
            (CT_Color?)(text.TextColor ?? dp?.FillColor), SKColors.Black, resMgt);
        bool doFill = text.Fill;
        // 描边色来自 StrokeColor，缺省回退到填充色；OFD 默认 Stroke=false
        bool doStroke = text.Stroke;
        var strokeColor = OfdColorUtil.ToSKColor(text.StrokeColor, fillColor, resMgt);

        // 应用有效透明度（图层 α × 图元 DrawParam α × 图元自身 Alpha，累积乘积）
        fillColor = ApplyAlpha(fillColor, effAlpha);
        strokeColor = ApplyAlpha(strokeColor, effAlpha);
        // 轮廓字线宽取文本 LineWidth，缺省回退 DrawParam 链，再退 0.35mm
        float strokeW = (float)((text.LineWidth ?? dp?.RawLineWidth ?? 0.35) * opts.PPM);

        using var fillPaint = new SKPaint
        {
            IsAntialias = opts.AntiAlias,
            Style = SKPaintStyle.Fill,
            Typeface = OfdFontCache.GetTypeface(ctFont, bold, italic),
            TextSize = (float)(text.Size * opts.TextSizeScale),
            Color = fillColor,
            TextAlign = SKTextAlign.Left,
            TextScaleX = (float)(text.HScale > 0 ? text.HScale : 1.0),
        };
        using var strokePaint = new SKPaint
        {
            IsAntialias = opts.AntiAlias,
            Style = SKPaintStyle.Stroke,
            Typeface = fillPaint.Typeface,
            TextSize = fillPaint.TextSize,
            Color = strokeColor,
            StrokeWidth = strokeW,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round,
            TextAlign = SKTextAlign.Left,
            TextScaleX = fillPaint.TextScaleX,
        };

        fillPaint.GetFontMetrics(out var fm);
        float emPx = fillPaint.TextSize;                       // 字身框(em box)高度(px)
        // 字形包围盒中心相对基线的偏移（用于竖排时把字形在 em 框内竖直居中）
        float vCenterOff = (fm.Ascent + fm.Descent) / 2;

        int readDir = text.ReadDirection;
        int charDir = text.CharDirection;
        bool vertical = readDir == 90 || readDir == 270;

        // 竖排时改用居中对齐，便于绕字身框中心旋转
        if (vertical)
        {
            fillPaint.TextAlign = SKTextAlign.Center;
            strokePaint.TextAlign = SKTextAlign.Center;
        }

        foreach (var tc in text.GetTextCodes())
        {
            var content = (tc.Text ?? "").Replace("\r", "").Replace("\n", "");
            if (content.Length == 0) continue;

            double x = tc.X;
            double y = tc.Y;
            // DeltaX/DeltaY 支持 "g n v" 压缩语法，展开后按字数补齐（DeltaTool 对齐 Java）；
            // 标准 len-1 输入补齐一位、其余情况与原有尾值回退语义等价。
            var dx = DeltaTool.GetDelta(tc.DeltaX, content.Length);
            var dy = DeltaTool.GetDelta(tc.DeltaY, content.Length);
            double? width = tc.Width;
            int n = content.Length;

            // 两端对齐（均布）：OFD 规范 §7.7 TextCode.Width — 当该属性存在且字数>1 时，
            // 字形在 [起点, 起点+Width] 内均匀分布（首字在起点、末字在起点+Width），
            // 覆盖该阅读方向上的 DeltaX/DeltaY 推进量（交叉轴偏移仍生效）。
            bool justified = width.HasValue && width.Value > 0 && n > 1;

            if (vertical)
            {
                // 竖排：字符自上而下(90)或自下而上(270)堆叠，单字绕字身框中心按 CharDirection 旋转。
                // OFD 规范 §7.7：ReadDirection=90 时字符沿 Y 轴排列，TextCode 的 (X,Y) 为首个字符 em 框左上角。
                int vSign = readDir == 270 ? -1 : 1;
                double vStep = justified ? width!.Value / (n - 1) : 0;
                double centerX = x + emPx / 2.0;               // 首字字身框中心 X（交叉轴基线）
                double curY = y + emPx / 2.0;                  // 自然推进起点（首字字身框中心 Y）
                for (int i = 0; i < n; i++)
                {
                    // 交叉轴（横向）逐字偏移（沿用 Java 逐字绝对偏移语义）
                    float ox = (float)((i < dx.Count) ? dx[i] : (dx.Count > 0 ? dx[^1] : 0));
                    double centerY;
                    if (justified)
                    {
                        // 均布：沿阅读方向按 vStep 推进，中心 = em 框左上角 + emPx/2
                        double topLeftY = y + vSign * i * vStep;
                        centerY = topLeftY + emPx / 2.0;
                    }
                    else
                    {
                        // 自然推进 = 字身框高度(emPx) + 额外 DeltaY
                        centerY = curY;
                        double adv = emPx + ((i < dy.Count) ? dy[i] : (dy.Count > 0 ? dy[^1] : 0));
                        curY += vSign * adv;
                    }
                    DrawVerticalGlyph(canvas, content[i].ToString(),
                        (float)(centerX + ox), (float)centerY,
                        fillPaint, strokePaint, doFill, doStroke, charDir, vCenterOff);
                }
            }
            else
            {
                bool rtl = readDir == 180;
                double step = justified ? width!.Value / (n - 1) : 0;
                if (justified)
                {
                    // 均布：首字锚点 X，末字锚点 X+Width，沿 X 轴等间距推进；
                    // 交叉轴（纵向）偏移按 DeltaY 累积生效，阅读方向推进量被 Width 覆盖。
                    for (int i = 0; i < n; i++)
                    {
                        double gx = x + (rtl ? -step * i : step * i);
                        double cumDy = 0;
                        for (int k = 0; k < i; k++)
                            if (dy.Count > 0)
                                cumDy += (k < dy.Count) ? dy[k] : dy[^1];
                        double gy = y + cumDy;
                        DrawTextSegment(canvas, content[i].ToString(), (float)gx, (float)gy, fillPaint, strokePaint, doFill, doStroke);
                    }
                }
                else if (dx.Count == 0)
                {
                    DrawTextSegment(canvas, content, (float)x, (float)y, fillPaint, strokePaint, doFill, doStroke);
                }
                    else
                    {
                        double curX = x, curY = y;
                        for (int i = 0; i < n; i++)
                        {
                            DrawTextSegment(canvas, content[i].ToString(), (float)curX, (float)curY, fillPaint, strokePaint, doFill, doStroke);
                            double adv = (i < dx.Count) ? dx[i] : dx[^1];
                            curX += (rtl ? -adv : adv);
                            if (dy.Count > 0)
                                curY += (i < dy.Count) ? dy[i] : dy[^1];
                        }
                    }
            }
        }
    }

    /// <summary>
    /// 竖排模式下绘制单个字形：平移到字身框中心后按 CharDirection 旋转，使字形绕中心旋转并竖直居中于 em 框。
    /// </summary>
    private static void DrawVerticalGlyph(
        SKCanvas canvas, string ch, float cx, float cy,
        SKPaint fillPaint, SKPaint strokePaint, bool doFill, bool doStroke,
        int charDirDeg, float vCenterOff)
    {
        canvas.Save();
        canvas.Translate(cx, cy);
        if (charDirDeg != 0)
            canvas.RotateDegrees(charDirDeg);
        // 字形包围盒中心相对基线的偏移为 (Ascent+Descent)/2；将绘制基线置于 -vCenterOff 使其竖直居中于 em 框
        if (doStroke) canvas.DrawText(ch, 0, -vCenterOff, strokePaint);
        if (doFill) canvas.DrawText(ch, 0, -vCenterOff, fillPaint);
        canvas.Restore();
    }

    /// <summary>
    /// 绘制一段文本：对齐 Java AWTMaker.renderChar，先描边后填充。
    /// 仅 Fill 时只填充；仅 Stroke 时只轮廓；两者皆有则填充盖在描边之上。
    /// </summary>
    private static void DrawTextSegment(
        SKCanvas canvas, string s, float x, float y,
        SKPaint fillPaint, SKPaint strokePaint, bool doFill, bool doStroke)
    {
        if (doStroke) canvas.DrawText(s, x, y, strokePaint);
        if (doFill) canvas.DrawText(s, x, y, fillPaint);
    }

    // ── 路径 ────────────────────────────────────────────────
    /// <summary>两个路径的并集（用于 Clip 内多 Area 合成）。</summary>
    private static SKPath OpUnion(SKPath a, SKPath b)
    {
        var result = new SKPath();
        result.AddPath(a);
        result.AddPath(b);
        return result;
    }

    private static void DrawPath(SKCanvas canvas, CTPath path, ResourceManage resMgt, ConverterOptions opts,
        double effAlpha = 1.0, CT_DrawParam? dp = null)
    {
        using var skPath = OfdPathParser.Parse(path.AbbreviatedData);
        if (skPath.IsEmpty) return;

        // 样式级联：图元自有值优先，未设置时回退 DrawParam 继承链（GB/T 33190 §8.1.2）
        float lineWidth = (float)(path.RawLineWidth ?? dp?.RawLineWidth ?? 0.353);
        string? cap = path.Cap ?? dp?.RawCap?.ToString();
        string? join = path.Join ?? dp?.RawJoin?.ToString();
        var strokeColor = path.StrokeColor ?? dp?.StrokeColor;
        var fillColor = path.FillColor ?? dp?.FillColor;

        // 描边
        if (path.Stroke)
        {
            var strokePattern = strokeColor?.GetPattern();
            if (strokePattern is not null)
            {
                // 图案描边：先把描边轮廓解析为填充路径（含线宽/线帽/虚线），再铺底纹
                using var outlinePaint = new SKPaint
                {
                    IsAntialias = opts.AntiAlias,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = lineWidth,
                    StrokeCap = MapCap(cap),
                    StrokeJoin = MapJoin(join),
                };
                if (path.Dash is { Count: >= 2 } dash)
                {
                    var intervals = dash.ToDoubleArray().Select(x => (float)x).ToArray();
                    outlinePaint.PathEffect = SKPathEffect.CreateDash(intervals, (float)(path.DashOffset ?? 0d));
                }
                using var outline = new SKPath();
                if (outlinePaint.GetFillPath(skPath, outline) && !outline.IsEmpty)
                    DrawPatternFill(canvas, outline, strokePattern, resMgt, effAlpha);
            }
            else
            {
                using var stroke = new SKPaint
                {
                    IsAntialias = opts.AntiAlias,
                    Style = SKPaintStyle.Stroke,
                    StrokeWidth = lineWidth,
                    StrokeCap = MapCap(cap),
                    StrokeJoin = MapJoin(join),
                };
                // 渐变描边：优先挂接着色器；纯色走常规路径
                var strokeShader = OfdColorUtil.TryCreateShader(strokeColor, resMgt, path.Boundary);
                if (strokeShader is not null)
                {
                    stroke.Shader = strokeShader;
                    stroke.Color = ApplyAlpha(SKColors.White, effAlpha); // 着色器自带颜色，此处仅控整体透明度
                }
                else
                {
                    stroke.Color = ApplyAlpha(OfdColorUtil.ToSKColor(strokeColor, SKColors.Black, resMgt), effAlpha);
                }
                if (path.Dash is { Count: >= 2 } dash)
                {
                    var intervals = dash.ToDoubleArray().Select(x => (float)x).ToArray();
                    // DashPattern 仅含「线长 空白」序列；偏移量在规范/Java 中单独存 DashOffset 属性
                    stroke.PathEffect = SKPathEffect.CreateDash(intervals, (float)(path.DashOffset ?? 0d));
                }
                canvas.DrawPath(skPath, stroke);
            }
        }

        // 填充
        if (path.Fill)
        {
            skPath.FillType = path.Rule == "EvenOdd"
                ? SKPathFillType.EvenOdd
                : SKPathFillType.Winding;

            // 高洛德渐变（自由 GouraudShd / 网格 LaGouraudShd）：顶点色插值渲染，优先于纯色/其它簇
            CT_GouraudShd? gshd = fillColor?.GetGouraudShd();
            CT_GouraudShd? lgshd = fillColor?.GetLaGouraudShd();
            if (gshd is not null || lgshd is not null)
            {
                DrawGouraudShdFill(canvas, skPath, lgshd ?? gshd!, resMgt, opts, effAlpha, path.Boundary);
            }
            else if (fillColor?.GetPattern() is { } fillPattern)
            {
                // 图案填充：裁剪到路径后平铺底纹单元
                DrawPatternFill(canvas, skPath, fillPattern, resMgt, effAlpha);
            }
            else
            {
                using var fp = new SKPaint
                {
                    IsAntialias = opts.AntiAlias,
                    Style = SKPaintStyle.Fill,
                };
                var fillShader = OfdColorUtil.TryCreateShader(fillColor, resMgt, path.Boundary);
                if (fillShader is not null)
                {
                    fp.Shader = fillShader;
                    fp.Color = ApplyAlpha(SKColors.White, effAlpha); // 着色器自带颜色，此处仅控整体透明度
                    canvas.DrawPath(skPath, fp);
                }
                else
                {
                    var fill = OfdColorUtil.ToSKColorOrNull(fillColor, resMgt);
                    if (fill.HasValue)
                    {
                        fp.Color = ApplyAlpha(fill.Value, effAlpha);
                        canvas.DrawPath(skPath, fp);
                    }
                }
            }
        }
    }

    // ── 高洛德渐变（GouraudShd / LaGouraudShd）────────────────

    /// <summary>
    /// 以高洛德渐变填充 <paramref name="clipPath"/>（顶点色线性插值）。
    /// <para>
    /// 对齐 GB/T 33190 §8.3.4.4 / §8.3.4.5 与 ISO 32000-1 type-4/5：
    /// <list type="bullet">
    /// <item>自由高洛德 GouraudShd：顶点流按 EdgeFlag 三角化——flag=0 该点与后续两点成新三角形；
    /// flag=1 与当前三角形 (va,vb,vc) 的 vbc 边成 (vb,vc,vd)；flag=2 与 vac 边成 (va,vc,vd)。</item>
    /// <item>网格高洛德 LaGouraudShd：顶点按行排列（每行 VerticesPerRow），相邻两行两列四边形
    /// 拆两个三角形 (p00,p01,p11)+(p00,p11,p10)。</item>
    /// </list>
    /// Extend=1 时以 BackColor 铺满网格外区域。顶点色经 Skia 自动插值（像素级平滑过渡）。
    /// 注：Java ofdrw-converter 未实现高洛德渲染，此为 NetOfd 按规范语义的增值实现。</para>
    /// </summary>
    private static void DrawGouraudShdFill(SKCanvas canvas, SKPath clipPath, CT_GouraudShd shd,
        ResourceManage resMgt, ConverterOptions opts, double effAlpha, STBox? boundary = null)
    {
        var points = shd.GetPoints();
        if (points.Count < 3) return;

        // 顶点坐标按 OFD 规范属于图元局部坐标系；渲染矩阵含 Translate(Boundary)，
        // 故需减去 Boundary 偏移，与相对化后的路径一致（否则渐变被 Boundary 重复平移）。
        double ox = boundary?.TopLeftX ?? 0d, oy = boundary?.TopLeftY ?? 0d;

        void Emit(List<SKPoint> ps, List<SKColor> cs,
            NetOfd.Core.PageDescription.Color.Point a,
            NetOfd.Core.PageDescription.Color.Point b,
            NetOfd.Core.PageDescription.Color.Point c)
        {
            ps.Add(new SKPoint((float)((a.X ?? 0) - ox), (float)((a.Y ?? 0) - oy)));
            ps.Add(new SKPoint((float)((b.X ?? 0) - ox), (float)((b.Y ?? 0) - oy)));
            ps.Add(new SKPoint((float)((c.X ?? 0) - ox), (float)((c.Y ?? 0) - oy)));
            cs.Add(ApplyAlpha(OfdColorUtil.ToSKColor(a.Color, SKColors.Black, resMgt), effAlpha));
            cs.Add(ApplyAlpha(OfdColorUtil.ToSKColor(b.Color, SKColors.Black, resMgt), effAlpha));
            cs.Add(ApplyAlpha(OfdColorUtil.ToSKColor(c.Color, SKColors.Black, resMgt), effAlpha));
        }

        var pos = new List<SKPoint>(points.Count);
        var colors = new List<SKColor>(points.Count);

        if (shd is CT_LaGouraudShd lat && lat.VerticesPerRow is int vpr && vpr >= 2)
        {
            // ── 网格高洛德：行切分 + 四边形 → 双三角 ──
            var rows = new List<NetOfd.Core.PageDescription.Color.Point[]>();
            for (int i = 0; i < points.Count; i += vpr)
                rows.Add(points.Skip(i).Take(vpr).ToArray());
            for (int r = 0; r + 1 < rows.Count; r++)
            {
                var top = rows[r];
                var bot = rows[r + 1];
                int cols = Math.Min(top.Length, bot.Length) - 1;
                for (int c = 0; c < cols; c++)
                {
                    var p00 = top[c];
                    var p01 = top[c + 1];
                    var p10 = bot[c];
                    var p11 = bot[c + 1];
                    Emit(pos, colors, p00, p01, p11);
                    Emit(pos, colors, p00, p11, p10);
                }
            }
        }
        else
        {
            // ── 自由高洛德：EdgeFlag 状态机三角化 ──
            var a = points[0];
            var b = points[1];
            var c = points[2];
            Emit(pos, colors, a, b, c);
            int i = 3;
            while (i < points.Count)
            {
                var d = points[i++];
                var flag = d.EdgeFlag ?? EdgeFlag.Flag0;
                if (flag == EdgeFlag.Flag0)
                {
                    // d 开始一个不与之前相连的新三角形：需要后续两点补全
                    if (i + 1 >= points.Count) break;
                    var e = points[i];
                    var g = points[i + 1];
                    i += 2;
                    Emit(pos, colors, d, e, g);
                    a = d; b = e; c = g;
                }
                else if (flag == EdgeFlag.Flag1)
                {
                    // 与当前三角形的 vbc 边构成新三角形 (vb, vc, vd)
                    Emit(pos, colors, b, c, d);
                    a = b; b = c; c = d;
                }
                else // Flag2
                {
                    // 与当前三角形的 vac 边构成新三角形 (va, vc, vd)
                    Emit(pos, colors, a, c, d);
                    b = c; c = d;
                }
            }
        }

        if (pos.Count == 0) return;

        canvas.Save();
        try
        {
            canvas.ClipPath(clipPath, SKClipOperation.Intersect, opts.AntiAlias);

            // Extend=1：网格外以 BackColor 铺底
            if (shd.Extend == true && shd.BackColor is not null)
            {
                using var bp = new SKPaint
                {
                    IsAntialias = opts.AntiAlias,
                    Style = SKPaintStyle.Fill,
                    Color = ApplyAlpha(OfdColorUtil.ToSKColor(shd.BackColor, SKColors.Transparent, resMgt), effAlpha),
                };
                canvas.DrawPath(clipPath, bp);
            }

            using var paint = new SKPaint
            {
                IsAntialias = opts.AntiAlias,
                Style = SKPaintStyle.Fill,
                Color = SKColors.White, // 顶点色已烘焙 effAlpha；此处保持不透明避免二次调制
            };
            canvas.DrawVertices(SKVertexMode.Triangles, pos.ToArray(), colors.ToArray(), paint);
        }
        finally
        {
            canvas.Restore();
        }
    }

    // ── 图案底纹（Pattern）──────────────────────────────────

    /// <summary>
    /// 以 <paramref name="pattern"/> 为底纹平铺填充 <paramref name="clipPath"/>。
    /// <para>
    /// 对齐 GB/T 33190 §8.3.3：单元按 XStep/YStep 排列（默认间距 = 单元宽/高），
    /// ReflectMethod 决定相邻单元镜像方式（Normal/Column/Row/RowAndColumn），
    /// RelativeTo 决定排列原点（Page=页面原点 / Object=对象包围盒左上），
    /// CTM 先排列后整体变换（当前支持平移/缩放类 CTM，旋转/错切以忽略近似）。
    /// 注：Java ofdrw-converter 的 ItextMaker.parsePattern 整段被注释（未实现），此为 NetOfd 增值实现。</para>
    /// </summary>
    private static void DrawPatternFill(SKCanvas canvas, SKPath clipPath, CT_Pattern pattern,
        ResourceManage resMgt, double effAlpha)
    {
        var cell = pattern.GetCellContent();
        var img = cell?.GetImage();
        if (img?.ResourceID is null) return;
        var multimedia = resMgt.GetMultiMedia(img.ResourceID.Value);
        if (multimedia is null) return;
        byte[]? bytes;
        try { bytes = resMgt.GetImageData(multimedia); }
        catch { return; }
        if (bytes is null || bytes.Length == 0) return;

        SKBitmap? bmp;
        try { bmp = SKBitmap.Decode(bytes); }
        catch { return; }
        if (bmp is null || bmp.Width == 0 || bmp.Height == 0) return;

        double cellW = Math.Max(pattern.Width, 1e-6);
        double cellH = Math.Max(pattern.Height, 1e-6);
        double stepX = pattern.XStep;   // getter 已做「< 单元尺寸则回退单元尺寸」
        double stepY = pattern.YStep;
        if (stepX < cellW) stepX = cellW;
        if (stepY < cellH) stepY = cellH;

        // CTM：先排列后整体变换 —— 平移并入原点、缩放并入步进/单元尺寸
        double ox = 0, oy = 0, sx = 1, sy = 1;
        if (pattern.CTM is { Count: >= 6 } ctm)
        {
            // CTM 按 [a b c d e f]：x' = a·x + c·y + e；y' = b·x + d·y + f
            // 支持无旋转/错切（b≈0 且 c≈0）的缩放+平移；其余情形忽略以保布局稳定
            double a = ctm[0], b = ctm[1], c = ctm[2], d = ctm[3], e = ctm[4], f = ctm[5];
            if (Math.Abs(b) < 1e-9 && Math.Abs(c) < 1e-9)
            {
                sx = Math.Abs(a) < 1e-9 ? 1 : a;
                sy = Math.Abs(d) < 1e-9 ? 1 : d;
                ox = e;
                oy = f;
            }
        }

        var bounds = clipPath.Bounds;
        double originX = pattern.RelativeTo == NetOfd.Core.PageDescription.Color.RelativeTo.Page ? 0 : bounds.Left * sx;
        double originY = pattern.RelativeTo == NetOfd.Core.PageDescription.Color.RelativeTo.Page ? 0 : bounds.Top * sy;
        originX += ox;
        originY += oy;

        double effW = cellW * sx, effH = cellH * sy;
        double effStepX = stepX * sx, effStepY = stepY * sy;
        if (effStepX < 1e-9 || effStepY < 1e-9) return;

        bool flipColumn = pattern.ReflectMethod is NetOfd.Core.PageDescription.Color.ReflectMethod.Column or NetOfd.Core.PageDescription.Color.ReflectMethod.RowAndColumn;
        bool flipRow = pattern.ReflectMethod is NetOfd.Core.PageDescription.Color.ReflectMethod.Row or NetOfd.Core.PageDescription.Color.ReflectMethod.RowAndColumn;

        // 覆盖裁剪包围盒（外扩一圈保证边缘完整）的网格范围
        long i0 = (long)Math.Floor((bounds.Left - originX) / effStepX) - 1;
        long i1 = (long)Math.Ceiling((bounds.Right - originX) / effStepX) + 1;
        long j0 = (long)Math.Floor((bounds.Top - originY) / effStepY) - 1;
        long j1 = (long)Math.Ceiling((bounds.Bottom - originY) / effStepY) + 1;

        using var paint = new SKPaint
        {
            IsAntialias = true,
            Color = ApplyAlpha(SKColors.White, effAlpha),
        };

        canvas.Save();
        canvas.ClipPath(clipPath, SKClipOperation.Intersect, true);
        for (long j = j0; j <= j1; j++)
        {
            double cy = originY + j * effStepY;
            bool flipY = flipRow && ((j & 1) != 0);
            for (long i = i0; i <= i1; i++)
            {
                double cx = originX + i * effStepX;
                bool flipX = flipColumn && ((i & 1) != 0);
                if (!flipX && !flipY)
                {
                    canvas.DrawBitmap(bmp, new SKRect((float)cx, (float)cy, (float)(cx + effW), (float)(cy + effH)), paint);
                }
                else
                {
                    canvas.Save();
                    canvas.Translate((float)(cx + (flipX ? effW : 0)), (float)(cy + (flipY ? effH : 0)));
                    canvas.Scale(flipX ? -1f : 1f, flipY ? -1f : 1f);
                    canvas.DrawBitmap(bmp, new SKRect(0, 0, (float)effW, (float)effH), paint);
                    canvas.Restore();
                }
            }
        }
        canvas.Restore();
    }

    // ── 图像 ────────────────────────────────────────────────
    private static void DrawImage(SKCanvas canvas, CTImage image, ResourceManage resMgt, STBox? boundary, bool hasCtm, double effAlpha = 1.0)
    {
        if (image.ResourceID is null) return;
        var multimedia = resMgt.GetMultiMedia(image.ResourceID.Value);
        if (multimedia is null) return;
        byte[]? bytes;
        try
        {
            bytes = resMgt.GetImageData(multimedia);
        }
        catch
        {
            return; // 资源读取失败（缺失/损坏）时跳过该图，不影响整页
        }
        if (bytes is null || bytes.Length == 0) return;

        SKBitmap? bmp;
        try
        {
            bmp = SKBitmap.Decode(bytes);
        }
        catch
        {
            return; // SkiaSharp 对部分畸形/不支持的图像会抛异常而非返回 null
        }
        if (bmp is null || bmp.Width == 0 || bmp.Height == 0) return;

        if (hasCtm)
        {
            // OFD 标准：图像数据先归一化到单位正方形 (0,0)-(1,1)，再由 CTM 映射到容器坐标系。
            // 当前画布矩阵已包含 CTM（Scale(PPM)·Translate(Boundary)·CTM），
            // 必须把归一化作为“最内层”变换：M = current · Scale(1/w, 1/h)。
            // 若用 canvas.Scale（等价于 Scale(1/w,1/h) · current）会把归一化放到 CTM 外层，
            // 图像位置/缩放全错。对齐 Java AWTMaker.writeImage 的 scale(1/w, 1/h) 处理。
            canvas.Save();
            var norm = SKMatrix.Concat(canvas.TotalMatrix,
                SKMatrix.CreateScale(1f / bmp.Width, 1f / bmp.Height));
            canvas.SetMatrix(norm);
            canvas.DrawBitmap(bmp, 0, 0, MakeAlphaPaint(effAlpha));
            canvas.Restore();
        }
        else
        {
            // 兜底：文件缺失 CTM（不合规）时按 Boundary 尺寸铺满，
            // 比 Java 直接画成 1×1 像素更符合预期。
            float w = boundary is not null ? (float)boundary.Width : bmp.Width;
            float h = boundary is not null ? (float)boundary.Height : bmp.Height;
            canvas.DrawBitmap(bmp, new SKRect(0, 0, w, h), MakeAlphaPaint(effAlpha));
        }
    }

    // ── 辅助 ────────────────────────────────────────────────
    private static SKMatrix ToSKMatrix(STArray ctm)
    {
        // OFD CTM 行主序: [a c e; b d f; 0 0 1]
        // SKMatrix 字段为: ScaleX, SkewX, TransX, SkewY, ScaleY, TransY, Persp0, Persp1, Persp2
        return new SKMatrix
        {
            ScaleX = (float)ctm[0],
            SkewX = (float)ctm[2],
            TransX = (float)ctm[4],
            SkewY = (float)ctm[1],
            ScaleY = (float)ctm[3],
            TransY = (float)ctm[5],
            Persp0 = 0,
            Persp1 = 0,
            Persp2 = 1,
        };
    }

    private static SKStrokeCap MapCap(string? cap) => cap?.ToLowerInvariant() switch
    {
        "round" => SKStrokeCap.Round,
        "square" => SKStrokeCap.Square,
        _ => SKStrokeCap.Butt,
    };

    private static SKStrokeJoin MapJoin(string? join) => join?.ToLowerInvariant() switch
    {
        "round" => SKStrokeJoin.Round,
        "bevel" => SKStrokeJoin.Bevel,
        _ => SKStrokeJoin.Miter,
    };

    /// <summary>将颜色的不透明度乘以有效透明度因子（0-1）。</summary>
    private static SKColor ApplyAlpha(SKColor c, double factor)
        => c.WithAlpha((byte)(c.Alpha * factor));

    /// <summary>生成带整体不透明度的绘制画笔（用于位图，White 作 tint 不改变像素色）。</summary>
    private static SKPaint MakeAlphaPaint(double factor)
        => new SKPaint { Color = SKColors.White.WithAlpha((byte)(255 * factor)) };
}
