using System;
using System.Collections.Generic;
using System.Linq;
using NetOfd.Archive.Rule;

namespace NetOfd.Archive;

/// <summary>
/// OFD-A 合规检查器（GB/T 42133-2022 长期保存要求）。
/// <para>聚合全部检查规则，对 OFD 文档执行全面的合规检查。可通过构造器自定义规则集。
/// 单条规则异常不影响其他规则执行。对齐 Java OFDArchiveChecker。</para>
/// </summary>
public class OFDArchiveChecker
{
    private readonly List<ArchiveRule> _rules;

    /// <summary>创建检查器，加载全部默认规则。</summary>
    public OFDArchiveChecker() : this(LoadDefaultRules()) { }

    /// <summary>创建检查器，使用自定义规则集。</summary>
    public OFDArchiveChecker(List<ArchiveRule> rules)
    {
        _rules = new List<ArchiveRule>(rules);
    }

    /// <summary>对 OFD 文件执行检查（自动管理 ArchiveContext 生命周期）。</summary>
    public IReadOnlyList<ArchiveViolation> Check(string ofdPath)
    {
        using var ctx = new ArchiveContext(ofdPath);
        return Check(ctx);
    }

    /// <summary>对 OFD 字节执行检查。</summary>
    public IReadOnlyList<ArchiveViolation> Check(byte[] ofdBytes)
    {
        using var ctx = new ArchiveContext(ofdBytes);
        return Check(ctx);
    }

    /// <summary>对已打开的 OFD 文档执行全部检查，收集违规项（按 RuleName 排序）。</summary>
    public IReadOnlyList<ArchiveViolation> Check(ArchiveContext ctx)
    {
        var all = new List<ArchiveViolation>();
        foreach (var rule in _rules)
        {
            try
            {
                var violations = rule.Check(ctx);
                if (violations != null) all.AddRange(violations);
            }
            catch (Exception e)
            {
                // 单条规则异常不影响其他规则执行
                all.Add(new ArchiveViolation(
                    rule.GetType().Name,
                    ArchiveViolation.Severity.Error,
                    "规则执行异常: " + e.Message));
            }
        }
        all.Sort((a, b) => string.CompareOrdinal(a.RuleName, b.RuleName));
        return all;
    }

    /// <summary>加载默认规则集（GB/T 42133-2022 Phase 1 全部检查规则）。</summary>
    private static List<ArchiveRule> LoadDefaultRules()
    {
        return new List<ArchiveRule>
        {
            // 文件结构规则
            new DocTypeRule(),            // 1: DocType="OFD-A"
            new SingleDocRule(),          // 2: 单文档
            new ExternalResourceRule(),   // 3: 禁止外部资源
            new PermissionRule(),         // 4: 去除权限
            new NonGotoActionRule(),      // 6: 去除非Goto动作
            new OutlineActionRule(),      // 7: 大纲动作检查
            new ExtensionRule(),          // 8: 去除扩展
            // 资源规则
            new ColorSpaceRule(),         // 9: 颜色空间检查
            new ImageFormatRule(),        // 10: 图像格式检查
            new FontSubsetRule(),         // 11: 字体嵌入检查
            new AudioVideoRule(),         // 12: 禁止音视频
            new ImageExtensionRule(),     // 13: 禁止图像扩展
            new ResourcePlacementRule(),  // 18: 资源位置检查
            new ImageResourceRegRule(),   // 19: 图像注册检查
            new ColorProfileRule(),       // 24: 颜色配置建议
            new TextHScaleRule(),         // 25: HScale检查
            // 页面内容规则
            new PageBlockDepthRule(),     // 14: PageBlock嵌套
            new ClipAreaRule(),           // 15: 裁剪区检查
            // 注释/签名/附件/内容规则
            new AnnotationRule(),         // 注释合规
            new SignatureRule(),          // 签名去技术化
            new AttachmentRule(),         // 附件合规
            new ImageInterpolateRule(),   // 17: 图像插值
            new TextSizeRule(),           // 23: 文字Size
        };
    }
}
