using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Archive.Convert.Handler;

/// <summary>
/// 处理器 14：展平过深的 PageBlock 嵌套（GB/T 42133-2022 6.2.3e）。
/// <para>嵌套深度超过 3 的 PageBlock，其子元素<b>原地</b>提升到父级（保持 Z 序），随后删除被掏空的块。</para>
/// <para>与 Java 版差异：Java 用 <c>page.elements("PageBlock")</c> 只取 Page 根的直接子元素，
/// 真实 OFD 中 PageBlock 挂在 Content/Layer 之下，因此 Java 版对真实文档不起作用；
/// 这里改用 TopLevelLocal 定位嵌套链根节点，两种结构都能处理。</para>
/// 对应 Java: PageBlockFlattenHandler
/// </summary>
public sealed class PageBlockFlattenHandler : ArchiveHandler
{
    /// <summary>允许的最大 PageBlock 嵌套深度。</summary>
    private const int MaxDepth = 3;

    public override string HandlerName => "PAGEBLOCK_DEPTH";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, page) in ws.Pages())
            foreach (var pb in page.TopLevelLocal("PageBlock").ToList())
                Flatten(pb, 1);
    }

    /// <summary>自底向上递归展平：先处理子块，再判断自身是否越界。</summary>
    private static void Flatten(XElement block, int depth)
    {
        foreach (var child in block.LocalElems("PageBlock").ToList())
            Flatten(child, depth + 1);

        if (depth <= MaxDepth) return;
        var parent = block.Parent;
        if (parent == null) return;

        var children = block.Elements().ToList();
        foreach (var c in children) c.Remove();
        // 插回到自身原位置，Z 序不变
        if (children.Count > 0) block.AddBeforeSelf(children);
        block.Remove();
    }
}

/// <summary>
/// 处理器 15：裁剪区优化（GB/T 42133-2022 6.3.2a/b）。
/// <para>面积为 0（宽或高 ≤ 0）的裁剪区：先把引用它的图元置 Visible=false，再删除裁剪区及被掏空的容器。</para>
/// <para><b>修复 Java 缺陷</b>：Java 版先 <c>clip.detach()</c> 再读 <c>clip.getParent()</c>，
/// 取到的永远是 null，Visible=false 从未真正生效；这里改为「先定位引用方、后摘除节点」。</para>
/// 对应 Java: ClipAreaHandler
/// </summary>
public sealed class ClipAreaHandler : ArchiveHandler
{
    public override string HandlerName => "CLIP_AREA";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, page) in ws.Pages())
        {
            foreach (var clip in page.LocalDescendants("ClipArea").ToList())
            {
                if (!IsEmptyArea(clip)) continue;

                // 1) 摘除之前先定位全部引用方
                var owners = new List<XElement>();
                var id = clip.AttrTrim("ID");
                if (!string.IsNullOrEmpty(id))
                {
                    owners.AddRange(page.Descendants()
                        .Where(e => string.Equals(id, e.AttrTrim("Clip"), StringComparison.Ordinal)));
                }
                // 内嵌写法：裁剪区直接挂在图元之下
                var host = NearestObject(clip);
                if (host != null) owners.Add(host);

                foreach (var o in owners.Distinct()) o.SetAttributeValue("Visible", "false");

                // 2) 摘除裁剪区，并回收空的 Clip/Clips 容器
                var container = clip.Parent;
                clip.Remove();
                PruneEmptyClipContainers(container, page);
            }
        }
    }

    /// <summary>Boundary 的宽或高 ≤ 0 视为空裁剪区。</summary>
    private static bool IsEmptyArea(XElement clip)
    {
        var boundary = clip.AttrTrim("Boundary");
        if (string.IsNullOrEmpty(boundary)) return false;
        var parts = boundary!.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 4) return false;
        if (!double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)) return false;
        if (!double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var h)) return false;
        return w <= 0 || h <= 0;
    }

    /// <summary>向上找最近的图元对象（元素名以 Object 结尾）。</summary>
    private static XElement? NearestObject(XElement clip)
    {
        for (var p = clip.Parent; p != null; p = p.Parent)
            if (p.Name.LocalName.EndsWith("Object", StringComparison.Ordinal)) return p;
        return null;
    }

    /// <summary>逐级回收因裁剪区删除而变空的 Clip / Clips 容器。</summary>
    private static void PruneEmptyClipContainers(XElement? container, XElement page)
    {
        while (container != null && container != page && !container.HasElements)
        {
            var name = container.Name.LocalName;
            if (name != "Clip" && name != "Clips") return;
            var up = container.Parent;
            container.Remove();
            container = up;
        }
    }
}

