using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Core.PageDescription.DrawParam;
using NetOfd.Layout.Engine;

namespace NetOfd.Layout.Element.Canvas;

/// <summary>
/// 画布绘制上下文
/// <p>提供类似 HTML5 Canvas 的 API 用于绘制 OFD 内容</p>
/// 对应 Java: org.ofdrw.layout.element.canvas.DrawContext
/// </summary>
public class DrawContext
{
    /// <summary>
    /// 恒等变换矩阵
    /// </summary>
    internal static readonly double[] Identity = { 1, 0, 0, 1, 0, 0 };

    private IGraphicContainer _container;
    private readonly Func<long> _nextId;
    internal IResourceProvider _resManager;
    private CanvasState _state = new();
    private readonly Stack<CanvasState> _stack = new();
    public double PPM { get; set; } = 3.78;

    /// <summary>当前写入的图层容器（替换旧反射取 <c>_container</c> 的写法）</summary>
    internal IGraphicContainer TargetContainer => _container;

    /// <summary>填充颜色</summary>
    public object? FillStyle { get; set; } = "#000000";

    /// <summary>描边颜色</summary>
    public object? StrokeStyle { get; set; } = "#000000";

    /// <summary>线宽（mm）</summary>
    public double LineWidth { get; set; } = 0.353;

    /// <summary>线帽样式</summary>
    public LineCapType LineCap { get; set; } = LineCapType.Butt;

    /// <summary>线连接样式</summary>
    public LineJoinType LineJoin { get; set; } = LineJoinType.Miter;

    /// <summary>透明度</summary>
    public double? GlobalAlpha { get; set; }

    /// <summary>虚线模式（mm），null 表示实线</summary>
    public double[]? LineDash { get; set; }

    /// <summary>虚线偏移量（mm）：虚线重复样式开始处的位移，0 表示不设置（DashOffset 省略）</summary>
    public double LineDashOffset { get; set; }

    /// <summary>字体（CSS格式）</summary>
    public string Font
    {
        get => _font;
        set { _font = value; ParseFont(value); }
    }
    private string _font = "1mm SimSun";

    private PathBuilder? _currentPath;

    /// <summary>当前变换矩阵（CTM）</summary>
    public double[] CurrentTransform => (double[])_state.Ctm!.Clone();

    public DrawContext(IGraphicContainer container, STBox boundary, Func<long> nextId, IResourceProvider resManager)
    {
        _container = container;
        _nextId = nextId;
        _resManager = resManager;
        FillStyle = "#000000";
        StrokeStyle = "#000000";
    }

    // ── 路径 API ─────────────────────────────────────────────
    public DrawContext BeginPath() { _currentPath = new PathBuilder(); return this; }
    public DrawContext ClosePath() { _currentPath?.Close(); return this; }
    public DrawContext MoveTo(double x, double y) { (_currentPath ??= new()).MoveTo(x, y); return this; }
    public DrawContext LineTo(double x, double y) { _currentPath?.LineTo(x, y); return this; }
    public DrawContext QuadraticCurveTo(double cpx, double cpy, double x, double y) { (_currentPath ??= new()).QuadraticBezier(cpx, cpy, x, y); return this; }
    public DrawContext BezierCurveTo(double cp1x, double cp1y, double cp2x, double cp2y, double x, double y) { (_currentPath ??= new()).CubicBezier(cp1x, cp1y, cp2x, cp2y, x, y); return this; }
    public DrawContext Arc(double rx, double ry, double angle, bool largeArc, bool sweep, double x, double y) { (_currentPath ??= new()).Arc(rx, ry, angle, largeArc, sweep, x, y); return this; }

    public DrawContext Rect(double x, double y, double w, double h) =>
        MoveTo(x, y).LineTo(x + w, y).LineTo(x + w, y + h).LineTo(x, y + h).ClosePath();

