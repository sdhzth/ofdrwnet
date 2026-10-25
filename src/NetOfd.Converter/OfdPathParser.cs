using SkiaSharp;
using System.Globalization;

namespace NetOfd.Converter;

/// <summary>
/// 解析 OFD 路径紧凑数据（AbbreviatedData）为 SkiaSharp 的 <see cref="SKPath"/>。
/// <para>
/// 同时兼容两种指令风格（按参数个数消歧）：
/// <list type="bullet">
///   <item>OFD 标准 / Java ofdrw 风格：<c>S</c> 起点、<c>M</c> 移动、<c>L</c> 线、<c>Q</c> 二次、<c>B</c> 三次、<c>A</c> 弧、<c>C</c> 闭合、<c>CM</c> 自定义（忽略）。</item>
///   <item>本移植版 NetOfd.Layout.DrawContext 风格（SVG 风）：<c>M/L/Q/C/Z</c>，其中 <c>C</c> 为三次贝塞尔（6 参数）、<c>Z</c> 为闭合。</item>
/// </list>
/// 圆弧（<c>A</c>）已按 SVG/OFD 椭圆弧语义实现：将椭圆弧转换为若干段三次贝塞尔近似
/// （每段 ≤ 90°，<c>(4/3)·tan(δ/4)</c> 控制点系数），与 NetOfd.Layout 写入侧的
/// PathBuilder.Arc（产出 <c>A rx ry angle largeArc sweep x y</c>）形成 round-trip。
/// 对应 Java: org.ofdrw.converter.AWTMaker.buildPath + org.ofdrw.core.graph.pathObj.AbbreviatedData.parse
/// </para>
/// </summary>
public static class OfdPathParser
{
    public static SKPath Parse(string? abbreviatedData)
    {
        var path = new SKPath();
        if (string.IsNullOrWhiteSpace(abbreviatedData))
            return path;

        // 1. 词法切分：把 "M 1 2 L 3 4" 拆成 token 序列
        var tokens = abbreviatedData
            .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

        double cx = 0, cy = 0;          // 当前点
        string? pendingCmd = null;
        var values = new List<double>();

        void Flush()
        {
            if (pendingCmd is null) return;
            Emit(path, pendingCmd, values, ref cx, ref cy);
            values.Clear();
            pendingCmd = null;
        }

        foreach (var tok in tokens)
        {
            if (IsCommand(tok))
            {
                Flush();
                pendingCmd = tok;
            }
            else
            {
                if (double.TryParse(tok, NumberStyles.Float, CultureInfo.InvariantCulture, out var v))
                    values.Add(v);
                else
                    values.Add(0); // 无法解析的数字按 0 处理（与 Java 一致）
            }
        }
        Flush();

        return path;
    }

