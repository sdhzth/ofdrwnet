using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using SkiaSharp;

namespace NetOfd.Archive.Convert.Handler;

/// <summary>
/// 处理器 9：删除音视频资源（GB/T 42133-2022 6.2.6g）。
/// <para>同时删除包内媒体文件本体与资源登记项。</para>
/// 对应 Java: AudioVideoHandler
/// </summary>
public sealed class AudioVideoHandler : ArchiveHandler
{
    public override string HandlerName => "AUDIO_VIDEO";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (resPath, res) in ws.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia").ToList())
            {
                var type = mm.AttrTrim("Type");
                if (!"Audio".Equals(type, StringComparison.OrdinalIgnoreCase)
                    && !"Video".Equals(type, StringComparison.OrdinalIgnoreCase)) continue;

                var mediaFile = mm.LocValue("MediaFile");
                if (!string.IsNullOrEmpty(mediaFile))
                {
                    var full = ws.ResolveResPath(resPath, res, mediaFile!);
                    if (full != null) ws.Delete(full);
                }
                mm.Remove();
            }
        }
    }
}

/// <summary>
/// 处理器 10：删除图像资源上的自定义扩展数据（GB/T 42133-2022 6.2.6f）。
/// <para>非 OFD 命名空间的子元素一律剔除。</para>
/// 对应 Java: ImageExtensionHandler
/// </summary>
public sealed class ImageExtensionHandler : ArchiveHandler
{
    public override string HandlerName => "IMAGE_EXTENSION";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, res) in ws.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia").ToList())
            {
                if (!"Image".Equals(mm.AttrTrim("Type"), StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var child in mm.Elements().ToList())
                {
                    var ns = child.Name.Namespace;
                    if (ns != XNamespace.None && ns.NamespaceName != ArchiveWorkspace.OfdNamespace)
                        child.Remove();
                }
            }
        }
    }
}

/// <summary>
/// 处理器 11：资源归位（GB/T 42133-2022 6.2.6a）。
/// <para>PublicRes 中的 MultiMedias/CompositeGraphicUnits/DrawParams 迁往 DocumentRes；
/// PageRes 中的 ColorSpaces/Fonts 迁往 PublicRes。迁移时把相对 loc 改写为包内绝对路径，避免基准目录变化导致引用失效。</para>
/// <para><b>Java 原版此处为空实现</b>（只有一个空循环），.NET 版补齐了真实逻辑。</para>
/// 对应 Java: ResourcePlacementHandler
/// </summary>
public sealed class ResourcePlacementHandler : ArchiveHandler
{
    public override string HandlerName => "RESOURCE_PLACEMENT";

    private static readonly string[] MisplacedInPublic = { "MultiMedias", "CompositeGraphicUnits", "DrawParams" };
    private static readonly string[] MisplacedInPageRes = { "ColorSpaces", "Fonts" };

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (docPath, doc) in ws.Documents())
        {
            var docDir = ArchiveContext.DirOf(docPath);
            var cd = doc.LocalElem("CommonData");
            if (cd == null) continue;

            foreach (var (pubPath, pubRoot) in ResHelper.NamedResFiles(ws, cd, docDir, "PublicRes"))
            {
                if (!ResHelper.HasAny(pubRoot, MisplacedInPublic)) continue;
                var target = ResHelper.EnsureRes(ws, doc, cd, docDir, "DocumentRes", "DocumentRes.xml");
                if (target.Root == pubRoot) continue;
                ResHelper.MoveSections(ws, pubPath, pubRoot, target, MisplacedInPublic);
            }

            foreach (var (prPath, prRoot) in ResHelper.PageResFiles(ws, docDir))
            {
                if (!ResHelper.HasAny(prRoot, MisplacedInPageRes)) continue;
                var target = ResHelper.EnsureRes(ws, doc, cd, docDir, "PublicRes", "PublicRes.xml");
                if (target.Root == prRoot) continue;
                ResHelper.MoveSections(ws, prPath, prRoot, target, MisplacedInPageRes);
            }
        }
    }
}

/// <summary>
/// 处理器 12：多页共用图像注册到 DocumentRes（GB/T 42133-2022 6.5a）。
/// <para>被 ≥2 个页面引用、却定义在 PageRes 中的图像资源迁入 DocumentRes。</para>
/// <para><b>Java 原版此处为空实现</b>，.NET 版补齐了真实逻辑。</para>
/// 对应 Java: ImageResourceRegHandler
/// </summary>
public sealed class ImageResourceRegHandler : ArchiveHandler
{
    public override string HandlerName => "IMAGE_RESOURCE_REG";