    public DrawContext Fill() { if (_currentPath == null) return this; AddPath(FillStyle, isStroke: false); _currentPath = null; return this; }
    public DrawContext Stroke() { if (_currentPath == null) return this; AddPath(StrokeStyle, isStroke: true); _currentPath = null; return this; }
    public DrawContext FillAndStroke() { if (_currentPath == null) return this; AddPath(FillStyle, false); AddPath(StrokeStyle, true); _currentPath = null; return this; }
    public DrawContext FillRect(double x, double y, double w, double h) { Rect(x, y, w, h); return Fill(); }
    public DrawContext StrokeRect(double x, double y, double w, double h) { Rect(x, y, w, h); return Stroke(); }

    // ── 状态管理 ─────────────────────────────────────────────
    private readonly Stack<(string? Data, STBox? Box)> _clipStack = new();
    private string? _clipData;
    private STBox? _clipBox;

    public DrawContext Save()
    {
        _clipStack.Push((_clipData, _clipBox));
        _stack.Push(new CanvasState
        {
            FillStyle = FillStyle,
            StrokeStyle = StrokeStyle,
            FontSetting = _state.FontSetting,
            GlobalAlpha = GlobalAlpha,
            Ctm = _state.Ctm != null ? (double[])_state.Ctm.Clone() : null,
            LineDash = LineDash != null ? (double[])LineDash.Clone() : null
        });
        return this;
    }

    public DrawContext Restore()
    {
        if (_stack.Count == 0) return this;
        var prev = _stack.Pop();
        FillStyle = prev.FillStyle;
        StrokeStyle = prev.StrokeStyle;
        _state.FontSetting = prev.FontSetting;
        GlobalAlpha = prev.GlobalAlpha;
        _state.Ctm = prev.Ctm;
        LineDash = prev.LineDash;
        LineDashOffset = prev.LineDashOffset;
        (_clipData, _clipBox) = _clipStack.Pop();
        return this;
    }

    // ── 裁剪 ─────────────────────────────────────────────────
    /// <summary>
    /// 矩形裁剪：其后的路径/文字/图像仅在区域内可见（坐标为页绝对 mm，随当前 CTM 变换）。
    /// </summary>
    public DrawContext ClipRect(double x, double y, double w, double h)
    {
        double[][] corners =
        {
            new[] { x, y }, new[] { x + w, y }, new[] { x + w, y + h }, new[] { x, y + h },
        };
        var ctm = _state.Ctm;
        var pts = corners.Select(p => ctm is null
            ? (p[0], p[1])
            : (ctm[0] * p[0] + ctm[2] * p[1] + ctm[4],
               ctm[1] * p[0] + ctm[3] * p[1] + ctm[5])).ToList();

        double minX = pts.Min(p => p.Item1), minY = pts.Min(p => p.Item2);
        double maxX = pts.Max(p => p.Item1), maxY = pts.Max(p => p.Item2);
        _clipBox = new STBox(minX, minY, maxX - minX, maxY - minY);

        var pb = new PathBuilder();
        pb.MoveTo(pts[0].Item1, pts[0].Item2);
        for (int i = 1; i < 4; i++) pb.LineTo(pts[i].Item1, pts[i].Item2);
        pb.Close();
        _clipData = string.Join(" ", pb.Operations.Select(o => o.ToAbbreviatedData()));
        return this;
    }

    /// <summary>移除裁剪。</summary>
    public DrawContext ClearClip()
    {
        _clipData = null;
        _clipBox = null;
        return this;
    }

    private void ApplyClipTo(CTGraphicUnit unit)
    {
        if (_clipData is null) return;
        var clip = new CT_Clip();
        clip.AddArea(new ClipArea { Boundary = _clipBox, AbbreviatedData = _clipData });
        unit.SetClips(new CT_Clips().AddClip(clip));
    }

    // ── 变换 ─────────────────────────────────────────────────
    public DrawContext Transform(double a, double b, double c, double d, double e, double f)
    {
        _state.Ctm ??= (double[])Identity.Clone();
        _state.Ctm = MultiplyCtm(new[] { a, b, c, d, e, f }, _state.Ctm);
        return this;
    }

    public DrawContext SetTransform(double a, double b, double c, double d, double e, double f)
    {
        _state.Ctm = new[] { a, b, c, d, e, f };
        return this;
    }

