using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Core.PageDescription.Color;
using NetOfd.Graphics2D.AWT;
using NetOfd.Pkg;
using NetOfd.Pkg.Container;

namespace NetOfd.Graphics2D;

/// <summary>
/// OFD 2D图形绘制上下文，完整实现 Graphics2D API（替代 Java OFDPageGraphics2D）
/// <p>
/// 提供类似 AWT Graphics2D 的 API 用于生成 OFD 文档
/// </p>
/// </summary>
public class OFDGraphics2D : IDisposable
{
    /// <summary>文档上下文</summary>
    public OFDGraphicsDocument Doc { get; }

    /// <summary>页面尺寸（毫米）</summary>
    public double Width { get; }
    public double Height { get; }

    /// <summary>对应页面目录（用于写 Content.xml）</summary>
    internal PageDir PageDir { get; }

    private readonly CTLayer _layer;
    private readonly STBox _boundary;

    // 绘制状态
    private OFDStroke _stroke = new();
    private object _paint = OFDColor.Black;
    private OFDFont _font = new("sanserif", OFDFont.PLAIN, 3.53);  // 10pt in mm
    private AffineTransform _ctm = new();
    private Area? _clip;
    private OFDColor _foreground = OFDColor.Black;
    private OFDColor _background = OFDColor.White;
    private AlphaComposite _composite = AlphaComposite.SrcOver;
    private RenderingHints _hints = new(null);
    private STRefID? _lastDrawParamRef;

    public OFDGraphics2D(OFDGraphicsDocument doc, PageDir pageDir, double width, double height)
    {
        Doc = doc;
        PageDir = pageDir;
        Width = width;
        Height = height;
        _boundary = new STBox(0, 0, width, height);

        // 创建图层（挂载到页面 Content）
        _layer = new CTLayer { LayerType = LayerType.Body };
        var pageObj = PageDir.ObtainContent();
        pageObj.ObtainContent().AddLayer(_layer);
        PageDir.SetContent(pageObj);
    }

    // ========== 基本绘制操作 ==========

    /// <summary>绘制图形轮廓</summary>
    public void Draw(object s)
    {
        if (s == null) return;
        var path = PathFromShape(s);
        AddPath(path, fill: false, stroke: true);
    }

    /// <summary>填充图形</summary>
    public void Fill(object s)
    {
        if (s == null) return;
        var path = PathFromShape(s);
        AddPath(path, fill: true, stroke: false, rule: null);
    }

    /// <summary>填充图形，并指定 OFD 填充规则（"Even-Odd" 或非 null 的 "NonZero"）</summary>
    public void Fill(object s, string fillingRule)
    {
        if (s == null) return;
        var path = PathFromShape(s);
        AddPath(path, fill: true, stroke: false, rule: string.IsNullOrEmpty(fillingRule) ? null : fillingRule);
    }

    /// <summary>画线</summary>
    public void DrawLine(double x1, double y1, double x2, double y2)
    {
        var p = new GeneralPath();
        p.MoveTo(x1, y1);
        p.LineTo(x2, y2);
        AddPath(p, fill: false, stroke: true);
    }

    /// <summary>描边矩形</summary>
    public void DrawRect(double x, double y, double w, double h)
    {
        var r = new Rectangle2D(x, y, w, h);
        Draw(r);
    }

    /// <summary>填充矩形</summary>
    public void FillRect(double x, double y, double w, double h)
    {
        var r = new Rectangle2D(x, y, w, h);
        Fill(r);
    }

    /// <summary>清除矩形区域</summary>
    public void ClearRect(double x, double y, double w, double h)
    {
        if (_background == null) return;
        var saved = _paint;
        _paint = _background;
        FillRect(x, y, w, h);
        _paint = saved;
    }

    /// <summary>描边圆角矩形</summary>
    public void DrawRoundRect(double x, double y, double w, double h, double arcW, double arcH)
    {
        var r = new RoundRectangle2D(x, y, w, h, arcW, arcH);
        Draw(r);
    }

    /// <summary>填充圆角矩形</summary>
    public void FillRoundRect(double x, double y, double w, double h, double arcW, double arcH)
    {
        var r = new RoundRectangle2D(x, y, w, h, arcW, arcH);
        Fill(r);
    }

    /// <summary>描边椭圆</summary>
    public void DrawOval(double x, double y, double w, double h) => Draw(new Ellipse2D(x, y, w, h));

