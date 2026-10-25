using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NetOfd.Graphics2D;
using NetOfd.Graphics2D.AWT;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Graphics.Colors;
using UglyToad.PdfPig.Graphics.Core;

namespace NetOfd.Converter;

/// <summary>
/// PDF → OFD 矢量转换器。
/// <para>
/// 读取 PDF 页面中的文字（Letter）、矢量路径（Path）并将它们重绘到
/// OFDGraphics2D 图形桥上，输出矢量 OFD 文档。页面尺寸按 PDF 页面等比例换算
/// （pt → mm, 1pt = 25.4/72mm），并处理 PDF 自下而上的坐标系（垂直翻转）。
/// </para>
/// 对应 Java: org.ofdrw.converter.ofdconverter.PDFConverter
/// </summary>
public class PdfConverter : IDisposable
{
    /// <summary>每毫米的点数（1pt = 25.4/72 mm）。</summary>
    private const double PtPerMm = 72.0 / 25.4;

    private readonly OFDGraphicsDocument _doc;
    private bool _closed;
    private int _renderedPages;

    /// <summary>创建 PDF→OFD 转换器（输出到文件路径）。</summary>
    /// <param name="ofdPath">转换后的 OFD 文件路径</param>
    public PdfConverter(string ofdPath)
    {
        if (string.IsNullOrEmpty(ofdPath))
            throw new ArgumentException("转换后的OFD文件路径为空", nameof(ofdPath));

        var full = Path.GetFullPath(ofdPath);
        if (!File.Exists(full))
        {
            var parent = Path.GetDirectoryName(full);
            if (parent is not null && !Directory.Exists(parent))
                Directory.CreateDirectory(parent);
        }
        _doc = new OFDGraphicsDocument(full);
    }

    /// <summary>创建 PDF→OFD 转换器（输出到流）。</summary>
    /// <param name="output">转换后的 OFD 流，由调用者负责关闭</param>
    public PdfConverter(Stream output)
    {
        if (output is null)
            throw new ArgumentException("转换后的OFD流为空");
        _doc = new OFDGraphicsDocument(output);
    }

    /// <summary>
    /// 转换 PDF 文件。
    /// </summary>
    /// <param name="pdfPath">待转换的 PDF 文件路径</param>
    /// <param name="indexes">可选：要转换的页号（1 起）。为空时转换全部页面；支持乱序/重复。</param>
    public void Convert(string pdfPath, params int[] indexes)
    {
        if (string.IsNullOrEmpty(pdfPath) || !File.Exists(pdfPath))
            return;
        if (IsDirectory(pdfPath))
            return;

        try
        {
            using var pdf = PdfDocument.Open(pdfPath);
            var pages = pdf.GetPages().ToList();

            if (indexes is { Length: > 0 })
            {
                foreach (var index in indexes)
                {
                    var page = pages.FirstOrDefault(p => p.Number == index);
                    if (page is not null)
                        RenderPage(page);
                }
            }
            else
            {
                foreach (var page in pages)
                    RenderPage(page);
            }
        }
        catch (Exception e) when (e is not GeneralConvertException)
        {
            throw new GeneralConvertException("PDF转换OFD异常", e);
        }
    }

    /// <summary>将单个 PDF 页面重绘到 OFD 页面。</summary>
    private void RenderPage(Page page)
    {
        // PdfPig 已按页面旋转给出有效宽高（自下而上坐标系，单位 pt）。
        double widthMm = page.Width / PtPerMm;
        double heightMm = page.Height / PtPerMm;
        double heightPt = page.Height;

        using var g = _doc.NewPage(widthMm, heightMm);
        _renderedPages++;

        DrawLetters(g, page, heightPt);
        DrawPaths(g, page, heightPt);
        DrawImages(g, page, heightPt);
    }

