using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using NetOfd.Archive.Convert.Handler;
using NetOfd.Crypto;

namespace NetOfd.Archive.Convert;

/// <summary>
/// OFD-A 转换器：把普通 OFD 转换为符合 GB/T 42133-2022 的长期保存格式。
/// <para>转换流程：</para>
/// <list type="number">
///   <item>把源包读入可写工作区（zip + XML）</item>
///   <item>按编排顺序执行全部去技术化处理器</item>
///   <item>从 OFD.xml 出发 DFS 收集可达文件白名单</item>
///   <item>丢弃白名单之外的非法夹带条目</item>
///   <item>重新打包输出</item>
/// </list>
/// <para>用法：</para>
/// <code>
/// var converter = new OFDArchiveConverter();
/// converter.AddDecryptor(new UserPasswordDecryptor("张三", "12345678")); // 仅加密文档需要
/// converter.Convert("input.ofd", "output.ofd");
/// </code>
/// 对应 Java: org.ofdrw.archive.convert.OFDArchiveConverter
/// </summary>
public sealed class OFDArchiveConverter
{
    private readonly List<ArchiveHandler> _handlers;
    private readonly EncryptionHandler? _encryptionHandler;

    /// <summary>创建转换器并加载默认处理器管道。</summary>
    public OFDArchiveConverter()
    {
        _encryptionHandler = new EncryptionHandler();
        _handlers = LoadDefaultHandlers(_encryptionHandler);
    }

    /// <summary>创建转换器并使用自定义处理器管道（按给定顺序执行）。</summary>
    public OFDArchiveConverter(IEnumerable<ArchiveHandler> handlers)
    {
        _handlers = handlers?.ToList() ?? throw new ArgumentNullException(nameof(handlers));
        _encryptionHandler = _handlers.OfType<EncryptionHandler>().FirstOrDefault();
    }

    /// <summary>当前处理器管道（只读）。</summary>
    public IReadOnlyList<ArchiveHandler> Handlers => _handlers;

    /// <summary>
    /// 添加 FEK 解密器，用于转换加密 OFD；明文文档无需调用。
    /// </summary>
    /// <exception cref="InvalidOperationException">自定义管道中不含 EncryptionHandler 时抛出。</exception>
    public OFDArchiveConverter AddDecryptor(IUserFekDecryptor decryptor)
    {
        if (_encryptionHandler == null)
        {
            throw new InvalidOperationException(
                "自定义处理器管道中无 EncryptionHandler，无法添加解密器。"
                + "请在管道中加入 EncryptionHandler 实例并直接调用其 AddDecryptor。");
        }
        _encryptionHandler.AddDecryptor(decryptor);
        return this;
    }

    /// <summary>转换 OFD 文件，输出 OFD-A；源文件不受影响。</summary>
    public void Convert(string srcPath, string dstPath)
    {
        if (string.IsNullOrEmpty(srcPath) || !File.Exists(srcPath))
            throw new ArgumentException("源文件不存在: " + srcPath, nameof(srcPath));
        if (string.IsNullOrEmpty(dstPath))
            throw new ArgumentException("输出路径为空", nameof(dstPath));

        using var ws = new ArchiveWorkspace(srcPath);
        Run(ws);
        ws.SaveTo(dstPath);
    }

    /// <summary>转换内存中的 OFD 字节，返回 OFD-A 字节。</summary>
    public byte[] Convert(byte[] ofdBytes)
    {
        if (ofdBytes == null) throw new ArgumentNullException(nameof(ofdBytes));
        using var ws = new ArchiveWorkspace(ofdBytes);
        Run(ws);
        return ws.ToBytes();
    }

    /// <summary>在既有工作区上原地执行完整管道 + 夹带清理（供测试与高级用法）。</summary>
    public void Run(ArchiveWorkspace ws)
    {
        foreach (var handler in _handlers) handler.Handle(ws);
        RemoveStowaways(ws);
    }

    // ---- 可达性分析 ----

    /// <summary>
    /// 从 OFD.xml 出发 DFS 收集可达条目，丢弃未被任何引用触及的非法夹带文件。
    /// </summary>
    internal static void RemoveStowaways(ArchiveWorkspace ws)
    {
        var reachable = CollectReachable(ws);
        if (reachable.Count == 0) return; // 入口缺失时不做任何删除，避免清空整包
        foreach (var key in ws.Keys)
            if (!reachable.Contains(key)) ws.Delete(key);
    }

    /// <summary>
    /// 收集可达条目集合。
    /// <para><b>修复 Java 缺陷</b>：Java 版用正则 <c>"(/?[A-Za-z0-9_/]+(\.[A-Za-z]+)?)"</c> 只能匹配<b>带引号</b>的
    /// 属性值，取不到 <c>&lt;DocRoot&gt;Doc_0/Document.xml&lt;/DocRoot&gt;</c> 这类元素文本，
    /// 会把整份文档判为不可达而丢失。这里改为结构化遍历「属性值 + 叶子节点文本」，
    /// 并按「绝对路径 → 相对当前文件 → 相对 BaseLoc」三种方式解析，命中包内条目才纳入。</para>
    /// </summary>
    internal static HashSet<string> CollectReachable(ArchiveWorkspace ws)
    {
        var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!ws.Exists("/OFD.xml")) return reachable;