/// <summary>
/// 处理器 17：同页图层名唯一化（GB/T 42133-2022 6.2.3d）。
/// <para>重名图层追加 _2、_3……直到不冲突。</para>
/// <para>与 Java 版差异：Java 只看 Page 根的直接子元素，这里深层遍历以适配 Content/Layer 结构。</para>
/// 对应 Java: LayerNameHandler
/// </summary>
public sealed class LayerNameHandler : ArchiveHandler
{
    public override string HandlerName => "LAYER_NAME";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, page) in ws.Pages())
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var layer in page.LocalDescendants("Layer"))
            {
                var name = layer.AttrTrim("Name");
                if (string.IsNullOrEmpty(name)) continue;
                if (seen.Add(name!)) continue;

                var counter = 2;
                string candidate;
                do { candidate = name + "_" + counter; counter++; } while (!seen.Add(candidate));
                layer.SetAttributeValue("Name", candidate);
            }
        }
    }
}

/// <summary>
/// 处理器 18：图像对象禁用插值绘制（GB/T 42133-2022 6.5b）。
/// 对应 Java: ImageInterpolateHandler
/// </summary>
public sealed class ImageInterpolateHandler : ArchiveHandler
{
    public override string HandlerName => "IMAGE_INTERPOLATE";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, page) in ws.Pages())
            foreach (var io in page.LocalDescendants("ImageObject"))
                io.SetAttributeValue("Interpolate", "false");
    }
}

/// <summary>
/// 处理器 19：清理无用勾边属性（GB/T 42133-2022 6.3.3c/d）。
/// <para>TextObject / PathObject 在 Stroke=false 时删除勾边修饰属性；
/// CompositeObject 上勾边与填充属性均无意义，一律删除。</para>
/// 对应 Java: CleanStrokeAttrHandler
/// </summary>
public sealed class CleanStrokeAttrHandler : ArchiveHandler
{
    public override string HandlerName => "CLEAN_STROKE_ATTR";

    /// <summary>Stroke=false 时应当删除的勾边修饰属性。</summary>
    private static readonly string[] StrokeAttrs =
    {
        "LineWidth", "Cap", "Join", "MiterLimit", "DashPattern", "DashOffset", "StrokeColor"
    };

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, page) in ws.Pages())
        {
            foreach (var name in new[] { "TextObject", "PathObject" })
            {
                foreach (var obj in page.LocalDescendants(name))
                {
                    if (!"false".Equals(obj.AttrTrim("Stroke"), StringComparison.OrdinalIgnoreCase)) continue;
                    RemoveAttrs(obj, StrokeAttrs);
                }
            }

            foreach (var comp in page.LocalDescendants("CompositeObject"))
            {
                RemoveAttrs(comp, StrokeAttrs);
                RemoveAttrs(comp, new[] { "Stroke", "Fill", "FillColor" });
            }
        }
    }

    private static void RemoveAttrs(XElement e, IEnumerable<string> names)
    {
        foreach (var n in names) e.Attribute(n)?.Remove();
    }
}

/// <summary>
/// 处理器 20：清理图像对象的无用填充属性（GB/T 42133-2022 6.3.3d/e）。
/// <para>ImageObject 的 Fill / FillColor 对该类对象无意义，予以删除。</para>
/// 对应 Java: CleanFillAttrHandler
/// </summary>
public sealed class CleanFillAttrHandler : ArchiveHandler
{
    public override string HandlerName => "CLEAN_FILL_ATTR";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, page) in ws.Pages())
        {
            foreach (var io in page.LocalDescendants("ImageObject"))
            {
                io.Attribute("Fill")?.Remove();
                io.Attribute("FillColor")?.Remove();
            }
        }
    }
}