    /// <summary>重绘嵌入图片（尽力而为：解码失败/格式不支持时跳过该项，不影响整体转换）。</summary>
    private void DrawImages(OFDGraphics2D g, Page page, double heightPt)
    {
        try
        {
            foreach (var image in page.GetImages())
            {
                byte[]? png = null;
                try { image.TryGetPng(out png); } catch { }
                if (png is null || png.Length == 0) continue;

                // 使用唯一文件名写入临时文件（AddImage 按文件名缓存，重名会复用错误图片）。
                var tmp = Path.Combine(Path.GetTempPath(), "ofdpdfimg_" + Guid.NewGuid().ToString("N") + ".png");
                try
                {
                    File.WriteAllBytes(tmp, png);
                    var media = _doc.AddImage(tmp);
                    var b = image.Bounds;
                    g.DrawImage(media,
                        ToMmX(b.Left),
                        ToMmY(b.Top, heightPt),
                        PtsToMm(b.Width),
                        PtsToMm(b.Height));
                }
                finally
                {
                    try { File.Delete(tmp); } catch { }
                }
            }
        }
        catch
        {
            // 图片解码失败时跳过，保证整体转换不中断（尽力而为）。
        }
    }

    /// <summary>重绘文字（每个 Letter 独立绘制，保留布局与颜色）。</summary>
    private static void DrawLetters(OFDGraphics2D g, Page page, double heightPt)
    {
        foreach (var letter in page.Letters)
        {
            if (string.IsNullOrEmpty(letter.Value) || letter.Value == " ")
                continue;
            // 跳过不可见文字（OCR 层通常以渲染模式 3 写入，绘制会产生幽灵文字）
            if (letter.RenderingMode is TextRenderingMode.Neither or TextRenderingMode.NeitherClip)
                continue;

            try
            {
                g.SetColor(ToOfdColor(letter.FillColor ?? letter.Color));
                g.SetFont(new OFDFont(
                    string.IsNullOrEmpty(letter.FontName) ? "sanserif" : letter.FontName,
                    OFDFont.PLAIN,
                    PtsToMm(letter.PointSize)));
                g.DrawString(letter.Value, ToMmX(letter.StartBaseLine.X), ToMmY(letter.StartBaseLine.Y, heightPt));
            }
            catch
            {
                // 单字符绘制失败（如字体名异常）时跳过，保证整页转换不中断（尽力而为）。
            }
        }
    }

    /// <summary>重绘矢量路径（填充/描边）。</summary>
    private static void DrawPaths(OFDGraphics2D g, Page page, double heightPt)
    {
        var paths = page.ExperimentalAccess.Paths;
        if (paths is null) return;

        foreach (var path in paths)
        {
            try
            {
                var gp = ToGeneralPath(path, heightPt);
                if (gp is null) continue;

                if (path.IsFilled)
                {
                    g.SetColor(ToOfdColor(path.FillColor));
                    g.Fill(gp, path.FillingRule == FillingRule.EvenOdd ? "Even-Odd" : "NonZero");
                }
                if (path.IsStroked)
                {
                    g.SetColor(ToOfdColor(path.StrokeColor));
                    g.SetStroke(MakeStroke(path));
                    g.Draw(gp);
                }
            }
            catch
            {
                // 单条路径构造/绘制失败时跳过，保证整页转换不中断（尽力而为）。
            }
        }
    }

    /// <summary>由 PdfPig 路径构造 AWT GeneralPath（坐标为 mm，垂直翻转）。</summary>
    private static GeneralPath? ToGeneralPath(object path, double heightPt)
    {
        var gp = new GeneralPath();
        var subpaths = path as IEnumerable;
        if (subpaths is null) return null;

        foreach (var subpath in subpaths)
        {
            if (subpath is null) continue;
            var commands = GetField(subpath, "Commands") as IEnumerable;
            if (commands is null) continue;

            foreach (var cmd in commands)
            {
                if (cmd is null) continue;
                switch (cmd.GetType().Name)
                {
                    case "Move":
                        var m = ReadPoint(cmd, "Location");
                        gp.MoveTo(ToMmX(m.X), ToMmY(m.Y, heightPt));
                        break;
                    case "Line":
                        var t = ReadPoint(cmd, "To");
                        gp.LineTo(ToMmX(t.X), ToMmY(t.Y, heightPt));
                        break;
                    case "QuadraticBezierCurve":
                        var qc1 = ReadPoint(cmd, "ControlPoint");
                        var qEnd = ReadPoint(cmd, "EndPoint");
                        gp.QuadTo(ToMmX(qc1.X), ToMmY(qc1.Y, heightPt), ToMmX(qEnd.X), ToMmY(qEnd.Y, heightPt));
                        break;
                    case "BezierCurve":
                        var c1 = ReadPoint(cmd, "FirstControlPoint");
                        var c2 = ReadPoint(cmd, "SecondControlPoint");
                        var end = ReadPoint(cmd, "EndPoint");
                        gp.CurveTo(ToMmX(c1.X), ToMmY(c1.Y, heightPt),
                                   ToMmX(c2.X), ToMmY(c2.Y, heightPt),
                                   ToMmX(end.X), ToMmY(end.Y, heightPt));
                        break;
                    case "Close":
                        gp.Close();
                        break;
                }
            }
        }

        return gp.Operations.Count == 0 ? null : gp;
    }

