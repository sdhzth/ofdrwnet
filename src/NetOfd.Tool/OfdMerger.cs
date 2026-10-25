using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicType;

namespace NetOfd.Tool;

/// <summary>
/// OFD 文档合并工具。
/// <para>支持多文档/多页合并、页面混合（overlay）、对象ID与资源ID重映射、资源文件去重复制。</para>
/// <para>对应 Java: org.ofdrw.tool.merge.OFDMerger</para>
/// <para>实现策略：zip + XML 层（与 NetOfd.Sign 一致），不依赖半成品容器层。</para>
/// </summary>
public sealed class OfdMerger : IDisposable
{
    private const string ToolVersion = "1.0.0";

    private readonly string _dest;
    private readonly List<PageEntry> _pageArr = new();
    private readonly Dictionary<string, DocContext> _ctxMap = new(StringComparer.OrdinalIgnoreCase);
    private long _nextId = 1;
    private readonly Dictionary<string, string> _resFileHash = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _pageContents = new(StringComparer.OrdinalIgnoreCase);
    private XElement _resRoot = null!;
    private bool _done;

    /// <summary>与资源引用相关的属性名（用于重映射资源ID）</summary>
    private static readonly string[] ResAttrs =
        { "Font", "ResourceID", "Substitution", "ImageMask", "Thumbnail", "DrawParam", "ColorSpace" };

