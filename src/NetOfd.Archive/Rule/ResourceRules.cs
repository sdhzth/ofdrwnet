using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Archive.Rule;

/// <summary>规则 9：颜色空间仅限 Gray/RGB/CMYK（GB/T 42133-2022 6.3.1b）。</summary>
public sealed class ColorSpaceRule : ArchiveRule
{
    public override string RuleName => "COLOR_SPACE";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            foreach (var cs in res.LocalDescendants("ColorSpace"))
            {
                var type = cs.AttrTrim("Type");
                if (string.IsNullOrEmpty(type))
                {
                    var id = cs.AttrTrim("ID") ?? "?";
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                        "颜色空间类型未知 (ID=" + id + ")", path, "null", "GRAY/RGB/CMYK"));
                }
            }
        }
        return violations;
    }
}

/// <summary>规则 24：颜色空间建议带 ICC 颜色配置文件（GB/T 42133-2022 6.3.1c，INFO 级提示）。</summary>
public sealed class ColorProfileRule : ArchiveRule
{
    public override string RuleName => "COLOR_PROFILE";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            foreach (var cs in res.LocalDescendants("ColorSpace"))
            {
                if (cs.LocalElem("Profile") == null)
                {
                    var id = cs.AttrTrim("ID") ?? "?";
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Info,
                        "建议为颜色空间添加 ICC 颜色配置文件 (ID=" + id + ")", path, "缺失", "ICC Profile"));
                }
            }
        }
        return violations;
    }
}

/// <summary>规则 10：图像格式仅限 BMP/JPEG/PNG/JBIG2/JPEG2000/TIFF（GB/T 42133-2022 6.2.6e，魔数检测）。</summary>
public sealed class ImageFormatRule : ArchiveRule
{
    public override string RuleName => "IMAGE_FORMAT";

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

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia"))
            {
                if (!"Image".Equals(mm.AttrTrim("Type"), StringComparison.OrdinalIgnoreCase)) continue;
                var mediaFile = mm.LocValue("MediaFile");
                if (string.IsNullOrEmpty(mediaFile)) continue;
                var bytes = ctx.ResolveResBytes(path, res, mediaFile!);
                if (bytes == null)
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                        "图像文件不存在: " + mediaFile, mediaFile, "不存在", "BMP/JPEG/PNG/JBIG2/JPEG2000/TIFF"));
                    continue;
                }
                var fmt = Detect(bytes);
                if (fmt == null)
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                        "无法识别图像格式 (魔数不匹配): " + mediaFile, mediaFile, "未知", "BMP/JPEG/PNG/JBIG2/JPEG2000/TIFF"));
                }
                else if (!Allowed.Contains(fmt))
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                        "图像格式不在允许范围内: " + fmt, mediaFile, fmt, "BMP/JPEG/PNG/JBIG2/JPEG2000/TIFF"));
                }
            }
        }
        return violations;
    }

    private static string? Detect(byte[] header)
    {
        var n = Math.Min(header.Length, 12);
        foreach (var (name, magic) in MagicTable)
        {
            if (n < magic.Length) continue;
            var ok = true;
            for (var i = 0; i < magic.Length; i++)
            {
                if (header[i] != magic[i]) { ok = false; break; }
            }
            if (ok) return name;
        }
        return null;
    }
}

/// <summary>规则 11：字体必须嵌入字型文件（GB/T 42133-2022 6.2.6b）。</summary>
public sealed class FontSubsetRule : ArchiveRule
{
    public override string RuleName => "FONT_SUBSET";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            foreach (var font in res.LocalDescendants("Font"))
            {
                var fontFile = font.LocValue("FontFile");
                var id = font.AttrTrim("ID") ?? "?";
                if (string.IsNullOrEmpty(fontFile))
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                        "字体未嵌入字型文件 (ID=" + id + ")", path, "缺失", "嵌入字型文件"));
                    continue;
                }
                if (ctx.ResolveResBytes(path, res, fontFile!) == null)
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                        "字体文件不存在: " + fontFile, fontFile, "文件不存在", "有效字体文件"));
                }
            }
        }
        return violations;
    }
}

/// <summary>规则 12：禁止音频视频资源（GB/T 42133-2022 6.2.6g）。</summary>
public sealed class AudioVideoRule : ArchiveRule
{
    public override string RuleName => "AUDIO_VIDEO";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia"))
            {
                var type = mm.AttrTrim("Type");
                if ("Audio".Equals(type, StringComparison.OrdinalIgnoreCase)
                    || "Video".Equals(type, StringComparison.OrdinalIgnoreCase))
                {
                    var id = mm.AttrTrim("ID") ?? "?";
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                        "OFD-A 不允许" + type + "资源 (ID=" + id + ")", mm.LocValue("MediaFile") ?? path, type, "删除"));
                }
            }
        }
        return violations;
    }
}