    /// <summary>由 PdfPig 路径描边属性构造 OFDStroke（线宽/线端/线接/虚线，pt→mm）。</summary>
    private static OFDStroke MakeStroke(object path)
    {
        var cap = path.GetType().GetProperty("LineCapStyle")?.GetValue(path) as LineCapStyle?;
        var join = path.GetType().GetProperty("LineJoinStyle")?.GetValue(path) as LineJoinStyle?;

        double width = 1.0;
        try { width = (double)path.GetType().GetProperty("LineWidth")!.GetValue(path)!; } catch { }

        var stroke = new OFDStroke((float)PtsToMm(width))
        {
            EndCap = cap switch
            {
                LineCapStyle.Round => OFDStroke.CAP_ROUND,
                LineCapStyle.ProjectingSquare => OFDStroke.CAP_SQUARE,
                _ => OFDStroke.CAP_BUTT
            },
            LineJoin = join switch
            {
                LineJoinStyle.Round => OFDStroke.JOIN_ROUND,
                LineJoinStyle.Bevel => OFDStroke.JOIN_BEVEL,
                _ => OFDStroke.JOIN_MITER
            }
        };

        if (path.GetType().GetProperty("LineDashPattern")?.GetValue(path) is { } dash)
        {
            var arrayProp = dash.GetType().GetProperty("Array");
            var phaseProp = dash.GetType().GetProperty("Phase");
            if (arrayProp?.GetValue(dash) is System.Collections.Generic.IReadOnlyList<decimal> pattern && pattern.Count > 0)
            {
                stroke.DashArray = pattern.Select(x => (float)x).ToArray();
                try { stroke.DashPhase = (int)phaseProp!.GetValue(dash)!; } catch { }
            }
        }
        return stroke;
    }

    private static PdfPoint ReadPoint(object obj, string prop)
    {
        var value = obj.GetType().GetProperty(prop)?.GetValue(obj);
        return value is PdfPoint pt ? pt : new PdfPoint(0, 0);
    }

    private static object? GetField(object obj, string name)
        => obj.GetType().GetProperty(name)?.GetValue(obj);

    /// <summary>由 IColor 解析 OFD 颜色（ToRGBValues 返回 0..1 归一化值）。</summary>
    private static OFDColor ToOfdColor(IColor? color)
    {
        if (color is null) return OFDColor.Black;
        try
        {
            var rgb = color.ToRGBValues();
            return new OFDColor(
                Clamp((int)Math.Round(rgb.r * 255)),
                Clamp((int)Math.Round(rgb.g * 255)),
                Clamp((int)Math.Round(rgb.b * 255)));
        }
        catch
        {
            return OFDColor.Black;
        }
    }

    private static int Clamp(int v) => v < 0 ? 0 : (v > 255 ? 255 : v);

    private static double ToMmX(double pt) => pt / PtPerMm;
    private static double ToMmY(double pt, double heightPt) => (heightPt - pt) / PtPerMm;
    private static double PtsToMm(double pt) => pt / PtPerMm;

    private static bool IsDirectory(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.Directory) != 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>关闭文档并释放资源（幂等）。未转换任何页面时不写出无效（0 页）OFD。</summary>
    public void Close()
    {
        if (_closed) return;
        _closed = true;
        if (_renderedPages == 0) return;
        _doc.Close();
    }

    public void Dispose()
    {
        Close();
    }
}