    public override void Handle(ArchiveWorkspace ws)
    {
        var refs = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);
        var pageNum = 0;
        foreach (var (_, page) in ws.Pages())
        {
            pageNum++;
            foreach (var io in page.LocalDescendants("ImageObject"))
            {
                var id = io.AttrTrim("ResourceID");
                if (string.IsNullOrEmpty(id)) continue;
                if (!refs.TryGetValue(id!, out var set)) refs[id!] = set = new HashSet<int>();
                set.Add(pageNum);
            }
        }

        var shared = new HashSet<string>(
            refs.Where(kv => kv.Value.Count >= 2).Select(kv => kv.Key),
            StringComparer.OrdinalIgnoreCase);
        if (shared.Count == 0) return;

        foreach (var (docPath, doc) in ws.Documents())
        {
            var docDir = ArchiveContext.DirOf(docPath);
            var cd = doc.LocalElem("CommonData");
            if (cd == null) continue;

            foreach (var (prPath, prRoot) in ResHelper.PageResFiles(ws, docDir))
            {
                var victims = prRoot.LocalDescendants("MultiMedia")
                    .Where(mm => shared.Contains(mm.AttrTrim("ID") ?? string.Empty))
                    .ToList();
                if (victims.Count == 0) continue;

                var target = ResHelper.EnsureRes(ws, doc, cd, docDir, "DocumentRes", "DocumentRes.xml");
                if (target.Root == prRoot) continue;
                var section = ResHelper.EnsureSection(target.Root, "MultiMedias");
                foreach (var mm in victims)
                {
                    ResHelper.AbsolutizeLocs(ws, prPath, prRoot, mm);
                    mm.Remove();
                    section.Add(mm);
                }
            }
        }
    }
}

/// <summary>
/// 处理器 13：非许可图像格式转码为 PNG（GB/T 42133-2022 6.2.6e）。
/// <para>按魔数判定格式；不在 BMP/JPEG/PNG/JBIG2/JPEG2000/TIFF 之列时用 SkiaSharp 解码后重编码为 PNG，
/// 并同步更新 MediaFile 与 Format。无法解码时保持原样，交由检查器报告。</para>
/// 对应 Java: ImageConvertHandler
/// </summary>
public sealed class ImageConvertHandler : ArchiveHandler
{
    public override string HandlerName => "IMAGE_FORMAT";

    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "BMP", "JPEG", "PNG", "JBIG2", "JPEG2000", "TIFF"
    };

    private static readonly (string Name, byte[] Magic)[] MagicTable =
    {
        ("PNG", new byte[] { 0x89, 0x50, 0x4E, 0x47 }),
        ("JPEG", new byte[] { 0xFF, 0xD8, 0xFF }),
        ("BMP", new byte[] { 0x42, 0x4D }),
        ("JBIG2", new byte[] { 0x97, 0x4A, 0x42, 0x32 }),
        ("TIFF", new byte[] { 0x49, 0x49, 0x2A, 0x00 }),
        ("TIFF", new byte[] { 0x4D, 0x4D, 0x00, 0x2A }),
    };

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (resPath, res) in ws.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia").ToList())
            {
                if (!"Image".Equals(mm.AttrTrim("Type"), StringComparison.OrdinalIgnoreCase)) continue;
                var mediaFile = mm.LocValue("MediaFile");
                if (string.IsNullOrEmpty(mediaFile)) continue;

                var full = ws.ResolveResPath(resPath, res, mediaFile!);
                if (full == null) continue;
                var bytes = ws.GetBytes(full);
                if (bytes == null) continue;

                var fmt = Detect(bytes);
                if (fmt != null && Allowed.Contains(fmt)) continue; // 已合规

                var png = TryEncodePng(bytes);
                if (png == null) continue; // 无法解码，保持原样

                ws.Delete(full);
                ws.SetBytes(ChangeExtension(full, ".png"), png);
                mm.SetLocValue("MediaFile", ChangeExtension(mediaFile!, ".png"));
                mm.SetAttributeValue("Format", "PNG");
            }
        }
    }

    internal static string? Detect(byte[] header)
    {
        foreach (var (name, magic) in MagicTable)
        {
            if (header.Length < magic.Length) continue;
            var ok = true;
            for (var i = 0; i < magic.Length; i++)
                if (header[i] != magic[i]) { ok = false; break; }
            if (ok) return name;
        }
        return null;
    }

    private static byte[]? TryEncodePng(byte[] src)
    {
        try
        {
            using var bmp = SKBitmap.Decode(src);
            if (bmp == null) return null;
            using var img = SKImage.FromBitmap(bmp);
            using var enc = img.Encode(SKEncodedImageFormat.Png, 100);
            return enc?.ToArray();
        }
        catch (Exception)
        {
            // 解码器缺失或图像损坏：不中断管道
            return null;
        }
    }

    internal static string ChangeExtension(string path, string ext)
    {
        var slash = path.LastIndexOf('/');
        var dot = path.LastIndexOf('.');
        return dot > slash && dot >= 0 ? path.Substring(0, dot) + ext : path + ext;
    }
}

