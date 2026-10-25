using System;
using System.Collections.Generic;
using System.Linq;

namespace NetOfd.Archive.Rule;

/// <summary>规则：注释合规检查（GB/T 42133-2022 6.10.1）。</summary>
public sealed class AnnotationRule : ArchiveRule
{
    public override string RuleName => "ANNOTATION";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (docPath, doc) in ctx.Documents())
        {
            var loc = doc.LocalElem("Annotations")?.Value.Trim();
            if (string.IsNullOrEmpty(loc)) continue;
            var annPath = ArchiveContext.Combine(ArchiveContext.DirOf(docPath), loc);
            var ann = ctx.Xml(annPath);
            if (ann == null) continue;
            // 真实 OFD 为 <Annotations><Page PageID=..><Annot/></Page></Annotations>，需深层遍历
            foreach (var annot in ann.LocalDescendants("Annot"))
            {
                var id = annot.AttrTrim("ID") ?? "?";
                var loc2 = loc!;
                if (!IsTrue(annot.AttrTrim("ReadOnly")))
                    violations.Add(Warn("注释 ReadOnly 应为 true (ID=" + id + ")", loc2));
                if (!IsTrue(annot.AttrTrim("NoZoom")))
                    violations.Add(Warn("注释 NoZoom 应为 true (ID=" + id + ")", loc2));
                if (!IsTrue(annot.AttrTrim("NoRotate")))
                    violations.Add(Warn("注释 NoRotate 应为 true (ID=" + id + ")", loc2));
                var appearance = annot.LocalElem("Appearance");
                if (appearance != null && appearance.LocalElems("PageBlock").Any())
                {
                    var nested = appearance.LocalDescendants("PageBlock").Count();
                    violations.Add(Warn("注释 Appearance 中存在 PageBlock 嵌套 (ID=" + id + ")，共 " + nested + " 层",
                        loc2));
                }
            }
        }
        return violations;
    }

    private static bool IsTrue(string? v)
        => "true".Equals(v, StringComparison.OrdinalIgnoreCase);

    private static ArchiveViolation Warn(string desc, string loc)
        => new("ANNOTATION", ArchiveViolation.Severity.Warn, desc, loc, "非 true", "true");
}

/// <summary>规则：附件合规检查（GB/T 42133-2022 6.15）。</summary>
public sealed class AttachmentRule : ArchiveRule
{
    public override string RuleName => "ATTACHMENT";

    private static readonly HashSet<string> KeepFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "TXT", "XML"
    };

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (docPath, doc) in ctx.Documents())
        {
            var loc = doc.LocalElem("Attachments")?.Value.Trim();
            if (string.IsNullOrEmpty(loc)) continue;
            var attPath = ArchiveContext.Combine(ArchiveContext.DirOf(docPath), loc);
            var att = ctx.Xml(attPath);
            if (att == null) continue;
            foreach (var a in att.LocalDescendants("Attachment"))
            {
                var format = a.AttrTrim("Format");
                // GB/T 33190 CT_Attachment 的名称是 Name 属性；兼容个别实现使用的 AttachmentName 子元素
                var name = a.AttrTrim("Name") ?? a.LocalElem("AttachmentName")?.Value.Trim() ?? "?";
                if (format == null || !KeepFormats.Contains(format))
                {
                    violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                        "附件 \"" + name + "\" 格式为 " + (format ?? "未知")
                        + "，OFD-A 建议移出并以摘要信息替代", loc,
                        format, "TXT/XML"));
                }
            }
        }
        return violations;
    }
}

/// <summary>规则：数字签名/签章的去技术化（GB/T 42133-2022 6.13）。</summary>
public sealed class SignatureRule : ArchiveRule
{
    public override string RuleName => "SIGNATURE";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        var sigs = ctx.Signatures();
        if (sigs == null) return violations;
        foreach (var sig in sigs.LocalDescendants("Signature"))
        {
            var id = sig.AttrTrim("ID") ?? "?";
            var type = sig.AttrTrim("Type");
            if ("Sign".Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                    "存在数字签名(ID=" + id + ")，OFD-A 需去技术化: 提取签名信息→注释参数→删除SignRef→从签名列表移除",
                    "/Signs/Signatures.xml", "Sign", "去技术化"));
            }
            else
            {
                violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                    "存在签章(ID=" + id + ")，OFD-A 需去技术化: 外观→注释(BlendMode=Darken)→保留签章人/证书/时间/摘要→移除签名列表",
                    "/Signs/Signatures.xml", "Seal", "去技术化"));
            }
        }
        return violations;
    }
}