    /// <summary>资源元素名 → Res 分组容器名（OFD 规范要求资源按类型分组）。</summary>
    private static readonly Dictionary<string, string> ResGroup = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Font"] = "Fonts",
        ["DrawParam"] = "DrawParams",
        ["ColorSpace"] = "ColorSpaces",
        ["MultiMedia"] = "MultiMedias"
    };

    public OfdMerger(string dest)
    {
        if (string.IsNullOrWhiteSpace(dest)) throw new ArgumentException("合并结果路径(dest)为空", nameof(dest));
        _dest = dest;
        var parent = Path.GetDirectoryName(Path.GetFullPath(dest));
        if (string.IsNullOrEmpty(parent) || !Directory.Exists(parent))
            throw new ArgumentException($"目标路径上级目录不存在: {parent}");
    }

    /// <summary>向合并文件添加页面（页码从 1 起；不传页码表示全部页面）。</summary>
    public OfdMerger Add(string filepath, params int[] pageIndexes)
    {
        var ctx = GetCtx(filepath);
        if (pageIndexes == null || pageIndexes.Length == 0)
        {
            for (int i = 1; i <= ctx.Pages.Count; i++) _pageArr.Add(new PageEntry(ctx, i));
        }
        else
        {
            foreach (var pi in pageIndexes) _pageArr.Add(new PageEntry(ctx, pi));
        }
        return this;
    }

    /// <summary>添加已构造好的页面对象（可携带混合页面）。</summary>
    public OfdMerger Add(params PageEntry[] pages)
    {
        if (pages != null)
            foreach (var p in pages)
                if (p?.Ctx != null) _pageArr.Add(p);
        return this;
    }

    /// <summary>添加并混合两个文档的页面（以 dst 页面为底层，tbMix 叠加上方）。</summary>
    public OfdMerger AddMix(string dstDocFilepath, int dstPageIndex, string tbMixDocFilepath, int tbMixPageIndex)
    {
        var dst = new PageEntry(GetCtx(dstDocFilepath), dstPageIndex);
        dst.MixPages.Add(new PageEntry(GetCtx(tbMixDocFilepath), tbMixPageIndex));
        _pageArr.Add(dst);
        return this;
    }

    /// <summary>按顺序混合多个文档页面到一个页面（第 1 个最底层）。</summary>
    public OfdMerger AddMix(params DocPage[] pageList)
    {
        if (pageList == null || pageList.Length == 0) return this;
        PageEntry? baseEntry = null;
        foreach (var p in pageList)
        {
            if (p == null || string.IsNullOrEmpty(p.Path) || p.Index < 1) continue;
            var ctx = GetCtx(p.Path);
            if (baseEntry == null) baseEntry = new PageEntry(ctx, p.Index);
            else baseEntry.MixPages.Add(new PageEntry(ctx, p.Index));
        }
        if (baseEntry != null) _pageArr.Add(baseEntry);
        return this;
    }

    /// <summary>执行合并并写出结果文件。复用 IDisposable 语义（using 自动调用）。</summary>
    public void Close()
    {
        if (_done) return;
        _done = true;
        if (_pageArr.Count > 0) DoMerge();
        foreach (var c in _ctxMap.Values) c.Dispose();
        _ctxMap.Clear();
    }

    private void DoMerge()
    {
        var ofd = new OFDRoot();
        var docBody = new DocBody
        {
            DocInfo = new DocInfo
            {
                DocID = Guid.NewGuid().ToString("N"),
                CreationDate = DateTime.Now.ToString("yyyy-MM-dd"),
                Creator = "NetOfd.Tool",
                CreatorVersion = ToolVersion
            },
            DocRoot = new STLoc("Doc_0/Document.xml")
        };
        ofd.AddDocBody(docBody);

        var doc = new Document();
        var cdata = new CTCommonData();
        // PageArea 取首个文档的默认页面尺寸
        var firstCtx = _pageArr[0].Ctx;
        var firstDoc = new Document(firstCtx.DocumentXml);
        var pa = firstDoc.CommonData?.PageArea;
        if (pa != null) cdata.PageArea = new CT_PageArea(new XElement(pa.Element));
        cdata.AddPublicRes(new STLoc("PublicRes.xml"));
        doc.CommonData = cdata;
        var pages = new Pages();
        doc.Pages = pages;

        _resRoot = new XElement(OfdZip.Ns + "Res");

        int pageNumber = 0;
        foreach (var entry in _pageArr)
        {
            pageNumber++;
            MigratePage(entry, pages, pageNumber);
        }

        cdata.MaxUnitID = new STID(_nextId);

        var outMap = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["OFD.xml"] = OfdZip.ToBytes(ofd.Element),
            ["Doc_0/Document.xml"] = OfdZip.ToBytes(doc.Element),
            ["Doc_0/PublicRes.xml"] = OfdZip.ToBytes(_resRoot)
        };
        foreach (var kv in _pageContents) outMap[kv.Key] = kv.Value;

        File.WriteAllBytes(_dest, OfdZip.WriteZip(outMap));
    }

    private void MigratePage(PageEntry entry, Pages pages, int pageNumber)
    {
        var ctx = entry.Ctx;
        int idx = entry.Index - 1;
        if (idx < 0 || idx >= ctx.Pages.Count)
            throw new ToolException($"页码越界: {entry.Index}（共 {ctx.Pages.Count} 页）");
        var (_, baseLoc) = ctx.Pages[idx];
        var contentKey = "Doc_0/" + ResolveContentPath(baseLoc);
        var content = OfdZip.TryLoad(ctx.Map, contentKey)
            ?? throw new ToolException($"找不到页面内容: {contentKey}");
        content = new XElement(content);

        // 重映射页面内对象 ID（跳过根元素，根 ID 单独分配）
        long pageObjId = _nextId++;
        var idMap = new Dictionary<string, long>();
        foreach (var el in content.Descendants().Where(e => e != content && e.Attribute("ID") != null).ToList())
        {
            var oldId = el.Attribute("ID")!.Value;
            if (!idMap.TryGetValue(oldId, out var nid)) { nid = _nextId++; idMap[oldId] = nid; }
            el.SetAttributeValue("ID", nid);
        }
        content.SetAttributeValue("ID", pageObjId);

        // 重映射资源引用
        MigrateResourcesInDom(ctx, content);

        // 混合页面
        var contentChild = content.Elements(OfdZip.Ns + "Content").FirstOrDefault();
        if (entry.MixPages.Count > 0)
        {
            contentChild ??= new XElement(OfdZip.Ns + "Content");
            if (contentChild.Parent == null) content.Add(contentChild);
            foreach (var mix in entry.MixPages) MixInto(contentChild, mix);
        }

        _pageContents[$"Doc_0/Pages/Page_{pageNumber}/Content.xml"] = OfdZip.ToBytes(content);

        var pageNode = new Page { ID = new STID(pageObjId), BaseLoc = new STLoc($"Pages/Page_{pageNumber}") };
        pages.AddPage(pageNode);
    }

    private void MixInto(XElement targetContentChild, PageEntry mixEntry)
    {
        var ctx = mixEntry.Ctx;
        int idx = mixEntry.Index - 1;
        if (idx < 0 || idx >= ctx.Pages.Count) return;
        var (_, baseLoc) = ctx.Pages[idx];
        var content = OfdZip.TryLoad(ctx.Map, "Doc_0/" + ResolveContentPath(baseLoc));
        if (content == null) return;
        content = new XElement(content);

        var idMap = new Dictionary<string, long>();
        foreach (var el in content.Descendants().Where(e => e != content && e.Attribute("ID") != null).ToList())
        {
            var oldId = el.Attribute("ID")!.Value;
            if (!idMap.TryGetValue(oldId, out var nid)) { nid = _nextId++; idMap[oldId] = nid; }
            el.SetAttributeValue("ID", nid);
        }
        MigrateResourcesInDom(ctx, content);

        var mixContent = content.Elements(OfdZip.Ns + "Content").FirstOrDefault();
        if (mixContent != null)
        {
            foreach (var child in mixContent.Elements().ToList())
                targetContentChild.Add(new XElement(child));
        }
    }

    private void MigrateResourcesInDom(DocContext ctx, XElement dom)
    {
        foreach (var attr in ResAttrs)
        {
            foreach (var el in dom.Descendants().Where(e => e.Attribute(attr) != null).ToList())
            {
                var oldVal = el.Attribute(attr)!.Value;
                if (!long.TryParse(oldVal, out var oldId)) continue;
                long newId = MigrateResource(ctx, oldId);
                el.SetAttributeValue(attr, newId);
            }
        }
    }

    private long MigrateResource(DocContext ctx, long oldId)
    {
        if (ctx.ResOldNew.TryGetValue(oldId, out var existing)) return existing;
        if (!ctx.ResRegistry.TryGetValue(oldId, out var resEl))
        {
            // 资源缺失：仍分配新 ID，保持引用结构（内容为空）
            var nid = _nextId++;
            ctx.ResOldNew[oldId] = nid;
            return nid;
        }
        var clone = new XElement(resEl);
        var newId = _nextId++;
        clone.SetAttributeValue("ID", newId);
        CopyResourceFileIfAny(ctx, clone, "FontFile");
        CopyResourceFileIfAny(ctx, clone, "Profile");
        CopyResourceFileIfAny(ctx, clone, "MediaFile");
        AddToResRoot(clone);
        ctx.ResOldNew[oldId] = newId;
        return newId;
    }

    /// <summary>将迁移后的资源按 OFD 规范归入正确的分组容器（Fonts/DrawParams/...）。</summary>
    private void AddToResRoot(XElement resEl)
    {
        var local = resEl.Name.LocalName;
        if (ResGroup.TryGetValue(local, out var containerName))
        {
            var container = _resRoot.Element(OfdZip.Ns + containerName);
            if (container == null)
            {
                container = new XElement(OfdZip.Ns + containerName);
                _resRoot.Add(container);
            }
            container.Add(resEl);
        }
        else
        {
            _resRoot.Add(resEl);
        }
    }

    private void CopyResourceFileIfAny(DocContext ctx, XElement resEl, string attrName)
    {
        var attr = resEl.Attribute(attrName);
        if (attr == null) return;
        var oldLoc = attr.Value;
        if (string.IsNullOrWhiteSpace(oldLoc)) { attr.Remove(); return; }
        var srcPath = ResolveInDoc(ctx, oldLoc);
        if (srcPath == null || !ctx.Map.TryGetValue(srcPath, out var bytes)) { attr.Remove(); return; }
        var fileName = CopyFileToMerged(bytes, oldLoc);
        attr.Value = "Res/" + fileName;
    }

    private static string? ResolveInDoc(DocContext ctx, string relLoc)
    {
        var candidates = new[]
        {
            "Doc_0/" + relLoc,
            "Doc_0/Res/" + relLoc,
            "Doc_0/" + relLoc.TrimStart('.', '/')
        };
        foreach (var c in candidates)
        {
            var norm = OfdZip.NormalizeKey(c);
            if (ctx.Map.ContainsKey(norm)) return norm;
        }
        return null;
    }

    private string CopyFileToMerged(byte[] bytes, string originalLoc)
    {
        using var sha = SHA256.Create();
        var hash = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        if (_resFileHash.TryGetValue(hash, out var existing)) return existing;
        var ext = Path.GetExtension(originalLoc);
        var name = "r" + (_resFileHash.Count + 1) + ext;
        _resFileHash[hash] = name;
        _pageContents["Doc_0/Res/" + name] = bytes;
        return name;
    }

    private DocContext GetCtx(string filepath)
    {
        var key = Path.GetFileName(filepath);
        if (_ctxMap.TryGetValue(key, out var c)) return c;
        c = DocContext.Load(filepath);
        _ctxMap[key] = c;
        return c;
    }

    /// <summary>将页面 BaseLoc 解析为 Content.xml 相对路径（BaseLoc 可能已含 Content.xml）。</summary>
    private static string ResolveContentPath(string baseLoc)
    {
        var bl = baseLoc.TrimEnd('/');
        return bl.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) ? bl : bl + "/Content.xml";
    }

    public void Dispose() => Close();
}