    private static bool IsCommand(string tok)
    {
        // 单/双字母命令，全部大写或小写
        return tok.Length switch
        {
            1 => "SMLQBCAZsmlqbcaz".Contains(tok),
            2 => tok.Equals("CM", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static void Emit(SKPath path, string cmd, List<double> v, ref double cx, ref double cy)
    {
        bool rel = char.IsLower(cmd[0]);
        string op = cmd.ToUpperInvariant();

        // 处理 C 的歧义：6 参数 -> 三次贝塞尔（DrawContext 风格）；0 参数 -> 闭合（OFD 标准风格）
        if (op == "C")
        {
            if (v.Count >= 6) Cubic(path, v, rel, ref cx, ref cy);
            else path.Close();
            return;
        }
        if (op == "CM") return; // 自定义扩展，忽略

        switch (op)
        {
            case "S":
            case "M":
                {
                    double x = v.Count >= 1 ? v[0] : 0;
                    double y = v.Count >= 2 ? v[1] : 0;
                    if (rel) { x += cx; y += cy; }
                    cx = x; cy = y;
                    path.MoveTo((float)x, (float)y);
                    break;
                }
            case "L":
                {
                    double x = v.Count >= 1 ? v[0] : 0;
                    double y = v.Count >= 2 ? v[1] : 0;
                    if (rel) { x += cx; y += cy; }
                    cx = x; cy = y;
                    path.LineTo((float)x, (float)y);
                    break;
                }
            case "Q":
                {
                    if (v.Count < 4) break;
                    double c1x = v[0], c1y = v[1], tx = v[2], ty = v[3];
                    if (rel) { c1x += cx; c1y += cy; tx += cx; ty += cy; }
                    cx = tx; cy = ty;
                    path.QuadTo((float)c1x, (float)c1y, (float)tx, (float)ty);
                    break;
                }
            case "B": // OFD 标准三次贝塞尔：控制点1、控制点2、目标点
                Cubic(path, v, rel, ref cx, ref cy);
                break;
            case "A": // 椭圆弧：rx ry x轴旋转 large-arc-flag sweep-flag x y
                {
                    if (v.Count < 7) break;
                    double rx = v[0], ry = v[1];
                    double xAxisRot = v[2];
                    bool largeArc = v[3] != 0;
                    bool sweep = v[4] != 0;
                    double x = v[5], y = v[6];
                    if (rel) { x += cx; y += cy; }
                    AppendArc(path, cx, cy, rx, ry, xAxisRot, largeArc, sweep, x, y, ref cx, ref cy);
                    break;
                }
            case "Z":
                path.Close();
                break;
        }
    }

    private static void Cubic(SKPath path, List<double> v, bool rel, ref double cx, ref double cy)
    {
        if (v.Count < 6) return;
        double c1x = v[0], c1y = v[1], c2x = v[2], c2y = v[3], tx = v[4], ty = v[5];
        if (rel)
        {
            c1x += cx; c1y += cy; c2x += cx; c2y += cy; tx += cx; ty += cy;
        }
        cx = tx; cy = ty;
        path.CubicTo((float)c1x, (float)c1y, (float)c2x, (float)c2y, (float)tx, (float)ty);
    }

    /// <summary>
    /// 将椭圆弧（起点 (x0,y0) → 终点 (x,y)）转换为若干段三次贝塞尔曲线并追加到 SKPath。
    /// 采用 SVG 端点→中心参数化 + 贝塞尔近似（每段 ≤ 90°），
    /// 与 OFD/Java ofdrw 的 <c>A rx ry x-axis-rotation large-arc-flag sweep-flag x y</c> 完全一致。
    /// </summary>
    private static void AppendArc(SKPath path, double x0, double y0, double rx, double ry,
        double xAxisRotDeg, bool largeArc, bool sweep, double x, double y, ref double cx, ref double cy)
    {
        // 退化情形：零半径或起止重合 → 直线连接
        if (rx == 0 || ry == 0 || (x0 == x && y0 == y))
        {
            path.LineTo((float)x, (float)y);
            cx = x; cy = y;
            return;
        }

        double phi = xAxisRotDeg * Math.PI / 180.0;
        double cosPhi = Math.Cos(phi), sinPhi = Math.Sin(phi);

        // 起止点差的一半，旋转到椭圆局部坐标
        double dx2 = (x0 - x) / 2.0;
        double dy2 = (y0 - y) / 2.0;
        double x1p = cosPhi * dx2 + sinPhi * dy2;
        double y1p = -sinPhi * dx2 + cosPhi * dy2;

        rx = Math.Abs(rx);
        ry = Math.Abs(ry);

        // 半径不足以容纳两点时按比例放大（OFD/SVG 同款修正）
        double lambda = (x1p * x1p) / (rx * rx) + (y1p * y1p) / (ry * ry);
        if (lambda > 1)
        {
            double sq = Math.Sqrt(lambda);
            rx *= sq;
            ry *= sq;
        }

        // 中心（椭圆局部坐标）
        double den = rx * rx * y1p * y1p + ry * ry * x1p * x1p;
        double co = den > 0 ? Math.Sqrt(Math.Max(0,
            (rx * rx * ry * ry - rx * rx * y1p * y1p - ry * ry * x1p * x1p) / den)) : 0;
        if (largeArc == sweep) co = -co;
        double cxp = co * (rx * y1p / ry);
        double cyp = co * (-ry * x1p / rx);

        // 中心（用户坐标）
        double cxUser = cosPhi * cxp - sinPhi * cyp + (x0 + x) / 2.0;
        double cyUser = sinPhi * cxp + cosPhi * cyp + (y0 + y) / 2.0;

        // 起止角度（归一化椭圆空间）
        double angle1 = Angle(1, 0, (x1p - cxp) / rx, (y1p - cyp) / ry);
        double angle2 = Angle((x1p - cxp) / rx, (y1p - cyp) / ry,
                               (-x1p - cxp) / rx, (-y1p - cyp) / ry);
        double delta = angle2;
        if (!sweep && delta > 0) delta -= 2 * Math.PI;
        if (sweep && delta < 0) delta += 2 * Math.PI;

        // 按 ≤ 90° 分段，每段用三次贝塞尔近似
        const double maxSeg = Math.PI / 2.0;
        int segments = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / maxSeg));
        double seg = delta / segments;

        for (int i = 0; i < segments; i++)
        {
            double t1 = angle1 + i * seg;
            double t2 = t1 + seg;

            double ex1 = cxUser + rx * cosPhi * Math.Cos(t1) - ry * sinPhi * Math.Sin(t1);
            double ey1 = cyUser + rx * sinPhi * Math.Cos(t1) + ry * cosPhi * Math.Sin(t1);
            double ex2 = cxUser + rx * cosPhi * Math.Cos(t2) - ry * sinPhi * Math.Sin(t2);
            double ey2 = cyUser + rx * sinPhi * Math.Cos(t2) + ry * cosPhi * Math.Sin(t2);

            // 控制点系数（精确适用于 ≤90° 分段）
            double f = (4.0 / 3.0) * Math.Tan(seg / 4.0);

            double c1x = ex1 + f * (-rx * cosPhi * Math.Sin(t1) - ry * sinPhi * Math.Cos(t1));
            double c1y = ey1 + f * (-rx * sinPhi * Math.Sin(t1) + ry * cosPhi * Math.Cos(t1));
            double c2x = ex2 - f * (-rx * cosPhi * Math.Sin(t2) - ry * sinPhi * Math.Cos(t2));
            double c2y = ey2 - f * (-rx * sinPhi * Math.Sin(t2) + ry * cosPhi * Math.Cos(t2));

            path.CubicTo((float)c1x, (float)c1y, (float)c2x, (float)c2y, (float)ex2, (float)ey2);
        }

        cx = x; cy = y;
    }

    /// <summary>有向角（弧度，范围 (−π, π]）：从向量 (ux,uy) 到 (vx,vy)。</summary>
    private static double Angle(double ux, double uy, double vx, double vy)
    {
        double dot = ux * vx + uy * vy;
        double ulen = Math.Sqrt(ux * ux + uy * uy);
        double vlen = Math.Sqrt(vx * vx + vy * vy);
        double a = Math.Acos(Math.Max(-1, Math.Min(1, ulen * vlen == 0 ? 0 : dot / (ulen * vlen))));
        if (ux * vy - uy * vx < 0) a = -a;
        return a;
    }
}
