using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.Annotation;
using NetOfd.Core.CustomTags;
using NetOfd.Core.BasicType;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 批量编辑会话：多次编辑共享同一内存 ZIP 映射，<see cref="Save()"/> 时一次性写回。
/// <para>
/// 相比逐次调用各编辑器（每次完整 zip 重写），批量场景（如为上百页添加批注）
/// 只做一次落盘，开销从 O(N×文件大小) 降为 O(1×文件大小)：
/// <code>
/// using (var session = OfdEditSession.Open(ofdBytes))
/// {
///     for (int p = 1; p &lt;= session.PageCount; p++)
///         session.AddAnnotation(p, annotation);
///     session.AddVersion("批注定稿");
///     ofdBytes = session.Save();
/// }
/// </code>
/// 对应 Java: ofdrw 的 OFDDoc 常驻编辑模式（close 时统一打包）。
/// </para>
/// <para>注意：<see cref="IDisposable.Dispose"/> 不会隐式保存——落盘必须显式调用 <see cref="Save()"/>；
/// using 块仅用于限定会话作用域。</para>
/// </summary>
public sealed class OfdEditSession : IDisposable
{
    private readonly Dictionary<string, byte[]> _map;

    private OfdEditSession(Dictionary<string, byte[]> map) => _map = map;

    /// <summary><inheritdoc/> 无非托管资源；不会隐式保存，落盘请调用 <see cref="Save()"/>。</summary>
    public void Dispose()
    {
    }

    /// <summary>从 OFD 字节打开会话。</summary>
    public static OfdEditSession Open(byte[] inputOfd) => new(ZipMapIO.ReadZip(inputOfd));

    /// <summary>从 OFD 文件路径打开会话。</summary>
    public static OfdEditSession Open(string inputPath) => new(ZipMapIO.ReadZip(inputPath));

    // ── 查询 ─────────────────────────────────────────────────────

    /// <summary>文档总页数。</summary>
    public int PageCount => GetDocumentPages();

    /// <summary>读取全部版本入口。</summary>
    public IReadOnlyList<NetOfd.Core.Versions.Version> GetVersions()
        => OfdVersioner.GetVersionsCore(_map);

    /// <summary>读取全部自定义标引及其内容。</summary>
    public IReadOnlyList<(CustomTag Tag, byte[] Content)> GetCustomTags()
        => OfdTagger.GetCustomTagsCore(_map);

    // ── 编辑（流式 API） ─────────────────────────────────────────

    /// <summary>向指定页（1 起）添加注释。</summary>
    public OfdEditSession AddAnnotation(int pageNumber, Annotation annotation)
    {
        OfdAnnotator.AddAnnotationCore(_map, pageNumber, new[] { annotation });
        return this;
    }

    /// <summary>向指定页（1 起）批量添加注释。</summary>
    public OfdEditSession AddAnnotations(int pageNumber, IReadOnlyList<Annotation> annotations)
    {
        OfdAnnotator.AddAnnotationCore(_map, pageNumber, annotations);
        return this;
    }

    /// <summary>向指定页追加绘制内容（置于最顶层）。</summary>
    public OfdEditSession AddContent(int pageNumber, IDrawer drawer)
    {
        OfdEditor.AddContentCore(_map, pageNumber, drawer);
        return this;
    }

    /// <summary>
    /// 向指定页（1 起）添加水印：平铺绘制到该页最上层图层（对齐 Java edit.Watermark）。
    /// 页码越界时抛异常（与 <see cref="AddContent"/> 同语义）。
    /// </summary>
    public OfdEditSession AddWatermark(Watermark watermark, int pageNumber)
    {
        OfdEditor.AddWatermarkCore(_map, watermark, pageNumber);
        return this;
    }

    /// <summary>向所有页添加水印（逐页平铺）。</summary>
    public OfdEditSession AddWatermarkAllPages(Watermark watermark)
    {
        for (int p = 1; p <= PageCount; p++)
            OfdEditor.AddWatermarkCore(_map, watermark, p);
        return this;
    }