    /// <summary>填充椭圆</summary>
    public void FillOval(double x, double y, double w, double h) => Fill(new Ellipse2D(x, y, w, h));

    /// <summary>描边圆弧</summary>
    public void DrawArc(double x, double y, double w, double h, double sa, double ext)
    {
        var a = new Arc2D(x, y, w, h, sa, ext, Arc2D.OPEN);
        Draw(a);
    }

    /// <summary>填充扇形</summary>
    public void FillArc(double x, double y, double w, double h, double sa, double ext)
    {
        var a = new Arc2D(x, y, w, h, sa, ext, Arc2D.PIE);
        Fill(a);
    }

    /// <summary>绘制折线</summary>
    public void DrawPolyline(double[] xPoints, double[] yPoints, int nPoints)
    {
        if (nPoints < 2) return;
        var p = new GeneralPath();
        p.MoveTo(xPoints[0], yPoints[0]);
        for (int i = 1; i < nPoints; i++) p.LineTo(xPoints[i], yPoints[i]);
        AddPath(p, fill: false, stroke: true);
    }

    /// <summary>绘制多边形</summary>
    public void DrawPolygon(double[] xPoints, double[] yPoints, int nPoints)
    {
        if (nPoints < 3) return;
        var p = new GeneralPath();
        p.MoveTo(xPoints[0], yPoints[0]);
        for (int i = 1; i < nPoints; i++) p.LineTo(xPoints[i], yPoints[i]);
        p.Close();
        AddPath(p, fill: false, stroke: true);
    }

    /// <summary>填充多边形</summary>
    public void FillPolygon(double[] xPoints, double[] yPoints, int nPoints)
    {
        if (nPoints < 3) return;
        var p = new GeneralPath();
        p.MoveTo(xPoints[0], yPoints[0]);
        for (int i = 1; i < nPoints; i++) p.LineTo(xPoints[i], yPoints[i]);
        p.Close();
        AddPath(p, fill: true, stroke: false);
    }

    // ========== 文字绘制 ==========

    /// <summary>绘制字符串</summary>
    public void DrawString(string str, double x, double y)
    {
        if (string.IsNullOrEmpty(str)) return;

        var textObj = new CTText
        {
            Boundary = new STBox(x, y, 100, 10),
            Size = _font.Size,
            Font = Doc.RegisterFont(_font.Name)
        };

        // 设置颜色
        textObj.TextColor = MakeColor(_paint);
        ApplyClipTo(textObj);

        // 添加文字内容
        var textCode = new TextCode { X = x, Y = y, Text = str };
        // 行内字符推进量（DeltaX）：≥2 字符即写。
        // GB/T 33190 中 DeltaX 虽为可选属性，但不度量字宽的阅读器（ofdgo 等）会把整行
        // 字符叠绘在起点——只有一个字可见。语义对齐 Java ofdrw Span#getDeltaX()：
        // 长度 = 字符数 − 1，第 i 个值 = 第 i 个字符的宽度。
        var deltaX = BuildDeltaX(str, _font.Size);
        if (deltaX is not null) textCode.DeltaX = deltaX;
        textObj.AddTextCode(textCode);

        // 应用变换
        if (!_ctm.IsIdentity)
        {
            textObj.CTM = CtmToSTArray(_ctm);
        }

        _layer.AddTextObject(textObj);
    }

    public void DrawString(string str, int x, int y) => DrawString(str, (double)x, y);