/// <summary>资源文件定位、创建与搬迁的共用实现。</summary>
internal static class ResHelper
{
    /// <summary>按 CommonData 中的指定登记项（PublicRes / DocumentRes）取资源文件。</summary>
    public static List<(string Path, XElement Root)> NamedResFiles(
        ArchiveWorkspace ws, XElement commonData, string docDir, string elemName)
    {
        var list = new List<(string, XElement)>();
        foreach (var e in commonData.LocalElems(elemName))
        {
            var loc = e.Value.Trim();
            if (string.IsNullOrEmpty(loc)) continue;
            var path = ArchiveContext.Combine(docDir, loc);
            var root = ws.Xml(path);
            if (root != null) list.Add((path, root));
        }
        return list;
    }

    /// <summary>文档目录下全部 PageRes*.xml。</summary>
    public static List<(string Path, XElement Root)> PageResFiles(ArchiveWorkspace ws, string docDir)
    {
        var list = new List<(string, XElement)>();
        foreach (var key in ws.Keys)
        {
            if (!key.StartsWith(docDir + "/", StringComparison.OrdinalIgnoreCase)) continue;
            var fname = Path.GetFileName(key);
            if (!fname.StartsWith("PageRes", StringComparison.OrdinalIgnoreCase)
                || !fname.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var root = ws.Xml(key);
            if (root != null) list.Add((key, root));
        }
        return list;
    }

    public static bool HasAny(XElement res, IEnumerable<string> sectionNames)
        => sectionNames.Any(n => res.LocalElem(n) != null);

    /// <summary>取（必要时创建并登记）指定资源文件。</summary>
    public static (string Path, XElement Root) EnsureRes(
        ArchiveWorkspace ws, XElement doc, XElement commonData, string docDir, string elemName, string defaultFileName)
    {
        var existing = NamedResFiles(ws, commonData, docDir, elemName);
        if (existing.Count > 0) return existing[0];

        var ns = doc.Name.Namespace;
        var path = ArchiveContext.Combine(docDir, defaultFileName);
        var root = ws.Xml(path) ?? ws.SetXml(path, new XElement(ns + "Res"));

        var locElem = commonData.LocalElem(elemName);
        if (locElem == null) commonData.Add(new XElement(ns + elemName, defaultFileName));
        else locElem.Value = defaultFileName;
        return (path, root);
    }

    /// <summary>取（必要时创建）资源分区节点，如 Fonts / MultiMedias。</summary>
    public static XElement EnsureSection(XElement res, string sectionName)
    {
        var sec = res.LocalElem(sectionName);
        if (sec != null) return sec;
        sec = new XElement(res.Name.Namespace + sectionName);
        res.Add(sec);
        return sec;
    }

    /// <summary>把源资源文件中的若干分区整体搬迁到目标资源文件，并合并同名分区。</summary>
    public static void MoveSections(
        ArchiveWorkspace ws, string srcPath, XElement srcRoot,
        (string Path, XElement Root) target, IEnumerable<string> sectionNames)
    {
        foreach (var name in sectionNames)
        {
            foreach (var sec in srcRoot.LocalElems(name).ToList())
            {
                foreach (var item in sec.Elements()) AbsolutizeLocs(ws, srcPath, srcRoot, item);
                var existing = target.Root.LocalElem(name);
                if (existing == null)
                {
                    sec.Remove();
                    target.Root.Add(sec);
                }
                else
                {
                    foreach (var item in sec.Elements().ToList()) { item.Remove(); existing.Add(item); }
                    sec.Remove();
                }
            }
        }
    }

    private static readonly string[] LocNames = { "MediaFile", "FontFile", "Profile" };

    /// <summary>
    /// 把元素（含子孙）内的相对 loc 改写为包内绝对路径。
    /// <para>资源项跨文件搬迁后，基准目录（含 BaseLoc）会变，必须先固化为绝对路径才不会引用失效。</para>
    /// </summary>
    public static void AbsolutizeLocs(ArchiveWorkspace ws, string srcResPath, XElement srcResRoot, XElement element)
    {
        foreach (var e in element.DescendantsAndSelf())
        {
            foreach (var name in LocNames)
            {
                var v = e.LocValue(name);
                if (string.IsNullOrEmpty(v) || v!.StartsWith("/")) continue;
                var full = ws.ResolveResPath(srcResPath, srcResRoot, v);
                if (full != null && ws.Exists(full)) e.SetLocValue(name, full);
            }
        }
    }
}