/// <summary>规则 13：图像资源禁止使用扩展机制加入自定义数据（GB/T 42133-2022 6.2.6f）。</summary>
public sealed class ImageExtensionRule : ArchiveRule
{
    public override string RuleName => "IMAGE_EXTENSION";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia"))
            {
                if (!"Image".Equals(mm.AttrTrim("Type"), StringComparison.OrdinalIgnoreCase)) continue;
                foreach (var child in mm.Elements())
                {
                    var ns = child.Name.Namespace;
                    // 非空且非 OFD 命名空间视为自定义扩展
                    if (ns != XNamespace.None && ns.NamespaceName != "http://www.ofdspec.org/2016")
                    {
                        violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                            "图像资源包含自定义扩展数据: " + child.Name.LocalName,
                            mm.LocValue("MediaFile") ?? path, child.Name.LocalName, "删除"));
                    }
                }
            }
        }
        return violations;
    }
}

/// <summary>规则 18：资源应在正确位置定义（GB/T 42133-2022 6.2.6a）。</summary>
public sealed class ResourcePlacementRule : ArchiveRule
{
    public override string RuleName => "RESOURCE_PLACEMENT";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            var fname = Path.GetFileName(path) ?? "";
            // PublicRes 不应包含 MultiMedia / CompositeGraphicUnits / DrawParams
            if (fname.StartsWith("PublicRes", StringComparison.OrdinalIgnoreCase))
            {
                if (res.LocalElem("MultiMedias") != null)
                    violations.Add(Warn("MultiMedia 资源应放在 DocumentRes 或 PageRes，而非 PublicRes", path));
                if (res.LocalElem("CompositeGraphicUnits") != null)
                    violations.Add(Warn("VectorG 资源应放在 DocumentRes 或 PageRes，而非 PublicRes", path));
                if (res.LocalElem("DrawParams") != null)
                    violations.Add(Warn("DrawParam 应放在 DocumentRes 或 PageRes，而非 PublicRes", path));
            }
            // PageRes 不应包含 ColorSpace / Font（应放 PublicRes）
            else if (fname.StartsWith("PageRes", StringComparison.OrdinalIgnoreCase))
            {
                if (res.LocalElem("ColorSpaces") != null)
                    violations.Add(Warn("ColorSpace 应放在 PublicRes，而非 PageRes", path));
                if (res.LocalElem("Fonts") != null)
                    violations.Add(Warn("Font 应放在 PublicRes，而非 PageRes", path));
            }
        }
        return violations;
    }

    private static ArchiveViolation Warn(string desc, string path)
        => new("RESOURCE_PLACEMENT", ArchiveViolation.Severity.Warn, desc, path, path, "正确位置");
}

/// <summary>规则 19：多页共用的栅格图像应在文档资源中注册（GB/T 42133-2022 6.5a）。</summary>
public sealed class ImageResourceRegRule : ArchiveRule
{
    public override string RuleName => "IMAGE_RESOURCE_REG";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        var imageRefs = new Dictionary<string, HashSet<int>>(StringComparer.OrdinalIgnoreCase);

        var pageNum = 0;
        foreach (var (_, page) in ctx.Pages())
        {
            pageNum++;
            CollectImageRefs(page, pageNum, imageRefs);
        }

        // 建立「MultiMedia ID → 定义所在资源文件」映射，用于判定是否已注册到 DocumentRes
        var definedIn = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (path, res) in ctx.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia"))
            {
                var id = mm.AttrTrim("ID");
                if (!string.IsNullOrEmpty(id)) definedIn[id!] = path;
            }
        }

        // 被 ≥2 页面共用的图像，必须注册在 DocumentRes 中；定义在 PageRes 或未定义则告警
        foreach (var kv in imageRefs)
        {
            if (kv.Value.Count < 2) continue;
            definedIn.TryGetValue(kv.Key, out var defPath);
            var defFile = defPath == null ? null : Path.GetFileName(defPath);
            if (defFile != null && defFile.StartsWith("DocumentRes", StringComparison.OrdinalIgnoreCase))
                continue; // 已正确注册
            violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                "图像资源 (ID=" + kv.Key + ") 被 " + kv.Value.Count + " 个页面引用，应注册到 DocumentRes",
                defPath, defFile ?? "未定义", "DocumentRes"));
        }
        return violations;
    }

    private static void CollectImageRefs(XElement container, int num, Dictionary<string, HashSet<int>> map)
    {
        foreach (var child in container.Elements())
        {
            if (child.Name.LocalName == "ImageObject")
            {
                var resId = child.AttrTrim("ResourceID");
                if (resId != null) map.GetOrAdd(resId).Add(num);
            }
            CollectImageRefs(child, num, map);
        }
    }
}

internal static class DictionaryExt
{
    public static HashSet<int> GetOrAdd(this Dictionary<string, HashSet<int>> d, string key)
    {
        if (!d.TryGetValue(key, out var set))
        {
            set = new HashSet<int>();
            d[key] = set;
        }
        return set;
    }
}
