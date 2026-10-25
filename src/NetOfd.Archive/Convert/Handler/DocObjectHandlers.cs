using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;

namespace NetOfd.Archive.Convert.Handler;

/// <summary>
/// 处理器 21：注释合规转换（GB/T 42133-2022 6.10.1）。
/// <para>为每条注释设置 ReadOnly / NoZoom / NoRotate = true，并把 Appearance 内的 PageBlock 嵌套彻底展平
/// （子元素原地上提、Z 序不变，空壳 PageBlock 删除）。按需求<b>不</b>固化外观。</para>
/// <para>关于 6.10.2「归档后注释」：管理部门追加的归档章、页码等注释应与原有注释分文件存放，
/// 由生成软件负责，本处理器只处理归档前已存在的注释。</para>
/// 对应 Java: AnnotationHandler
/// </summary>
public sealed class AnnotationHandler : ArchiveHandler
{
    public override string HandlerName => "ANNOTATION";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (annPath, ann) in DocEntryLocator.Locate(ws, "Annotations"))
        {
            _ = annPath;
            foreach (var annot in ann.LocalDescendants("Annot"))
            {
                annot.SetAttributeValue("ReadOnly", "true");
                annot.SetAttributeValue("NoZoom", "true");
                annot.SetAttributeValue("NoRotate", "true");

                var appearance = annot.LocalElem("Appearance");
                if (appearance != null) FlattenPageBlocks(appearance);
            }
        }
    }

    /// <summary>
    /// 把 Appearance 下所有层级的 PageBlock 全部拆开。
    /// <para>每轮取文档序中的首个 PageBlock，将其子元素原地上提后删除自身；
    /// 深层块会在后续轮次被再次命中，最终 Appearance 内不再残留任何 PageBlock。</para>
    /// </summary>
    private static void FlattenPageBlocks(XElement appearance)
    {
        while (true)
        {
            var pb = appearance.LocalDescendants("PageBlock").FirstOrDefault();
            if (pb == null) return;
            var children = pb.Elements().ToList();
            foreach (var c in children) c.Remove();
            if (children.Count > 0) pb.AddBeforeSelf(children);
            pb.Remove();
        }
    }
}

/// <summary>
/// 处理器 22：数字签名/签章去技术化（GB/T 42133-2022 6.13）。
/// <para>OFD-A 不保留签名技术数据：逐条清除 Signatures.xml 中的签名记录、删除对应的 Sign_N 描述目录，
/// 最后删除 Signatures.xml 本身并摘掉 DocBody 上的 &lt;Signatures&gt; 引用。</para>
/// <para>说明：签章外观转注释、签名信息转注释自定义参数属于「生成端」职责（需要重新渲染外观），
/// 这里与 Java 版保持一致，只做技术数据剥离。</para>
/// 对应 Java: SignatureHandler
/// </summary>
public sealed class SignatureHandler : ArchiveHandler
{
    public override string HandlerName => "SIGNATURE";

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var body in ws.DocBodies().ToList())
        {
            var sigElem = body.LocalElem("Signatures");
            var loc = sigElem?.Value.Trim();
            if (string.IsNullOrEmpty(loc)) continue;

            var sigPath = ArchiveContext.Normalize(loc!);
            var signsDir = ArchiveContext.DirOf(sigPath);

            var sigsRoot = ws.Xml(sigPath);
            if (sigsRoot != null)
            {
                foreach (var sig in sigsRoot.LocalDescendants("Signature").ToList())
                {
                    DeleteSignEntry(ws, signsDir, sig.LocValue("BaseLoc"));
                    sig.Remove();
                }
            }

            // 删除签名入口文件；若整个 Signs 目录已无用则一并清除
            ws.Delete(sigPath);
            if (IsSignsDir(signsDir)) ws.DeleteDirectory(signsDir);

            sigElem!.Remove();
        }
    }

    /// <summary>删除单条签名的描述目录（Sign_N/），BaseLoc 为文件时退化为删除该文件。</summary>
    private static void DeleteSignEntry(ArchiveWorkspace ws, string signsDir, string? baseLoc)
    {
        if (string.IsNullOrEmpty(baseLoc)) return;
        var full = ArchiveContext.Combine(signsDir, baseLoc!);
        var dir = ArchiveContext.DirOf(full);
        // BaseLoc 通常形如 "Sign_0/Signature.xml"，其父目录即待删除的签名目录
        if (dir != "/" && !string.Equals(dir, signsDir, StringComparison.OrdinalIgnoreCase))
            ws.DeleteDirectory(dir);
        else
            ws.Delete(full);
    }

    /// <summary>仅当目录名确为 Signs 时才允许整体删除，避免误删文档目录。</summary>
    private static bool IsSignsDir(string dir)
    {
        if (dir == "/" ) return false;
        var last = dir.TrimEnd('/');
        var i = last.LastIndexOf('/');
        if (i >= 0) last = last.Substring(i + 1);
        return "Signs".Equals(last, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// 处理器 23：附件处理（GB/T 42133-2022 6.15）。
/// <para>仅保留纯文本附件（TXT / XML），其余附件连同包内文件一并移出；
/// 附件列表清空后删除 Attachments.xml 并摘掉 Document 上的 &lt;Attachments&gt; 引用。</para>
/// 对应 Java: AttachmentHandler
/// </summary>
public sealed class AttachmentHandler : ArchiveHandler
{
    public override string HandlerName => "ATTACHMENT";

    /// <summary>允许保留的附件格式。</summary>
    private static readonly HashSet<string> KeepFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "TXT", "XML"
    };

    public override void Handle(ArchiveWorkspace ws)
    {
        foreach (var (attPath, att, doc, elem) in DocEntryLocator.LocateWithOwner(ws, "Attachments"))
        {
            _ = doc;
            foreach (var a in att.LocalDescendants("Attachment").ToList())
            {
                var format = a.AttrTrim("Format");
                if (format != null && KeepFormats.Contains(format)) continue;

                var fileLoc = a.LocValue("FileLoc");
                if (!string.IsNullOrEmpty(fileLoc))
                    ws.Delete(ArchiveContext.Combine(ArchiveContext.DirOf(attPath), fileLoc!));

                a.Remove();
            }

            // 附件全部移出后，入口文件与引用一并回收
            if (!att.LocalDescendants("Attachment").Any())
            {
                ws.Delete(attPath);
                elem.Remove();
            }
        }
    }
}

/// <summary>Document 级入口文件（Annotations / Attachments 等）的定位共用实现。</summary>
internal static class DocEntryLocator
{
    /// <summary>定位各文档下由 <paramref name="elemName"/> 声明的入口文件 (路径, 根)。</summary>
    public static List<(string Path, XElement Root)> Locate(ArchiveWorkspace ws, string elemName)
        => LocateWithOwner(ws, elemName).Select(t => (t.Path, t.Root)).ToList();

    /// <summary>定位入口文件，并一并返回其所属 Document 根与声明元素（便于回收引用）。</summary>
    public static List<(string Path, XElement Root, XElement Doc, XElement Elem)> LocateWithOwner(
        ArchiveWorkspace ws, string elemName)
    {
        var list = new List<(string, XElement, XElement, XElement)>();
        foreach (var (docPath, doc) in ws.Documents())
        {
            foreach (var elem in doc.LocalElems(elemName).ToList())
            {
                var loc = elem.Value.Trim();
                if (string.IsNullOrEmpty(loc)) continue;
                var path = ArchiveContext.Combine(ArchiveContext.DirOf(docPath), loc);
                var root = ws.Xml(path);
                if (root != null) list.Add((path, root, doc, elem));
            }
        }
        return list;
    }
}
