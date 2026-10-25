using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Archive.Rule;

/// <summary>规则 14：PageBlock 嵌套深度 ≤ 3（GB/T 42133-2022 6.2.3e）。</summary>
public sealed class PageBlockDepthRule : ArchiveRule
{
    public override string RuleName => "PAGEBLOCK_DEPTH";
    private const int MaxDepth = 3;

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, page) in ctx.Pages())
        {
            // 从「最外层」PageBlock 起算：真实 OFD 中 PageBlock 挂在 Content/Layer 下，
            // 合成骨架里可能直接挂在 Page 下，TopLevelLocal 两种结构都能正确定位嵌套链根节点。
            foreach (var pb in page.TopLevelLocal("PageBlock"))
                CheckDepth(pb, 1, path, violations);
        }
        return violations;
    }

    private static void CheckDepth(XElement element, int depth, string pageLoc, List<ArchiveViolation> violations)
    {
        if (depth > MaxDepth)
        {
            violations.Add(new ArchiveViolation("PAGEBLOCK_DEPTH", ArchiveViolation.Severity.Error,
                "PageBlock 嵌套深度超过最大允许值: " + depth + " > " + MaxDepth, pageLoc,
                depth.ToString(), "≤" + MaxDepth));
            return;
        }
        foreach (var child in element.LocalElems("PageBlock"))
            CheckDepth(child, depth + 1, pageLoc, violations);
    }
}

/// <summary>规则 15：裁剪区优化检查（GB/T 42133-2022 6.3.2）。</summary>
public sealed class ClipAreaRule : ArchiveRule
{
    public override string RuleName => "CLIP_AREA";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, page) in ctx.Pages())
        {
            foreach (var clip in page.LocalDescendants("ClipArea"))
            {
                var boundaryStr = clip.AttrTrim("Boundary");
                if (boundaryStr == null) continue;
                var parts = boundaryStr.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 4) continue;
                if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                    || !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                    continue;
                if (w <= 0 || h <= 0)
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                        "裁剪区面积为 0，应删除并设置 Visible=false", path, "面积=0", "Visible=false"));
                }
            }
        }
        return violations;
    }
}

/// <summary>规则 25：文字仅横向缩放时应用 HScale 属性（GB/T 42133-2022 6.6d）。</summary>
public sealed class TextHScaleRule : ArchiveRule
{
    public override string RuleName => "TEXT_HSCALE";
    private const double Epsilon = 1e-6;

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, page) in ctx.Pages())
        {
            foreach (var to in page.LocalDescendants("TextObject"))
            {
                var ctmStr = to.AttrTrim("CTM");
                if (string.IsNullOrEmpty(ctmStr)) continue;
                var ctm = ParseCtm(ctmStr);
                if (ctm == null) continue;
                // 纯横向缩放: b≈0, c≈0, d≈1, a≠1
                if (Math.Abs(ctm[1]) < Epsilon && Math.Abs(ctm[2]) < Epsilon
                    && Math.Abs(ctm[3] - 1) < Epsilon && Math.Abs(ctm[0] - 1) > Epsilon)
                {
                    if (string.IsNullOrEmpty(to.AttrTrim("HScale")))
                    {
                        violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                            "TextObject 的 CTM 为纯横向缩放 (a=" + ctm[0].ToString("F3", CultureInfo.InvariantCulture)
                            + ")，建议使用 HScale 属性代替", path,
                            "CTM: " + ctmStr, "HScale=" + ctm[0].ToString("F3", CultureInfo.InvariantCulture)));
                    }
                }
            }
        }
        return violations;
    }

    private static double[]? ParseCtm(string s)
    {
        var parts = s.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 6) return null;
        var r = new double[6];
        for (var i = 0; i < 6; i++)
            if (!double.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out r[i])) return null;
        return r;
    }
}

/// <summary>规则 23：文字对象应使用 Size 属性标识大小（GB/T 42133-2022 6.6c）。</summary>
public sealed class TextSizeRule : ArchiveRule
{
    public override string RuleName => "TEXT_SIZE";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, page) in ctx.Pages())
        {
            foreach (var to in page.LocalDescendants("TextObject"))
            {
                if (string.IsNullOrEmpty(to.AttrTrim("Size")))
                {
                    var id = to.AttrTrim("ID") ?? "?";
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                        "文字对象缺少 Size 属性 (ID=" + id + ")", path, "缺失", "应设置 Size 属性"));
                }
            }
        }
        return violations;
    }
}

/// <summary>规则 17：图像对象插值绘制必须为 false（GB/T 42133-2022 6.5b）。</summary>
public sealed class ImageInterpolateRule : ArchiveRule
{
    public override string RuleName => "IMAGE_INTERPOLATE";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, page) in ctx.Pages())
        {
            foreach (var io in page.LocalDescendants("ImageObject"))
            {
                if ("true".Equals(io.AttrTrim("Interpolate"), StringComparison.OrdinalIgnoreCase))
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                        "图像对象 Interpolate=true，OFD-A 要求设置为 false", path, "true", "false"));
                }
            }
        }
        return violations;
    }
}
