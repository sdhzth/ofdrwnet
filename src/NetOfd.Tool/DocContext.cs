using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicType;

namespace NetOfd.Tool;

/// <summary>
/// 源文档上下文：解析 OFD 包，构建页面清单与资源注册表，供合并时迁移使用。
/// 对应 Java: org.ofdrw.tool.merge.DocContext
/// </summary>
public sealed class DocContext : IDisposable
{
    public string FileName { get; }

    /// <summary>包内 路径→字节 字典</summary>
    public Dictionary<string, byte[]> Map { get; }

    /// <summary>Doc_0/Document.xml 根元素</summary>
    public XElement DocumentXml { get; }

    /// <summary>页面清单（0-based 顺序）：(页面对象ID, BaseLoc)</summary>
    public List<(long Id, string BaseLoc)> Pages { get; }

    /// <summary>资源注册表：旧资源ID → 资源元素（克隆）</summary>
    public Dictionary<long, XElement> ResRegistry { get; }

    /// <summary>旧资源ID → 新资源ID 映射（本次合并内去重）</summary>
    public Dictionary<long, long> ResOldNew { get; }

    private DocContext(string fileName, Dictionary<string, byte[]> map, XElement docXml,
        List<(long, string)> pages, Dictionary<long, XElement> registry)
    {
        FileName = fileName;
        Map = map;
        DocumentXml = docXml;
        Pages = pages;
        ResRegistry = registry;
        ResOldNew = new Dictionary<long, long>();
    }

    public static DocContext Load(string path)
    {
        if (!File.Exists(path)) throw new ToolException($"OFD 文件不存在: {path}");
        var map = OfdZip.ReadZip(path);
        var docXml = OfdZip.TryLoad(map, "Doc_0/Document.xml")
            ?? throw new ToolException($"缺少 Doc_0/Document.xml: {path}");
        var doc = new Document(docXml);

        var pages = new List<(long, string)>();
        var pagesTree = doc.Pages;
        if (pagesTree != null)
        {
            int i = 0;
            foreach (var p in pagesTree.GetPages())
            {
                var bl = p.BaseLoc?.Value;
                if (string.IsNullOrEmpty(bl)) bl = $"Pages/Page_{i}";
                pages.Add((p.ID.Value, bl!));
                i++;
            }
        }

        var registry = new Dictionary<long, XElement>();
        var cdata = doc.CommonData;
        if (cdata != null)
        {
            foreach (var loc in cdata.PublicResList.Concat(cdata.DocumentResList))
            {
                var resEl = OfdZip.TryLoad(map, "Doc_0/" + loc.Value);
                if (resEl == null) continue;
                // 资源按类型分组在 <Fonts>/<DrawParams>/... 容器内，
                // 需递归所有后代元素以捕获带 @ID 的资源定义。
                foreach (var child in resEl.Descendants())
                {
                    var idAttr = child.Attribute("ID");
                    if (idAttr != null && long.TryParse(idAttr.Value, out var rid))
                        registry[rid] = new XElement(child);
                }
            }
        }

        return new DocContext(Path.GetFileName(path), map, docXml, pages, registry);
    }

    public void Dispose()
    {
        Map.Clear();
        Pages.Clear();
        ResRegistry.Clear();
        ResOldNew.Clear();
    }
}