    /// <summary>
    /// 计算 TextCode 的 DeltaX（行内字符推进量）：长度 = 字符数 − 1，第 i 个值 = 第 i 个字符宽度。
    /// <para>字宽启发式：ASCII 0.6em / CJK 1.0em（与 NetOfd.Layout.DrawContext 一致）。</para>
    /// </summary>
    private static string? BuildDeltaX(string str, double fontSize)
    {
        // 单字符无推进量，按 ofdrw 语义不写
        if (string.IsNullOrEmpty(str) || str.Length < 2) return null;
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < str.Length - 1; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append((fontSize * (str[i] > 255 ? 1.0 : 0.6)).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    // ========== 图片绘制 ==========

    /// <summary>绘制图片（OFDMedia对象）</summary>
    public bool DrawImage(object img, double x, double y, double w, double h)
    {
        if (img == null) return true;

        string mediaId = img switch
        {
            string s => s,
            OFDMedia m => m.Id.ToString(),
            _ => img.ToString() ?? ""
        };

        if (string.IsNullOrEmpty(mediaId)) return true;

        var imgObj = new CTImage
        {
            Boundary = new STBox(x, y, w, h),
            ResourceID = STRefID.Parse(mediaId)
        };
        ApplyClipTo(imgObj);

        // 应用变换
        if (!_ctm.IsIdentity)
        {
            imgObj.CTM = CtmToSTArray(_ctm);
        }

        // 透明度
        if (_paint is OFDColor c && c.A < 255)
            imgObj.DrawParam = _lastDrawParamRef;

        _layer.AddImageObject(imgObj);
        return true;
    }

    // ========== 变换操作 ==========

    /// <summary>平移</summary>
    public void Translate(double tx, double ty)
    {
        _ctm.Translate(tx, ty);
        InvalidateDrawParam();
    }

    public void Translate(int x, int y) => Translate((double)x, y);

    /// <summary>旋转（弧度）</summary>
    public void Rotate(double theta)
    {
        _ctm.Rotate(theta);
        InvalidateDrawParam();
    }

    /// <summary>绕点旋转</summary>
    public void Rotate(double theta, double px, double py)
    {
        var t = AffineTransform.GetRotateInstance(theta, px, py);
        _ctm.Concatenate(t);
        InvalidateDrawParam();
    }

    /// <summary>缩放</summary>
    public void Scale(double sx, double sy)
    {
        _ctm.Scale(sx, sy);
        InvalidateDrawParam();
    }

    /// <summary>切变</summary>
    public void Shear(double shx, double shy)
    {
        var t = new AffineTransform(1, shy, shx, 1, 0, 0);
        _ctm.Concatenate(t);
        InvalidateDrawParam();
    }

    /// <summary>应用变换矩阵</summary>
    public void Transform(AffineTransform tx)
    {
        if (tx == null) return;
        _ctm.Concatenate(tx);
        InvalidateDrawParam();
    }

    /// <summary>设置变换矩阵</summary>
    public void SetTransform(AffineTransform tx) => _ctm = tx?.Clone() ?? new AffineTransform();

    /// <summary>获取当前变换矩阵</summary>
    public AffineTransform GetTransform() => _ctm.Clone();

    // ========== 状态管理 ==========

    /// <summary>获取当前颜色</summary>
    public OFDColor GetColor() => _foreground;

    /// <summary>设置颜色</summary>
    public void SetColor(OFDColor? c)
    {
        if (c == null) return;
        _foreground = c;
        _paint = c;
        InvalidateDrawParam();
    }

    /// <summary>获取当前字体</summary>
    public OFDFont GetFont() => _font;

    /// <summary>设置字体</summary>
    public void SetFont(OFDFont? font)
    {
        if (font == null) return;
        _font = font;
    }

    /// <summary>获取描边属性</summary>
    public OFDStroke GetStroke() => _stroke;

    /// <summary>设置描边属性</summary>
    public void SetStroke(OFDStroke? s)
    {
        if (s == null) return;
        _stroke = s;
        InvalidateDrawParam();
    }

    /// <summary>获取画笔</summary>
    public object GetPaint() => _paint;

    /// <summary>设置画笔/填充颜色</summary>
    public void SetPaint(object? paint)
    {
        if (paint == null) return;
        _paint = paint;
        if (paint is OFDColor c) _foreground = c;
        InvalidateDrawParam();
    }

    /// <summary>设置合成模式</summary>
    public void SetComposite(AlphaComposite? comp)
    {
        if (comp == null) return;
        _composite = comp;
    }

    /// <summary>获取合成模式</summary>
    public AlphaComposite GetComposite() => _composite;

    /// <summary>获取背景色</summary>
    public OFDColor GetBackground() => _background;

    /// <summary>设置背景色</summary>
    public void SetBackground(OFDColor? c)
    {
        if (c == null) return;
        _background = c;
    }

    // ========== 裁剪区域 ==========

    private string? _clipData;   // 设置时经 CTM 变换后的裁剪路径（页坐标）
    private STBox? _clipBox;

    /// <summary>获取裁剪区域</summary>
    public object? GetClip() => _clip;
    public void SetClip(object? s)
    {
        if (s == null) { _clip = null; _clipData = null; return; }
        _clip = new Area(s as IShape ?? new Rectangle2D());
        RefreshClipRecord();
        InvalidateDrawParam();
    }

    public void Clip(object? s)
    {
        if (s == null) { _clip = null; _clipData = null; return; }
        if (_clip == null)
            _clip = new Area(s as IShape ?? new Rectangle2D());
        else
            _clip.Intersect(new Area(s as IShape ?? new Rectangle2D()));
        RefreshClipRecord();
        InvalidateDrawParam();
    }

    public Rectangle2D GetClipBounds() => _clip?.GetBounds() ?? new Rectangle2D(0, 0, Width, Height);

    /// <summary>把当前裁剪形状按设置时的 CTM 变换并序列化为 AbbreviatedData。</summary>
    private void RefreshClipRecord()
    {
        if (_clip is null) { _clipData = null; _clipBox = null; return; }
        var p = new GeneralPath();
        var iter = ((IShape)_clip).GetPathIterator(_ctm.IsIdentity ? null : _ctm);
        double[] coords = new double[6];
        while (!iter.IsDone)
        {
            switch (iter.CurrentSegment(coords))
            {
                case 0: p.MoveTo(coords[0], coords[1]); break;
                case 1: p.LineTo(coords[0], coords[1]); break;
                case 2: p.QuadTo(coords[0], coords[1], coords[2], coords[3]); break;
                case 3: p.CurveTo(coords[0], coords[1], coords[2], coords[3], coords[4], coords[5]); break;
                case 4: p.Close(); break;
            }
            iter.Next();
        }
        var ops = p.Operations;
        _clipData = ops.Count == 0
            ? null
            : string.Join(" ", ops.Select(o => o.ToAbbreviatedData()));
        var b = p.GetBounds();
        _clipBox = b.Width <= 0 || b.Height <= 0
            ? null
            : new STBox(b.X, b.Y, b.Width, b.Height);
    }

    /// <summary>把当前裁剪记录挂到图元上（无裁剪时为 no-op）。</summary>
    private void ApplyClipTo(Core.PageDescription.CTGraphicUnit unit)
    {
        if (_clipData is null) return;
        var clip = new Core.PageDescription.CT_Clip();
        clip.AddArea(new Core.PageDescription.ClipArea { Boundary = _clipBox, AbbreviatedData = _clipData });
        unit.SetClips(new Core.PageDescription.CT_Clips().AddClip(clip));
    }

    // ========== 渲染提示 ==========

    public void SetRenderingHint(object key, object value)
    {
        _hints.Put(key, value);
        InvalidateDrawParam();
    }

    public object? GetRenderingHint(object key) => _hints.Get(key);

    public void SetRenderingHints(RenderingHints hints)
    {
        _hints.Clear();
        _hints.Add(hints);
        InvalidateDrawParam();
    }

    public void AddRenderingHints(RenderingHints hints)
    {
        _hints.Add(hints);
        InvalidateDrawParam();
    }

    public RenderingHints GetRenderingHints() => _hints;

    // ========== 状态保存/恢复 ==========

    private readonly Stack<GraphicsState> _stateStack = new();

    /// <summary>保存状态（类似 Graphics2D.create()，返回 this 以支持链式调用）</summary>
    public OFDGraphics2D Create()
    {
        var state = new GraphicsState
        {
            Stroke = _stroke,
            Paint = _paint,
            Font = _font,
            Ctm = _ctm.Clone(),
            Clip = _clip,
            Foreground = _foreground,
            Background = _background,
            Composite = _composite,
            Hints = _hints.Clone()
        };
        _stateStack.Push(state);
        return this;
    }

    /// <summary>恢复状态</summary>
    public void Dispose()
    {
        if (_stateStack.Count == 0) return;
        var state = _stateStack.Pop();
        _stroke = state.Stroke;
        _paint = state.Paint;
        _font = state.Font;
        _ctm = state.Ctm;
        _clip = state.Clip;
        RefreshClipRecord();
        _foreground = state.Foreground;
        _background = state.Background;
        _composite = state.Composite;
        _hints = state.Hints;
        InvalidateDrawParam();
    }

    /// <summary>销毁绘制上下文</summary>
    public void dispose() { /* no-op for compatibility */ }

    // ========== 命中检测 ==========

    public bool Hit(Rectangle2D rect, object s, bool onStroke)
    {
        if (s is not IShape shape) return false;
        if (onStroke) { /* 简化处理 */ }
        var transformed = _ctm.CreateTransformedShape(shape) as IShape ?? shape;
        return transformed.Intersects(rect.X, rect.Y, rect.Width, rect.Height);
    }

    // ========== 内部方法 ==========

    private void AddPath(GeneralPath path, bool fill, bool stroke)
        => AddPath(path, fill, stroke, rule: null);

    private void AddPath(GeneralPath path, bool fill, bool stroke, string? rule)
    {
        var pathObj = new CTPath
        {
            Boundary = _boundary
        };

        // 转换路径数据
        var sb = new StringBuilder();
        foreach (var op in path.Operations)
            sb.Append(op.ToAbbreviatedData()).Append(' ');
        pathObj.AbbreviatedData = sb.ToString().TrimEnd();

        pathObj.Fill = fill;
        pathObj.Stroke = stroke;
        if (fill && rule == "Even-Odd")
            pathObj.Rule = "Even-Odd"; // 默认 NonZero，仅显式写出非默认值
        ApplyClipTo(pathObj);

        var color = MakeColor(_paint);
        if (color is not null)
        {
            if (fill && pathObj.FillColor is null) pathObj.FillColor = color;
            if (stroke && pathObj.StrokeColor is null) pathObj.StrokeColor = color;
        }

        // 记录描边状态：线宽/虚线/线端样式/线接样式（此前被静默丢弃，一律按默认 0.353 渲染）
        if (stroke)
        {
            pathObj.LineWidth = _stroke.LineWidth;
            if (_stroke.DashArray is { Length: >= 2 } dashArr)
                pathObj.Dash = new NetOfd.Core.BasicType.STArray(dashArr.Select(x => (double)x));
            if (_stroke.DashPhase != 0f)
                pathObj.DashOffset = _stroke.DashPhase;
            // 仅显式写出与 OFD 默认（Butt/Miter）不同的线端/线接，保持常规输出简洁。
            var cap = _stroke.EndCap switch
            {
                OFDStroke.CAP_ROUND => "Round",
                OFDStroke.CAP_SQUARE => "Square",
                _ => "Butt"
            };
            var join = _stroke.LineJoin switch
            {
                OFDStroke.JOIN_ROUND => "Round",
                OFDStroke.JOIN_BEVEL => "Bevel",
                _ => "Miter"
            };
            if (cap != "Butt") pathObj.Cap = cap;
            if (join != "Miter") pathObj.Join = join;
        }

        // 记录合成透明度（AlphaComposite 的 alpha → 图元 Alpha）
        if (_composite.Rule == AlphaComposite.SRC_OVER && _composite.AlphaValue < 1.0f)
            pathObj.Alpha = Math.Round(_composite.AlphaValue * 255.0);

        // 应用变换
        if (!_ctm.IsIdentity)
        {
            pathObj.CTM = CtmToSTArray(_ctm);
        }

        _layer.AddPathObject(pathObj);
    }

    private GeneralPath PathFromShape(object shape)
    {
        var path = new GeneralPath();
        IShape? s = shape as IShape;
        if (s == null) return path;
        var iter = s.GetPathIterator(null);
        double[] coords = new double[6];

        while (!iter.IsDone)
        {
            int segType = iter.CurrentSegment(coords);
            switch (segType)
            {
                case 0: path.MoveTo(coords[0], coords[1]); break; // MOVETO
                case 1: path.LineTo(coords[0], coords[1]); break; // LINETO
                case 2: path.QuadTo(coords[0], coords[1], coords[2], coords[3]); break; // QUAD
                case 3: path.CurveTo(coords[0], coords[1], coords[2], coords[3], coords[4], coords[5]); break; // CUBIC
                case 4: path.Close(); break; // CLOSE
            }
            iter.Next();
        }
        return path;
    }

private CT_Color? MakeColor(object? paint)
    {
        if (paint == null) return null;
        if (paint is OFDColor c)
            return new CT_Color { Value = c.ToRgbString() };

        // ���仭ˢ��OFD ���䣨Shading���淶��δ֧�֣�
        // ���е��ֵɫ����д�룬���⽥����侲Ĭ��ʧΪ���ɼ�
        if (paint is OFDLinearGradientPaint lg)
            return new CT_Color { Value = lg.GetColor(0.5).ToRgbString() };
        if (paint is OFDRadialGradientPaint rg)
            return new CT_Color { Value = rg.GetColor(0.5).ToRgbString() };
        if (paint is OFDGradientPaint gp)
            return new CT_Color { Value = gp.GetColor(0.5).ToRgbString() };

        return null;
    }

    /// <summary>
    /// ��޸�OFDLinearGradientPaint��CT_AxialShd
    /// </summary>
    private CT_AxialShd ConvertLinearGradientToShading(OFDLinearGradientPaint lg)
    {
        var shd = new CT_AxialShd(lg.StartPoint.X, lg.StartPoint.Y, lg.EndPoint.X, lg.EndPoint.Y);
        shd.MapType = MapType.Direct;
        shd.Extend = Extend.Both;

        // Add segments from colors and fractions
        for (int i = 0; i < lg.Colors.Length; i++)
        {
            float fraction = i < lg.Fractions.Length ? lg.Fractions[i] : (i > 0 ? lg.Fractions[i - 1] : 0);
            shd.AddSegment(fraction, new CT_Color { Value = lg.Colors[i].ToRgbString() });
        }

        // Ensure we have at least 2 segments
        if (shd.GetSegments().Count < 2 && lg.Colors.Length >= 2)
        {
            shd.AddSegment(0, new CT_Color { Value = lg.Colors[0].ToRgbString() });
            shd.AddSegment(1, new CT_Color { Value = lg.Colors[lg.Colors.Length - 1].ToRgbString() });
        }

        return shd;
    }

    /// <summary>
    /// ��޸�OFDRadialGradientPaint��CT_RadialShd
    /// </summary>
    private CT_RadialShd ConvertRadialGradientToShading(OFDRadialGradientPaint rg)
    {
        var shd = new CT_RadialShd
        {
            StartPoint = new NetOfd.Core.BasicType.STPos(rg.CenterPoint.X, rg.CenterPoint.Y),
            EndPoint = new NetOfd.Core.BasicType.STPos(rg.CenterPoint.X, rg.CenterPoint.Y),
            StartRadius = 0,
        };

        // Set eccentricity and angle if needed
        if (rg.Radius > 0)
        {
            // Calculate eccentricity based on focus point offset
            double dx = rg.FocusPoint.X - rg.CenterPoint.X;
            double dy = rg.FocusPoint.Y - rg.CenterPoint.Y;
            shd.Eccentricity = Math.Sqrt(dx * dx + dy * dy) / rg.Radius;
            shd.Angle = Math.Atan2(dy, dx) * 180 / Math.PI;
        }

        shd.EndRadius = (float)rg.Radius;

        // Add segments from colors and fractions
        for (int i = 0; i < rg.Colors.Length; i++)
        {
            float fraction = i < rg.Fractions.Length ? rg.Fractions[i] : (i > 0 ? rg.Fractions[i - 1] : 0);
            shd.AddSegment(fraction, new CT_Color { Value = rg.Colors[i].ToRgbString() });
        }

        // Ensure we have at least 2 segments
        if (shd.GetSegments().Count < 2 && rg.Colors.Length >= 2)
        {
            shd.AddSegment(0, new CT_Color { Value = rg.Colors[0].ToRgbString() });
            shd.AddSegment(1, new CT_Color { Value = rg.Colors[rg.Colors.Length - 1].ToRgbString() });
        }

        return shd;
    }

    private void InvalidateDrawParam() => _lastDrawParamRef = null;

    internal CTLayer GetLayer() => _layer;

    /// <summary>获取变换矩阵的 OFD 表示</summary>
    private NetOfd.Core.BasicType.STArray ToSTArray(AffineTransform tx)
    {
        var m = tx.GetMatrix();
        var arr = new NetOfd.Core.BasicType.STArray();
        arr.Add(m[0]); arr.Add(m[1]); arr.Add(m[2]);
        arr.Add(m[3]); arr.Add(m[4]); arr.Add(m[5]);
        return arr;
    }

    /// <summary>从 AffineTransform 创建 STArray</summary>
    internal static NetOfd.Core.BasicType.STArray CtmToSTArray(AffineTransform tx)
    {
        var m = tx.GetMatrix();
        var arr = new NetOfd.Core.BasicType.STArray();
        arr.Add(m[0]); arr.Add(m[1]); arr.Add(m[2]);
        arr.Add(m[3]); arr.Add(m[4]); arr.Add(m[5]);
        return arr;
    }
}

/// <summary>
/// 图形状态快照
/// </summary>
internal class GraphicsState
{
    public OFDStroke Stroke { get; set; } = new();
    public object Paint { get; set; } = OFDColor.Black;
    public OFDFont Font { get; set; } = new("sanserif", 0, 12);
    public AffineTransform Ctm { get; set; } = new();
    public Area? Clip { get; set; }
    public OFDColor Foreground { get; set; } = OFDColor.Black;
    public OFDColor Background { get; set; } = OFDColor.White;
    public AlphaComposite Composite { get; set; } = AlphaComposite.SrcOver;
    public RenderingHints Hints { get; set; } = new(null);
}