using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Crypto;

namespace NetOfd.Archive.Convert.Handler;

/// <summary>
/// 处理器 0：文件解密（必须最先执行）。
/// <para>包内存在 Encryptions.xml 时，用配置的解密器还原明文包；未配置解密器则中断转换。</para>
/// 对应 Java: EncryptionHandler
/// </summary>
public sealed class EncryptionHandler : ArchiveHandler
{
    private readonly List<IUserFekDecryptor> _decryptors = new();

    public override string HandlerName => "ENCRYPTION";

    /// <summary>添加 FEK 解密器（口令或证书）。</summary>
    public EncryptionHandler AddDecryptor(IUserFekDecryptor decryptor)
    {
        if (decryptor is not null) _decryptors.Add(decryptor);
        return this;
    }

    public override void Handle(ArchiveWorkspace ws)
    {
        if (!ws.Exists("/Encryptions.xml")) return;
        if (_decryptors.Count == 0)
        {
            throw new InvalidOperationException(
                "文档已加密但未配置解密器，无法继续转换。请通过 OFDArchiveConverter.AddDecryptor() 提供解密密钥。");
        }

        var srcTmp = Path.Combine(Path.GetTempPath(), "ofd-arch-enc-" + Guid.NewGuid().ToString("N") + ".ofd");
        var dstTmp = Path.Combine(Path.GetTempPath(), "ofd-arch-dec-" + Guid.NewGuid().ToString("N") + ".ofd");
        try
        {
            ws.SaveTo(srcTmp);
            using (var dec = new OfdDecryptor(srcTmp, dstTmp))
            {
                foreach (var d in _decryptors) dec.AddUser(d);
                dec.Decrypt();
            }
            ws.ReloadFrom(File.ReadAllBytes(dstTmp));
        }
        catch (CryptoException e)
        {
            throw new IOException("解密失败: " + e.Message, e);
        }
        finally
        {
            TryDelete(srcTmp);
            TryDelete(dstTmp);
        }
    }

    private static void TryDelete(string p)
    {
        try { if (File.Exists(p)) File.Delete(p); } catch (IOException) { /* 临时文件清理失败可忽略 */ }
    }
}

/// <summary>
/// 处理器 1：单文档化（GB/T 42133-2022 6.2.1c）。
/// <para>仅保留首个 DocBody，其余 DocBody 及其文档目录一并删除。</para>
/// 对应 Java: SingleDocHandler
/// </summary>
public sealed class SingleDocHandler : ArchiveHandler
{
    public override string HandlerName => "SINGLE_DOC";

    public override void Handle(ArchiveWorkspace ws)
    {
        var ofd = ws.OfdRoot;
        if (ofd == null) return;
        var bodies = ofd.LocalElems("DocBody").ToList();
        if (bodies.Count <= 1) return;

        for (var i = 1; i < bodies.Count; i++)
        {
            var docRoot = bodies[i].LocalElem("DocRoot")?.Value.Trim();
            if (!string.IsNullOrEmpty(docRoot))
            {
                var seg = ArchiveContext.Normalize(docRoot!).TrimStart('/');
                var idx = seg.IndexOf('/');
                if (idx > 0) ws.DeleteDirectory("/" + seg.Substring(0, idx));
            }
            bodies[i].Remove();
        }
    }
}

/// <summary>
/// 处理器 2：DocType 置为 OFD-A（GB/T 42133-2022 6.2.1a）。
/// 对应 Java: DocTypeHandler
/// </summary>
public sealed class DocTypeHandler : ArchiveHandler
{
    public override string HandlerName => "DOC_TYPE";

    public override void Handle(ArchiveWorkspace ws)
        => ws.OfdRoot?.SetAttributeValue("DocType", "OFD-A");
}

/// <summary>
/// 处理器 3：去除权限声明（GB/T 42133-2022 6.2.2a）。
/// <para>注意 Document 中的元素名为 &lt;Permissions&gt;（复数）。</para>
/// 对应 Java: PermissionHandler
/// </summary>
public sealed class PermissionHandler : ArchiveHandler
{
    public override string HandlerName => "PERMISSION";

    public override void Handle(ArchiveWorkspace ws)
        => DocElementRemover.Remove(ws, "Permissions");
}

/// <summary>
/// 处理器 4：去除视图首选项（GB/T 42133-2022 6.2.2b）。
/// 对应 Java: VPrefsHandler
/// </summary>
public sealed class VPrefsHandler : ArchiveHandler
{
    public override string HandlerName => "VPREFS";

    public override void Handle(ArchiveWorkspace ws)
        => DocElementRemover.Remove(ws, "VPreferences");
}

/// <summary>
/// 处理器 5：去除扩展信息（GB/T 42133-2022 6.2.2e）。
/// 对应 Java: ExtensionHandler
/// </summary>
public sealed class ExtensionHandler : ArchiveHandler
{
    public override string HandlerName => "EXTENSION";