    public DrawContext Translate(double tx, double ty) => Transform(1, 0, 0, 1, tx, ty);
    public DrawContext Rotate(double rad)
    {
        var c = Math.Cos(rad);
        var s = Math.Sin(rad);
        return Transform(c, s, -s, c, 0, 0);
    }
    public DrawContext Scale(double sx, double sy) => Transform(sx, 0, 0, sy, 0, 0);

    /// <summary>旋转角度（度）</summary>
    public DrawContext RotateDeg(double angleDeg) => Rotate(angleDeg * Math.PI / 180d);

    /// <summary>两个 6 元素 CTM 相乘：A * B</summary>
    private static double[] MultiplyCtm(double[] a, double[] b)
    {
        return new[]
        {
            a[0] * b[0] + a[2] * b[1],
            a[1] * b[0] + a[3] * b[1],
            a[0] * b[2] + a[2] * b[3],
            a[1] * b[2] + a[3] * b[3],
            a[0] * b[4] + a[2] * b[5] + a[4],
            a[1] * b[4] + a[3] * b[5] + a[5],
        };
    }

    /// <summary>将点 (x,y) 用当前 CTM 变换</summary>
    private (double X, double Y) ApplyCtm(double x, double y)
    {
        var ctm = _state.Ctm ?? Identity;
        return (ctm[0] * x + ctm[2] * y + ctm[4],
                ctm[1] * x + ctm[3] * y + ctm[5]);
    }

    // ── 文字 ─────────────────────────────────────────────────
    public DrawContext FillText(string text, double x, double y) => AddTextObject(text, x, y, FillStyle, StrokeStyle, doFill: true, doStroke: false);
    public DrawContext StrokeText(string text, double x, double y) => AddTextObject(text, x, y, FillStyle, StrokeStyle, doFill: false, doStroke: true);

    /// <summary>测量文字宽度（mm）</summary>
    public TextMetrics MeasureText(string text)
    {
        var fs = _state.FontSetting ?? new FontSetting(12, "SimSun");
        var tm = new TextMetrics(MeasureTextWidth(text, fs.Size), fs.Size)
        {
            Ascent = fs.Size,
            Descent = 0
        };
        return tm;
    }

