using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.Attachment;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicType;

namespace NetOfd.Layout.Edit;

/// <summary>
/// OFD 附件编辑器：向已有 OFD 文档添加或删除附件。
/// <para>
/// 采用与 <see cref="OfdAnnotator"/> 一致的映射（Dictionary）方式读写包内文件：
/// 附件文件复制进文档资源目录（Doc_0/Res），声明写入 Attachments.xml，
/// 并在 Document.xml 中登记附件列表入口。
/// </para>
/// 对应 Java: org.ofdrw.layout.OFDDoc#addAttachment / #deleteAttachment
/// </summary>
public static class OfdAttacher
{
    /// <summary>向已有 OFD 字节添加附件，返回新的 OFD 字节。</summary>
    public static byte[] AddAttachment(byte[] inputOfd, Attachment attachment)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        AddAttachmentCore(map, attachment);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>向已有 OFD 文件添加附件，输出新的 OFD 文件。</summary>
    public static void AddAttachment(string inputPath, string outputPath, Attachment attachment)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        AddAttachmentCore(map, attachment);
        ZipMapIO.WriteZip(outputPath, map);
    }

    /// <summary>从已有 OFD 字节中删除指定名称的附件，返回新的 OFD 字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="name">附件名称，若附件不存在则忽略</param>
    public static byte[] DeleteAttachment(byte[] inputOfd, string name)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        DeleteAttachmentCore(map, name);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>从已有 OFD 文件中删除指定名称的附件，输出新的 OFD 文件。</summary>
    public static void DeleteAttachment(string inputPath, string outputPath, string name)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        DeleteAttachmentCore(map, name);
        ZipMapIO.WriteZip(outputPath, map);
    }

    // ── 核心实现 ─────────────────────────────────────────────────

    internal static void AddAttachmentCore(Dictionary<string, byte[]> map, Attachment attachment)
    {
        if (attachment is null)
            throw new ArgumentNullException(nameof(attachment));
        if (string.IsNullOrWhiteSpace(attachment.FilePath) || !File.Exists(attachment.FilePath))
            throw new ArgumentException("附件文件(file)为空或不存在", nameof(attachment));

        // ── 1. 解析 OFD.xml，定位文档目录 ─────────────────────────
        var (ofdRoot, docPath, docDirName) = LoadDocumentContext(map, out var document);

        // ── 2. 初始化最大对象 ID ──────────────────────────────────
        long maxUnitId =
            ofdRoot.GetFirstDocBody()?.DocInfo?.MaxUnitID?.Value
            ?? document.CommonData?.MaxUnitID?.Value
            ?? 0;

        // ── 3. 复制附件文件至文档资源目录（对齐 Java obtainRes）────
        // 说明：zip 包内条目键用全路径（含文档目录前缀），
        // 但附件对象上的 FileLoc 存为「相对文档根目录」的路径（不含 Doc_N/ 前缀），
        // 以便 OFDReader 以 Attachments.xml 所在目录为基准正确解析为 /Doc_N/Res/...。
        var fileName = Path.GetFileName(attachment.FilePath);
        var fileLoc = $"{docDirName}/Res/{fileName}";
        map[fileLoc] = File.ReadAllBytes(attachment.FilePath);

        // ── 4. 构造附件对象并填充默认值 ───────────────────────────
        var ctAttachment = attachment.GetAttachment();
        ctAttachment.ID = (++maxUnitId).ToString();
        if (string.IsNullOrEmpty(ctAttachment.CreationDate))
            ctAttachment.CreationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);
        ctAttachment.Size = map[fileLoc].Length / 1024d;
        ctAttachment.FileLoc = new STLoc($"Res/{fileName}");

        // ── 5. 获取或创建附件列表文件 ─────────────────────────────
        var (attPath, attachments) = ObtainAttachments(map, docDirName, document);

        // ── 6. 同名替换（除非禁用）────────────────────────────────
        if (!attachment.DisableReplace)
            CleanOldAttachment(map, attachments, attachment.GetName(), docDirName);

        // ── 7. 加入附件记录并回写 ─────────────────────────────────
        attachments.AddAttachment(ctAttachment);
        map[attPath] = ZipMapIO.ToUtf8Bytes(attachments.Element);
        WriteBackDocumentAndOfd(map, ofdRoot, docPath, document, maxUnitId);
    }

    internal static void DeleteAttachmentCore(Dictionary<string, byte[]> map, string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return;

        // ── 1. 解析 OFD.xml 与 Document.xml ───────────────────────
        var (_, docPath, docDirName) = LoadDocumentContext(map, out var document);

        // ── 2. 附件列表文件不存在则忽略（无附件可删）──────────────
        var attLocText = document.Attachments?.Value;
        var attPath = attLocText is null ? null : ZipMapIO.TryResolve(map, attLocText!, docDirName);
        if (attPath is null)
            return;

        var attachments = new Attachments(ZipMapIO.LoadXml(map, attPath).Root!);

        // ── 3. 删除同名附件记录与文件 ─────────────────────────────
        CleanOldAttachment(map, attachments, name, docDirName);
        map[attPath] = ZipMapIO.ToUtf8Bytes(attachments.Element);
    }

    // ── 内部辅助 ─────────────────────────────────────────────────

    private static (OFDRoot OfdRoot, string DocPath, string DocDirName) LoadDocumentContext(
        Dictionary<string, byte[]> map, out Document document)
    {
        if (!map.TryGetValue("OFD.xml", out var ofdBytes))
            throw new InvalidOperationException("错误的OFD结构，缺少 OFD.xml 文件");
        var ofdRoot = new OFDRoot(XDocument.Parse(System.Text.Encoding.UTF8.GetString(ofdBytes)).Root!);
        var docLoc = ofdRoot.GetFirstDocBody()?.DocRoot?.Value ?? "Doc_0/Document.xml";
        var docPath = ZipMapIO.Normalize(docLoc);
        if (!map.TryGetValue(docPath, out var docBytes))
            throw new InvalidOperationException($"错误的OFD结构，缺少文档描述文件 {docPath}");
        document = new Document(XDocument.Parse(System.Text.Encoding.UTF8.GetString(docBytes)).Root!);
        return (ofdRoot, docPath, ZipMapIO.ParentDir(docPath));
    }

    /// <summary>获取附件列表文件，不存在则创建并在 Document.xml 登记。</summary>
    private static (string Path, Attachments Model) ObtainAttachments(
        Dictionary<string, byte[]> map, string docDirName, Document document)
    {
        var attLocText = document.Attachments?.Value;
        var found = attLocText is null ? null : ZipMapIO.TryResolve(map, attLocText!, "");
        if (found != null)
            return (found, new Attachments(ZipMapIO.LoadXml(map, found).Root!));

        var path = $"{docDirName}/Attachments.xml";
        var model = new Attachments(new XElement(OFDElement.OfdNs + "Attachments"));
        // 记为根路径（/Doc_N/Attachments.xml），对齐 Java 样本约定，
        // 使 OFDReader 的 ResourceLocator 能从包根正确解析。
        document.Attachments = new STLoc($"/{path}");
        return (path, model);
    }

    /// <summary>清理同名附件：移除记录并删除包内文件。</summary>
    private static void CleanOldAttachment(Dictionary<string, byte[]> map, Attachments attachments, string name, string docDirName)
    {
        foreach (var att in attachments.GetAttachments())
        {
            if (!string.Equals(att.Name, name, StringComparison.Ordinal)) continue;

            // 删除附件记录
            att.Element.Remove();

            // 删除附件的文件（FileLoc 为相对文档目录路径，需以文档目录为基准解析）
            var loc = att.FileLoc?.Value;
            if (!string.IsNullOrWhiteSpace(loc))
            {
                var found = ZipMapIO.TryResolve(map, loc!, docDirName);
                if (found != null) map.Remove(found);
            }
            break;
        }
    }

    private static void WriteBackDocumentAndOfd(
        Dictionary<string, byte[]> map, OFDRoot ofdRoot,
        string docPath, Document document, long maxUnitId)
    {
        document.ObtainCommonData().MaxUnitID = new STID(maxUnitId + 1);
        map[docPath] = ZipMapIO.ToUtf8Bytes(document.Element);
        ofdRoot.ObtainDocBody().ObtainDocInfo().MaxUnitID = new STID(maxUnitId);
        map["OFD.xml"] = ZipMapIO.ToUtf8Bytes(ofdRoot.Element);
    }
}