    public override void Handle(ArchiveWorkspace ws)
        => DocElementRemover.Remove(ws, "Extensions");
}

/// <summary>Document 根下同名元素批量移除的共用实现。</summary>
internal static class DocElementRemover
{
    public static void Remove(ArchiveWorkspace ws, string localName)
    {
        foreach (var (_, doc) in ws.Documents())
            foreach (var e in doc.LocalElems(localName).ToList())
                e.Remove();
    }
}

/// <summary>
/// 处理器 6：清理非 Goto 动作（GB/T 42133-2022 6.2.2c）。
/// <para>删除 Document 级 Actions；页面内逐层清理 Actions，仅保留 Goto/GotoA，清空后连同 Actions 一并删除。</para>
/// 对应 Java: NonGotoActionHandler
/// </summary>
public sealed class NonGotoActionHandler : ArchiveHandler
{
    public override string HandlerName => "NON_GOTO_ACTION";

    public override void Handle(ArchiveWorkspace ws)
    {
        // Document 级动作整体移除
        DocElementRemover.Remove(ws, "Actions");

        // 页面内动作逐个甄别
        foreach (var (_, page) in ws.Pages())
            foreach (var actions in page.LocalDescendants("Actions").ToList())
                CleanActions(actions);
    }

    /// <summary>仅保留 Goto/GotoA 动作；若清理后无有效动作则删除整个 Actions 节点。</summary>
    internal static void CleanActions(XElement actions)
    {
        var hasValid = false;
        foreach (var action in actions.LocalElems("Action").ToList())
        {
            if (IsGoto(action)) hasValid = true;
            else action.Remove();
        }
        if (!hasValid) actions.Remove();
    }

    /// <summary>Goto 判定：首个子元素名为 Goto/GotoA，或 &lt;Type&gt;Goto&lt;/Type&gt;，或 Type="Goto" 属性。</summary>
    internal static bool IsGoto(XElement action)
    {
        var first = action.Elements().FirstOrDefault();
        if (first != null)
        {
            var n = first.Name.LocalName;
            if ("Goto".Equals(n, StringComparison.OrdinalIgnoreCase)
                || "GotoA".Equals(n, StringComparison.OrdinalIgnoreCase)) return true;
        }
        if ("Goto".Equals(action.LocalElem("Type")?.Value.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        return "Goto".Equals(action.AttrTrim("Type"), StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 处理器 7：清理大纲中的非 Goto 动作（GB/T 42133-2022 6.2.5a/b）。
/// <para>大纲可多级嵌套，逐层遍历 OutlineElem。</para>
/// 对应 Java: OutlineActionHandler
/// </summary>
public sealed class OutlineActionHandler : ArchiveHandler
{
    public override string HandlerName => "OUTLINE_ACTION";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, doc) in ws.Documents())
        {
            foreach (var outlines in doc.LocalElems("Outlines").ToList())
                foreach (var elem in outlines.LocalDescendants("OutlineElem").ToList())
                    foreach (var actions in elem.LocalElems("Actions").ToList())
                        NonGotoActionHandler.CleanActions(actions);
        }
    }
}

/// <summary>
/// 处理器 8：清除外部资源引用（GB/T 42133-2022 6.1）。
/// <para>MediaFile 指向 http(s) 外链或越出包根的多媒体资源一律删除；同时清理各 XML 中残留的外链属性值。</para>
/// 对应 Java: ExternalResourceHandler
/// </summary>
public sealed class ExternalResourceHandler : ArchiveHandler
{
    public override string HandlerName => "EXTERNAL_RESOURCE";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (_, res) in ws.ResourceFiles())
        {
            foreach (var mm in res.LocalDescendants("MultiMedia").ToList())
            {
                var mediaFile = mm.LocValue("MediaFile");
                if (string.IsNullOrEmpty(mediaFile)) continue;
                if (IsExternal(mediaFile!) || EscapesRoot(mediaFile!)) mm.Remove();
            }
        }

        // 兜底：删除任何仍指向 http(s) 外链的属性（命名空间声明除外）
        foreach (var loc in ws.Keys)
        {
            if (!loc.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) continue;
            var root = ws.Xml(loc);
            if (root == null) continue;
            foreach (var e in root.DescendantsAndSelf().ToList())
            {
                foreach (var a in e.Attributes().ToList())
                {
                    if (a.IsNamespaceDeclaration) continue;
                    if (IsExternal(a.Value.Trim())) a.Remove();
                }
            }
        }
    }

    private static bool IsExternal(string v)
        => v.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
           || v.StartsWith("https://", StringComparison.OrdinalIgnoreCase);

    /// <summary>路径中出现上跳且跳出包根视为越界。</summary>
    private static bool EscapesRoot(string loc)
    {
        var depth = 0;
        foreach (var seg in loc.Replace('\\', '/').Split('/'))
        {
            if (seg == "..") { if (--depth < 0) return true; }
            else if (seg.Length > 0 && seg != ".") depth++;
        }
        return false;
    }
}