        var stack = new Stack<string>();
        void Seed(string path)
        {
            var norm = ArchiveContext.Normalize(path);
            if (!ws.Exists(norm) || !reachable.Add(norm)) return;
            if (IsXml(norm)) stack.Push(norm);
        }

        Seed("/OFD.xml");
        // 结构入口一并作为种子：文档、页面、资源文件即便未被显式 ST_Loc 引用（如按命名约定放置的
        // PageRes_N.xml）也属于合法内容，不应被当作夹带清除。
        foreach (var (p, _) in ws.Documents()) Seed(p);
        foreach (var (p, _) in ws.Pages()) Seed(p);
        foreach (var (p, _) in ws.ResourceFiles()) Seed(p);

        while (stack.Count > 0)
        {
            var cur = stack.Pop();
            var root = ws.Xml(cur);
            if (root == null) continue;
            foreach (var raw in CandidateValues(root))
            {
                var target = Resolve(ws, cur, root, raw);
                if (target == null || !reachable.Add(target)) continue;
                if (IsXml(target)) stack.Push(target);
            }
        }
        return reachable;
    }

    private static bool IsXml(string path)
        => path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    /// <summary>候选引用值：全部非命名空间属性值 + 全部叶子节点文本。</summary>
    private static IEnumerable<string> CandidateValues(XElement root)
    {
        foreach (var e in root.DescendantsAndSelf())
        {
            foreach (var a in e.Attributes())
            {
                if (a.IsNamespaceDeclaration) continue;
                yield return a.Value;
            }
            if (e.HasElements) continue;
            var text = e.Value;
            if (!string.IsNullOrWhiteSpace(text)) yield return text;
        }
    }

    /// <summary>把候选值解析为包内条目路径；不像路径或不存在则返回 null。</summary>
    private static string? Resolve(ArchiveWorkspace ws, string curPath, XElement root, string raw)
    {
        var v = raw.Trim();
        if (v.Length < 2 || v.Length > 512) return null;
        if (v.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return null;
        if (v.IndexOf('/') < 0 && v.IndexOf('.') < 0) return null; // 既无目录也无扩展名，不可能是路径
        foreach (var ch in v)
            if (char.IsWhiteSpace(ch)) return null;

        var dir = ArchiveContext.DirOf(curPath);
        var abs = Collapse(ArchiveContext.Normalize(v));
        if (ws.Exists(abs)) return abs;

        var rel = Collapse(ArchiveContext.Combine(dir, v));
        if (ws.Exists(rel)) return rel;

        var baseLoc = root.AttrTrim("BaseLoc");
        if (!string.IsNullOrEmpty(baseLoc))
        {
            var viaBase = Collapse(ArchiveContext.Combine(ArchiveContext.Combine(dir, baseLoc!), v));
            if (ws.Exists(viaBase)) return viaBase;
        }
        return null;
    }

    /// <summary>折叠路径中的 "." 与 ".."（越出根时截断在根）。</summary>
    private static string Collapse(string path)
    {
        if (path.IndexOf('.') < 0) return path;
        var stack = new List<string>();
        foreach (var seg in path.Split('/'))
        {
            if (seg.Length == 0 || seg == ".") continue;
            if (seg == "..") { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); continue; }
            stack.Add(seg);
        }
        var sb = new StringBuilder();
        foreach (var s in stack) sb.Append('/').Append(s);
        return sb.Length == 0 ? "/" : sb.ToString();
    }

    // ---- 默认管道 ----

    /// <summary>
    /// 默认处理器管道，顺序至关重要：
    /// 解密 → 结构 → 元数据 → 动作 → 扩展 → 资源 → 页面内容 → 属性清理 → 注释 → 签名 → 附件。
    /// </summary>
    private static List<ArchiveHandler> LoadDefaultHandlers(EncryptionHandler encryptionHandler)
        => new()
        {
            // 0. 文件解密（必须最先，加密未解则中断）
            encryptionHandler,

            // 1. 结构变更
            new SingleDocHandler(),
            new DocTypeHandler(),

            // 2. 元数据清理
            new ExternalResourceHandler(),
            new PermissionHandler(),
            new VPrefsHandler(),

            // 3. 动作清理
            new NonGotoActionHandler(),
            new OutlineActionHandler(),

            // 4. 扩展清理
            new ExtensionHandler(),

            // 5. 资源处理
            new AudioVideoHandler(),
            new ImageExtensionHandler(),
            new ResourcePlacementHandler(),
            new ImageResourceRegHandler(),

            // 6. 页面内容处理
            new PageBlockFlattenHandler(),
            new ClipAreaHandler(),
            new ImageConvertHandler(),
            new LayerNameHandler(),
            new ImageInterpolateHandler(),

            // 7. 属性清理
            new CleanStrokeAttrHandler(),
            new CleanFillAttrHandler(),

            // 8. 注释
            new AnnotationHandler(),

            // 9. 签名/签章去技术化
            new SignatureHandler(),

            // 10. 附件
            new AttachmentHandler(),
        };
}