    /// <summary>
    /// 向登记区域（AreaHolderBlocks.xml）填充内容：绘制图元挂入该区域登记的空 PageBlock
    /// （对齐 Java AreaHolderContext.get + Canvas.setPreferBlock）。区域不存在或占位无效时本会话不变。
    /// </summary>
    /// <returns>true=已填充；false=区域不存在（不抛错，同 Java get 返回 null）。</returns>
    public bool FillAreaHolder(string areaName, IDrawer drawer)
        => OfdEditor.FillAreaHolderCore(_map, areaName, drawer);

    /// <summary>
    /// 向登记区域填充「单元格化」内容（对齐 Java <c>AreaHolderContext.getCell(name)</c>）：
    /// 自动以区域边界构造 <see cref="CellContentDrawer"/> 并交由 <paramref name="configure"/> 设置
    /// 文字/样式，绘制挂入该区域登记的空 PageBlock。区域不存在或占位无效时本会话不变。
    /// </summary>
    /// <returns>true=已填充；false=区域不存在（不抛错，同 Java get 返回 null）。</returns>
    public bool FillAreaHolder(string areaName, Action<CellContentDrawer> configure)
        => OfdEditor.FillAreaHolderCellCore(_map, areaName, configure);

    /// <summary>查询登记区域边界。返回 false=区域不存在或边界无效。</summary>
    public bool TryGetAreaHolder(string areaName, out STBox boundary)
        => OfdEditor.TryGetAreaHolderCore(_map, areaName, out boundary);

    /// <summary>追加空白页，返回新页码（1 起）。</summary>
    public int AppendPage() => OfdEditor.AppendPageCore(_map);

    /// <summary>
    /// 在末页之后追加一组流式元素（对齐 <see cref="OfdEditor.AddStream(byte[], System.Collections.Generic.IEnumerable{Div}, out byte[])"/>）：
    /// 从末页已有内容底部续写，超出则自动分页。会话内通过 zip 往返复用编辑态（一次落盘成本）。
    /// </summary>
    public OfdEditSession AddStream(IEnumerable<Div> elements)
    {
        var bytes = ZipMapIO.WriteZipToBytes(_map);
        OfdEditor.AddStream(bytes, elements, out var outBytes);
        _map.Clear();
        foreach (var kv in ZipMapIO.ReadZip(outBytes))
            _map[kv.Key] = kv.Value;
        return this;
    }

    /// <summary>添加附件。</summary>
    public OfdEditSession AddAttachment(Attachment attachment)
    {
        OfdAttacher.AddAttachmentCore(_map, attachment);
        return this;
    }

    /// <summary>删除指定名称的附件。</summary>
    public OfdEditSession DeleteAttachment(string name)
    {
        OfdAttacher.DeleteAttachmentCore(_map, name);
        return this;
    }

    /// <summary>添加自定义标引。</summary>
    public OfdEditSession AddCustomTag(string typeId, byte[] tagFileContent,
        string? nameSpace = null, string? schemaLoc = null)
    {
        OfdTagger.AddCustomTagCore(_map, typeId, tagFileContent, nameSpace, schemaLoc);
        return this;
    }

    /// <summary>创建版本快照，返回版本序号（1 起）。</summary>
    public int AddVersion(string versionName) => OfdVersioner.AddVersionCore(_map, versionName);

    // ── 落盘 ─────────────────────────────────────────────────────

    /// <summary>写回为 OFD 字节。会话仍可继续使用（继续编辑后再次 Save）。</summary>
    public byte[] Save() => ZipMapIO.WriteZipToBytes(_map);

    /// <summary>写回至指定路径。</summary>
    public void Save(string outputPath) => ZipMapIO.WriteZip(outputPath, _map);

    // ── 内部 ─────────────────────────────────────────────────────

    private int GetDocumentPages()
    {
        foreach (var kv in _map)
        {
            if (!kv.Key.EndsWith("/Document.xml", StringComparison.Ordinal)) continue;
            var doc = XDocumentLoad(kv.Value);
            var pages = doc.Descendants().First(e => e.Name.LocalName == "Pages");
            return pages.Elements().Count(e => e.Name.LocalName == "Page");
        }
        throw new InvalidOperationException("错误的OFD结构，缺少 Document.xml 文件");
    }

    private static XDocument XDocumentLoad(byte[] bytes)
        => XDocument.Parse(System.Text.Encoding.UTF8.GetString(bytes));
}