    private static double MeasureTextWidth(string text, double fontSize)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        double width = 0;
        foreach (char c in text)
            width += CharWidth(c, fontSize);
        return width;
    }

    /// <summary>
    /// 单字符宽度（mm）。启发式：ASCII 0.6em / CJK 1.0em
    /// （与 <see cref="MeasureTextWidth"/> 及 CellContentDrawer.CharWidth 保持一致）。
    /// </summary>
    private static double CharWidth(char c, double fontSize) => fontSize * (c > 255 ? 1.0 : 0.6);

    /// <summary>
    /// 计算 TextCode 的 <c>DeltaX</c>（行内字符推进量）。
    /// <para>
    /// 语义对齐 GB/T 33190 / Java ofdrw <c>Span#getDeltaX()</c>：
    /// 长度为 <b>字符数 − 1</b>，第 <c>i</c> 个值 = 第 <c>i</c> 个字符的宽度
    /// （即"后一字符相对前一字符在阅读方向上的偏移量"）。
    /// </para>
    /// <para>
    /// <b>必须写</b>：GB/T 33190 中 DeltaX 虽为可选属性，但并非所有阅读器都实现字体度量——
    /// 缺少 DeltaX 时，ofdgo 等阅读器会把整行文字的所有字符叠绘在起点（每格只显示一个字）。
    /// Java ofdrw 的 <c>DrawContext.drawText</c> 亦为无条件写入。
    /// </para>
    /// </summary>
    private static string? BuildDeltaX(string text, double fontSize)
    {
        // 单字符无推进量，按 ofdrw 语义不写（DeltaX 数组为空）。
        if (string.IsNullOrEmpty(text) || text.Length < 2) return null;
        var sb = new StringBuilder();
        for (int i = 0; i < text.Length - 1; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(STBase.Fmt(CharWidth(text[i], fontSize)));
        }
        return sb.ToString();
    }

    private DrawContext AddTextObject(string text, double x, double y, object? fillColor, object? strokeColor, bool doFill, bool doStroke)
    {
        if (string.IsNullOrEmpty(text)) return this;
        var fontSize = _state.FontSetting?.Size ?? 12;
        // OFD Size 单位就是 mm（与 Java 版一致）
        double sizeInMm = fontSize;

        // 根据字体大小计算文字包围盒
        double textWidth = MeasureTextWidth(text, fontSize);
        var textHeight = fontSize * 1.2;

        var textObj = new CTText
        {
            Boundary = new STBox(x, y, Math.Max(textWidth, 1), Math.Max(textHeight, 1))
        };
        textObj.TextColor = ParseColor(fillColor);
        var fontId = _resManager.GetFontId(_state.FontSetting?.Family ?? "SimSun");
        textObj.Font = STRefID.Parse(fontId);
        textObj.Size = sizeInMm;
        if (_state.FontSetting is { Bold: true }) textObj.Bold = true;
        if (_state.FontSetting is { Italic: true }) textObj.Italic = true;
        if (doStroke)
        {
            // 描边/轮廓字：仅描边不填充，描边色 = StrokeStyle，线宽 = 当前 LineWidth（渲染端缺省回退 0.35mm）
            textObj.Fill = false;
            textObj.Stroke = true;
            textObj.StrokeColor = ParseColor(strokeColor);
            if (LineWidth > 0) textObj.LineWidth = LineWidth;
        }
        // 纯填充（FillText）：保持 OFD 默认 Fill=true / Stroke=false，不显式写 attr（既有产物不变）
        // TextCode X/Y 相对于 Boundary（OFD 规范）
        var textCode = new TextCode { X = 0, Y = fontSize, Text = text };
        // 行内字符推进量：≥2 字符即写（对齐 ofdrw DrawContext 无条件写 DeltaX 的行为）
        var deltaX = BuildDeltaX(text, fontSize);
        if (deltaX is not null) textCode.DeltaX = deltaX;
        textObj.AddTextCode(textCode);

        ApplyCommon(textObj, x, y);
        _container.AddTextObject(textObj);
        return this;
    }

    // ── 图像 ─────────────────────────────────────────────────
    public DrawContext DrawImage(string mediaId, double x, double y, double w, double h) { AddImageObject(mediaId, x, y, w, h); return this; }
    public DrawContext DrawImageFromFile(string filePath, double x, double y, double w, double h) => DrawImage(_resManager.AddMedia(filePath), x, y, w, h);

    private void AddImageObject(string mediaId, double x, double y, double w, double h)
    {
        var img = new CTImage
        {
            Boundary = new STBox(x, y, w, h),
            ResourceID = STRefID.Parse(mediaId)
        };
        ApplyCommon(img, x, y);
        _container.AddImageObject(img);
    }

    // ── 路径绘制 ─────────────────────────────────────────────
    private void AddPath(object? color, bool isStroke)
    {
        if (_currentPath == null) return;
        var pathObj = new CTPath();

        // 应用变换矩阵到路径坐标
        var ops = _currentPath.Operations;
        if (_state.Ctm is not null)
        {
            _currentPath = _currentPath.Transformed(_state.Ctm);
            ops = _currentPath.Operations;
        }

        // 计算路径外接矩形（Boundary）并输出相对坐标。
        // 依据 GB/T 33190：PathObject 必须带 Boundary（否则严格阅读器如 ofdreader.net
        // 将裁剪框视为零尺寸而丢弃图形）；坐标相对 Boundary 左上角。
        var (boundary, data) = BuildBoundaryAndAbbrevData(ops, isStroke ? LineWidth : 0d);
        pathObj.Boundary = boundary;
        pathObj.AbbreviatedData = data;

        var c = ParseColor(color);
        if (isStroke)
        {
            pathObj.Stroke = true;
            pathObj.LineWidth = LineWidth;
            pathObj.StrokeColor = c;
            if (LineCap != LineCapType.Butt) pathObj.Cap = LineCap.ToString();
            if (LineJoin != LineJoinType.Miter) pathObj.Join = LineJoin.ToString();
            if (LineDash is { Length: > 0 })
            {
                pathObj.DashPattern = new STArray(LineDash);
                if (LineDashOffset > 0)
                    pathObj.DashOffset = LineDashOffset;
            }
        }
        else
        {
            pathObj.Fill = true;
            pathObj.FillColor = c;
        }
        ApplyClipTo(pathObj);
        ApplyCommon(pathObj, 0, 0);
        _container.AddPathObject(pathObj);
    }

    /// <summary>
    /// 依据路径操作计算外接矩形（Boundary）并输出相对该矩形左上角的 AbbreviatedData。
    /// <para>
    /// 采用 OFD 规范且被主流阅读器（含 ofdgo、HCView 导出）一致接受的写法：
    /// <b>Boundary 原点 = 路径真实绝对位置（minX,minY，描边外扩半个线宽），数据相对 Boundary 左上角。</b>
    /// 这样无论阅读器按 parent + Boundary + 相对坐标合成，还是直接把数据当绝对坐标，
    /// 落点都正确；同时 Boundary 紧贴图形（仅外扩半线宽），避免某些 Web 阅读器对超大
    /// Boundary 包络（如 (0,0,190,112)）误裁左/上边线或做空间裁剪。
    /// </para>
    /// <para>netofd 自有渲染器以 <c>Translate(Boundary)</c> 处理坐标，配合本处相对化数据，视觉与绝对坐标写法完全一致。</para>
    /// </summary>
    private static (STBox Boundary, string Data) BuildBoundaryAndAbbrevData(
        IReadOnlyList<PathOp> ops, double lineWidth)
    {
        double minX = double.MaxValue, minY = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue;
        bool any = false;
        foreach (var op in ops)
            foreach (var (x, y) in PointCoords(op))
            {
                any = true;
                if (x < minX) minX = x;
                if (y < minY) minY = y;
                if (x > maxX) maxX = x;
                if (y > maxY) maxY = y;
            }
        if (!any) return (new STBox(0, 0, 1, 1), string.Empty);

        // 描边外扩半个线宽，确保整条笔触都落在 Boundary 内（避免任何阅读器按 Boundary 裁剪时吃掉半边）。
        // 同时加极小 epsilon，避免退化（点/水平/竖直）路径出现零尺寸 Boundary。
        double pad = lineWidth / 2d + 1e-6;
        double bx = minX - pad;
        double by = minY - pad;
        double bw = Math.Max(maxX - minX + 2 * pad, 0.01);
        double bh = Math.Max(maxY - minY + 2 * pad, 0.01);
        var b = new STBox(bx, by, bw, bh);

        // 数据相对化：原坐标 - (Boundary 原点)，使渲染器 Translate(Boundary) 后落回真实绝对位置。
        var sb = new StringBuilder();
        foreach (var op in ops)
            sb.Append(OffsetOp(op, -bx, -by)).Append(' ');
        return (b, sb.ToString().TrimEnd());
    }

    private static IEnumerable<(double X, double Y)> PointCoords(PathOp op)
    {
        var a = op.Args;
        switch (op.Type)
        {
            case OpType.M:
            case OpType.L:
                yield return (a[0], a[1]);
                break;
            case OpType.Q:
                yield return (a[0], a[1]);
                yield return (a[2], a[3]);
                break;
            case OpType.C:
                yield return (a[0], a[1]);
                yield return (a[2], a[3]);
                yield return (a[4], a[5]);
                break;
            case OpType.A:
                yield return (a[5], a[6]);
                break;
            case OpType.Z:
                break;
        }
    }

    private static string OffsetOp(PathOp op, double dx, double dy)
    {
        var a = op.Args;
        switch (op.Type)
        {
            case OpType.M: return $"M {STBase.Fmt(a[0] + dx)} {STBase.Fmt(a[1] + dy)}";
            case OpType.L: return $"L {STBase.Fmt(a[0] + dx)} {STBase.Fmt(a[1] + dy)}";
            case OpType.Q: return $"Q {STBase.Fmt(a[0] + dx)} {STBase.Fmt(a[1] + dy)} {STBase.Fmt(a[2] + dx)} {STBase.Fmt(a[3] + dy)}";
            // GB/T 33190 图形轮廓数据：三次贝塞尔命令为 B（6 参数），闭合命令为 C（0 参数）。
            // 曾误用 SVG 风格的 C（三次贝塞尔）与 Z（闭合）——ofdgo 等严格阅读器直接报
            // “invalid path command "Z"”，导致矩形左/上边这类「靠闭合段收尾」的边线整段丢失。
            case OpType.C: return $"B {STBase.Fmt(a[0] + dx)} {STBase.Fmt(a[1] + dy)} {STBase.Fmt(a[2] + dx)} {STBase.Fmt(a[3] + dy)} {STBase.Fmt(a[4] + dx)} {STBase.Fmt(a[5] + dy)}";
            case OpType.A: return $"A {STBase.Fmt(a[0])} {STBase.Fmt(a[1])} {STBase.Fmt(a[2])} {a[3]:F0} {a[4]:F0} {STBase.Fmt(a[5] + dx)} {STBase.Fmt(a[6] + dy)}";
            case OpType.Z: return "C";
            default: return string.Empty;
        }
    }

    // ── 通用属性应用（透明度 / CTM）──────────────────────────
    private void ApplyCommon(CTGraphicUnit unit, double drawX, double drawY)
    {
        // 透明度
        if (GlobalAlpha is double alpha && alpha < 1.0)
        {
            unit.Alpha = alpha * 255;
        }
        // 变换矩阵
        if (_state.Ctm is not null && !IsIdentity(_state.Ctm))
        {
            unit.CTM = new STArray(_state.Ctm);
        }
    }

    private static bool IsIdentity(double[] ctm)
    {
        return Math.Abs(ctm[0] - 1) < 1e-9 && Math.Abs(ctm[1]) < 1e-9
            && Math.Abs(ctm[2]) < 1e-9 && Math.Abs(ctm[3] - 1) < 1e-9
            && Math.Abs(ctm[4]) < 1e-9 && Math.Abs(ctm[5]) < 1e-9;
    }

    // ── 字体 ─────────────────────────────────────────────────
    private void ParseFont(string fontDesc)
    {
        if (string.IsNullOrEmpty(fontDesc)) return;
        var parts = fontDesc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var fs = new FontSetting(12, "SimSun");
        foreach (var part in parts)
        {
            var lower = part.ToLowerInvariant();
            if (lower is "italic" or "oblique") fs.Italic = true;
            else if (lower is "bold" or "bolder") fs.Bold = true;
            else if (part.EndsWith("pt") && double.TryParse(part[..^2], out var pt)) fs.Size = pt * 0.353;
            else if (part.EndsWith("px") && double.TryParse(part[..^2], out var px)) fs.Size = px / PPM;
            else if (part.EndsWith("mm") && double.TryParse(part[..^2], out var mm)) fs.Size = mm;
            else if (double.TryParse(part, out var num)) fs.Size = num;
            else fs.Family = part;
        }
        _state.FontSetting = fs;
    }

    /// <summary>注册外部字体文件（默认字族名），返回字体 ID。</summary>
    public string AddFont(string familyName, string fontFilePath)
    {
        return _resManager.AddFont(familyName, fontFilePath);
    }

    // ── 渐变（对齐 HTML Canvas API）─────────────────────────

    /// <summary>
    /// 创建线性（轴向）渐变，可赋值给 <see cref="FillStyle"/> 或 <see cref="StrokeStyle"/>。
    /// 对应 Java: DrawContext.createLinearGradient
    /// </summary>
    public CanvasGradient CreateLinearGradient(double x0, double y0, double x1, double y1)
        => new(x0, y0, x1, y1);

    /// <summary>
    /// 创建放射状（径向）渐变，可赋值给 <see cref="FillStyle"/> 或 <see cref="StrokeStyle"/>。
    /// 对应 Java: DrawContext.createRadialGradient
    /// </summary>
    public CanvasRadialGradient CreateRadialGradient(double x0, double y0, double r0,
                                                    double x1, double y1, double r1)
        => new(x0, y0, r0, x1, y1, r1);

    // ── 图案底纹（对齐 Java CanvasPattern）──────────────────

    /// <summary>
    /// 创建以图片为单元的重复底纹，可赋值给 <see cref="FillStyle"/> 或 <see cref="StrokeStyle"/>。
    /// <para>
    /// 图片像素尺寸按 Java ofdrw 的 <see cref="CanvasPattern.Ppm"/>（3.78 px/mm）换算为毫米；
    /// 可用返回对象的 <see cref="CanvasPattern.SetImageSize"/> 覆盖。
    /// </para>
    /// 对应 Java: DrawContext.createPattern(Path, String)
    /// </summary>
    /// <param name="imgFilePath">底纹图片路径。</param>
    /// <param name="repetition">重复样式：repeat（默认）/ column / row / row-column。</param>
    public CanvasPattern CreatePattern(string imgFilePath, string repetition = "repeat")
    {
        var fullPath = System.IO.Path.GetFullPath(imgFilePath);
        if (!System.IO.File.Exists(fullPath))
            throw new System.IO.FileNotFoundException("底纹图片不存在：" + fullPath);

        var mediaId = _resManager.AddMedia(fullPath);

        // 图片像素尺寸 → 毫米（Java: mm(px) = px / PPM）
        var px = ImageSizeProbe.TryGetPixelSize(fullPath);
        if (px is null)
            throw new InvalidOperationException("无法识别的底纹图片格式（仅支持 PNG/JPEG/BMP/GIF）：" + fullPath);
        double w = px.Value.Width / CanvasPattern.Ppm;
        double h = px.Value.Height / CanvasPattern.Ppm;

        var imgObj = new NetOfd.Core.PageDescription.CTImage
        {
            Boundary = new STBox(0, 0, w, h),
            ResourceID = STRefID.Parse(mediaId),
            CTM = new STArray([w, 0, 0, h, 0, 0])
        };

        return new CanvasPattern(fullPath, repetition, imgObj);
    }

    // ── 颜色解析 ─────────────────────────────────────────────

    /// <summary>
    /// 将画笔样式解析为 OFD 颜色。
    /// <para>
    /// 支持：<see cref="NetOfd.Core.PageDescription.Color.CT_Color"/> 实例、CSS 颜色字符串（#RGB / #RRGGBB / #RRGGBBAA /
    /// rgb() / rgba() / 143 种颜色名）、<see cref="CanvasGradient"/>、<see cref="CanvasRadialGradient"/>。
    /// </para>
    /// 对应 Java: DrawContext.detectColor
    /// </summary>
    private NetOfd.Core.PageDescription.Color.CT_Color? ParseColor(object? color)
    {
        if (color == null) return null;
        // 已有 CT_Color 对象（可能已挂接底纹）
        if (color is NetOfd.Core.PageDescription.Color.CT_Color ctColor) return ctColor;
        // 线性渐变
        if (color is CanvasGradient linear)
            return new NetOfd.Core.PageDescription.Color.CT_Color().SetColorCluster(linear.AxialShd);
        // 径向渐变
        if (color is CanvasRadialGradient radial)
            return new NetOfd.Core.PageDescription.Color.CT_Color().SetColorCluster(radial.RadialShd);
        // 图案底纹
        if (color is CanvasPattern canvasPattern)
            return new NetOfd.Core.PageDescription.Color.CT_Color().SetPattern(canvasPattern.Pattern);
        // CSS 颜色字符串
        if (color is string str) return ParseCssColor(str);
        return null;
    }

    /// <summary>
    /// 解析 CSS 颜色字符串为 OFD 颜色。
    /// <para>
    /// 与 Java 的差异（此处以规范为准）：OFD 的 Color/@Value 为「通道1 通道2 通道3」格式
    /// （GB/T 33190 §7.4，Java 用 ST_Array 生成），旧实现写成逗号分隔 "r,g,b" 会导致
    /// Java ofdrw 无法解析；此处统一为空格分隔。
    /// </para>
    /// </summary>
    private NetOfd.Core.PageDescription.Color.CT_Color? ParseCssColor(string color)
    {
        var parsed = GradientColorUtil.ToCtColor(color);
        if (parsed is not null) return parsed;

        // 兜底：保持旧行为，返回黑色（OFD 规范格式）
        return new NetOfd.Core.PageDescription.Color.CT_Color { Value = "0 0 0" };
    }
}