using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NetOfd.Archive.Rule;

/// <summary>规则 1：DocType 必须为 "OFD-A"（GB/T 42133-2022 6.2.1a）。</summary>
public sealed class DocTypeRule : ArchiveRule
{
    public override string RuleName => "DOC_TYPE";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var ofd = ctx.OfdRoot;
        if (ofd == null)
        {
            return new[] { new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                "无法读取 OFD.xml", "/OFD.xml") };
        }
        var docType = ofd.AttrTrim("DocType");
        if (!"OFD-A".Equals(docType, StringComparison.OrdinalIgnoreCase))
        {
            return new[] { new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                "DocType 必须为 \"OFD-A\"，当前为: " + (docType == null ? "缺失" : "\"" + docType + "\""),
                "/OFD.xml", docType, "OFD-A") };
        }
        return Array.Empty<ArchiveViolation>();
    }
}

/// <summary>规则 2：OFD 文件只能包含一个文档（GB/T 42133-2022 6.2.1c）。</summary>
public sealed class SingleDocRule : ArchiveRule
{
    public override string RuleName => "SINGLE_DOC";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var bodies = ctx.DocBodies();
        var count = 0;
        foreach (var _ in bodies) count++;
        if (count > 1)
        {
            return new[] { new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                "OFD 文件包含 " + count + " 个文档体，OFD-A 只允许单个文档",
                "/OFD.xml", count.ToString(), "1") };
        }
        return Array.Empty<ArchiveViolation>();
    }
}

/// <summary>规则 3：资源必须自包含，禁止外部引用（GB/T 42133-2022 6.1）。</summary>
public sealed class ExternalResourceRule : ArchiveRule
{
    public override string RuleName => "EXTERNAL_RESOURCE";

    // 负向后顾排除 xmlns / xmlns:prefix 声明，避免把 OFD 默认命名空间 URI 误判为外部资源。
    // 注：Java 原版同样用正则扫描原文，会误报命名空间声明，这里做了修正。
    private static readonly Regex UrlRegex = new(@"(?<!xmlns[\w:]*=)""(https?://[^""]+)""", RegexOptions.Compiled);

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (path, text) in ctx.XmlTexts())
        {
            var m = UrlRegex.Match(text);
            while (m.Success)
            {
                var url = m.Groups[1].Value;
                violations.Add(new ArchiveViolation(RuleName, ArchiveViolation.Severity.Error,
                    "引用外部 URL 资源: " + url, path, url, "包内路径"));
                m = m.NextMatch();
            }
        }
        return violations;
    }
}

/// <summary>规则 4：去除权限声明（GB/T 42133-2022 6.2.2a）。</summary>
public sealed class PermissionRule : ArchiveRule
{
    public override string RuleName => "PERMISSION";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        foreach (var (_, doc) in ctx.Documents())
        {
            // Java Document.getPermission() 读取的是 <Permissions> 元素（Document 直接子元素）
            if (doc.LocalElem("Permissions") != null)
            {
                return new[] { new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                    "文档包含权限声明，OFD-A 建议去除", "/Document.xml", "存在", "无") };
            }
        }
        return Array.Empty<ArchiveViolation>();
    }
}

/// <summary>规则 8：去除扩展信息（GB/T 42133-2022 6.2.2e）。</summary>
public sealed class ExtensionRule : ArchiveRule
{
    public override string RuleName => "EXTENSION";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        foreach (var (_, doc) in ctx.Documents())
        {
            if (doc.LocalElem("Extensions") != null)
            {
                return new[] { new ArchiveViolation(RuleName, ArchiveViolation.Severity.Warn,
                    "文档包含扩展信息，OFD-A 要求去除", "/Document.xml", "存在", "无") };
            }
        }
        return Array.Empty<ArchiveViolation>();
    }
}

/// <summary>规则 6：Document 中不得包含非 Goto 动作（GB/T 42133-2022 6.2.2c）。</summary>
public sealed class NonGotoActionRule : ArchiveRule
{
    public override string RuleName => "NON_GOTO_ACTION";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (_, doc) in ctx.Documents())
        {
            var actions = doc.LocalElem("Actions");
            if (actions == null) continue;
            CheckActions(actions, violations, "NON_GOTO_ACTION", "/Document.xml");
        }
        return violations;
    }

    internal static void CheckActions(XElement actions, List<ArchiveViolation> violations, string ruleName, string loc)
    {
        foreach (var action in actions.LocalElems("Action"))
        {
            // Goto 动作：包含 <Goto> 子元素或 <Type>Goto</Type>
            var isGoto = action.LocalElem("Goto") != null
                || "Goto".Equals(action.LocalElem("Type")?.Value.Trim(), StringComparison.OrdinalIgnoreCase);
            if (!isGoto)
            {
                var type = action.LocalElem("Type")?.Value.Trim() ?? action.AttrTrim("Type") ?? "未知";
                violations.Add(new ArchiveViolation(ruleName, ArchiveViolation.Severity.Error,
                    "包含非 Goto 动作: " + type, loc, type, "Goto"));
            }
        }
    }
}

/// <summary>规则 7：大纲节点动作检查（GB/T 42133-2022 6.2.5a/b）。</summary>
public sealed class OutlineActionRule : ArchiveRule
{
    public override string RuleName => "OUTLINE_ACTION";

    public override IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var violations = new List<ArchiveViolation>();
        foreach (var (_, doc) in ctx.Documents())
        {
            var outlines = doc.LocalElem("Outlines");
            if (outlines == null) continue;
            // 大纲可多级嵌套，需逐层遍历
            foreach (var elem in outlines.LocalDescendants("OutlineElem"))
            {
                var actions = elem.LocalElem("Actions");
                if (actions != null) NonGotoActionRule.CheckActions(actions, violations, "OUTLINE_ACTION", "/Document.xml");
            }
        }
        return violations;
    }
}
