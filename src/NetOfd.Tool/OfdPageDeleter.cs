using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicStructure.PageTree;

namespace NetOfd.Tool;

/// <summary>
/// OFD 文档页面删除器（按索引删除，索引从 0 起）。
/// <para>仅删除文档树中的页面节点（与 Java OFDPageDeleter 行为一致），并更新修改时间。</para>
/// <para>对应 Java: org.ofdrw.tool.merge.OFDPageDeleter</para>
/// </summary>
public sealed class OfdPageDeleter : IDisposable
{
    private readonly string _src;
    private readonly string _out;
    private readonly List<int> _indexes = new();
    private bool _done;

    public OfdPageDeleter(string src, string @out)
    {
        if (string.IsNullOrWhiteSpace(src) || !File.Exists(src))
            throw new ArgumentException("OFD 源文件不存在", nameof(src));
        _src = src;
        _out = @out ?? throw new ArgumentNullException(nameof(@out));
    }

    /// <summary>标记要删除的页码（从 0 起）。可多次调用累加。</summary>
    public OfdPageDeleter Delete(params int[] indexes)
    {
        if (indexes != null) _indexes.AddRange(indexes);
        return this;
    }

    /// <summary>执行删除并写出结果文件。</summary>
    public void Close()
    {
        if (_done) return;
        _done = true;

        var map = OfdZip.ReadZip(_src);
        var docXml = OfdZip.TryLoad(map, "Doc_0/Document.xml")
            ?? throw new ToolException($"缺少 Doc_0/Document.xml: {_src}");
        var doc = new Document(docXml);
        var pages = doc.ObtainPages();
        var list = pages.GetPages();
        var toRemove = _indexes.Where(i => i >= 0 && i < list.Count).Select(i => list[i]).ToList();
        foreach (var p in toRemove) p.Element.Remove();

        // 更新文档修改时间
        var ofdXml = OfdZip.TryLoad(map, "OFD.xml");
        if (ofdXml != null)
        {
            var ofd = new OFDRoot(ofdXml);
            var body = ofd.GetFirstDocBody();
            var di = body?.ObtainDocInfo();
            if (di != null) di.ModDate = DateTime.Now.ToString("yyyy-MM-dd");
            map["OFD.xml"] = OfdZip.ToBytes(ofd.Element);
        }

        map["Doc_0/Document.xml"] = OfdZip.ToBytes(doc.Element);
        File.WriteAllBytes(_out, OfdZip.WriteZip(map));
    }

    public void Dispose() => Close();
}
