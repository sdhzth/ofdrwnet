using System.Text;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicStructure.PageTree;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Layout.Engine;

namespace NetOfd.Layout.Edit;

/// <summary>
/// OFD 页面编辑器：向已有 OFD 文档的指定页添加内容，或追加新页面。
/// <para>
/// 采用与 <see cref="OfdAnnotator"/> 一致的映射（Dictionary）方式读写包内文件：
/// <list type="bullet">
/// <item><see cref="AddContent(byte[], int, NetOfd.Layout.Element.Canvas.IDrawer)"/>：定位页面 Content.xml，选取最上层图层绘制
/// （对齐 Java AdditionVPage.obtainTopLayer 语义）；</item>
/// <item><see cref="AppendPage(byte[])"/>：创建新页面目录 Content.xml 并登记页树
/// （对齐 Java OFDDoc#addVPage 的页面注册部分）。</item>
/// </list>
/// </para>
/// </summary>
public static class OfdEditor
{
    /// <summary>向已有 OFD 字节中的指定页添加内容，返回新的 OFD 字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="pageNumber">页码（从 1 开始）</param>
    /// <param name="drawer">内容绘制器</param>
    public static byte[] AddContent(byte[] inputOfd, int pageNumber, IDrawer drawer)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        AddContentCore(map, pageNumber, drawer);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>向已有 OFD 文件中的指定页添加内容，输出新的 OFD 文件。</summary>
    public static void AddContent(string inputPath, string outputPath, int pageNumber, IDrawer drawer)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        AddContentCore(map, pageNumber, drawer);
        ZipMapIO.WriteZip(outputPath, map);
    }

    /// <summary>向已有 OFD 字节追加一个空白新页，返回新的 OFD 字节与新页码（1 起）。</summary>
    public static int AppendPage(byte[] inputOfd, out byte[] outputOfd)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        var pageNum = AppendPageCore(map);
        outputOfd = ZipMapIO.WriteZipToBytes(map);
        return pageNum;
    }

    /// <summary>向已有 OFD 字节追加一个空白新页，返回新的 OFD 字节。</summary>
    public static byte[] AppendPage(byte[] inputOfd)
    {
        AppendPage(inputOfd, out var output);
        return output;
    }

    /// <summary>向已有 OFD 文件追加一个空白新页，输出新的 OFD 文件，返回新页码（1 起）。</summary>
    public static int AppendPage(string inputPath, string outputPath)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        var pageNum = AppendPageCore(map);
        ZipMapIO.WriteZip(outputPath, map);
        return pageNum;
    }

    // ── 区域占位区块填充（对齐 Java areaholder.AreaHolderContext + AdditionVPage 编辑路径）──

    /// <summary>
    /// 向已有 OFD 中登记的「区域占位区块」（AreaHolderBlocks.xml）内填充内容，返回新的 OFD 字节。
    /// <para>绘制内容挂入登记记录指向的既有 <c>PageBlock</c>（Java
    /// <c>AreaHolderContext.get(areaName)</c> → <c>Canvas.setPreferBlock</c> 同构），而非追加到顶层图层；
    /// 绘制坐标 = 页面绝对 mm（与 <see cref="AddContent(byte[], int, IDrawer)"/> 一致）。
    /// 建议先经 <see cref="TryGetAreaHolder(byte[], string, out STBox)"/> 取得区域边界以确定摆放位置。</para>
    /// </summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="areaName">区域名称（AreaName 属性）</param>
    /// <param name="drawer">内容绘制器</param>
    /// <param name="outputOfd">输出 OFD 字节（区域不存在时为原字节）</param>
    /// <returns>true=区域存在且已填充；false=区域不存在或占位无效（Java <c>AreaHolderContext.get</c> 返回 null 同语义）</returns>
    public static bool FillAreaHolder(byte[] inputOfd, string areaName, IDrawer drawer, out byte[] outputOfd)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        var ok = FillAreaHolderCore(map, areaName, drawer);
        outputOfd = ok ? ZipMapIO.WriteZipToBytes(map) : inputOfd;
        return ok;
    }

    /// <summary>向已有 OFD 文件中的登记区域填充内容，输出新的 OFD 文件。返回 false 表示区域不存在。</summary>
    public static bool FillAreaHolder(string inputPath, string outputPath, string areaName, IDrawer drawer)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        var ok = FillAreaHolderCore(map, areaName, drawer);
        if (ok) ZipMapIO.WriteZip(outputPath, map);
        return ok;
    }

    /// <summary>
    /// 在已有 OFD 的末页之后追加一组流式元素（对齐 Java <c>OFDDoc.addStreamCollect</c> 的
    /// "在已有文档插入流式段"语义）：从末页已有内容底部续写，超出版面时自动分页新建页面并插入页树。
    /// </summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="elements">追加的流式元素（Div/Paragraph/Img 等）</param>
    /// <param name="outputOfd">输出 OFD 字节</param>
    /// <returns>是否成功（既有文档有效即成功）</returns>
    public static bool AddStream(byte[] inputOfd, IEnumerable<Div> elements, out byte[] outputOfd)
    {
        using var doc = OFDDoc.OpenEdit(inputOfd);
        foreach (var e in elements) doc.Add(e);
        using var ms = new MemoryStream();
        doc.Save(ms);
        outputOfd = ms.ToArray();
        return true;
    }

    /// <summary>文件版：在 <paramref name="inputPath"/> 末页追加流式元素，输出到 <paramref name="outputPath"/>。</summary>
    public static bool AddStream(string inputPath, string outputPath, params Div[] elements)
    {
        var bytes = File.ReadAllBytes(inputPath);
        var ok = AddStream(bytes, elements, out var outBytes);
        if (ok) File.WriteAllBytes(outputPath, outBytes);
        return ok;
    }

    // ── 水印（对齐 Java edit.Watermark：平铺到指定页最上层图层）──

    /// <summary>
    /// 向已有 OFD 字节的指定页（1 起）添加水印：复用 <see cref="AddContent(byte[], int, IDrawer)"/>，
    /// 将 <paramref name="watermark"/> 的 <see cref="WatermarkDrawer"/>（已实现 <see cref="IDrawer"/>）作为绘制器平铺。
    /// </summary>
    public static byte[] AddWatermark(byte[] inputOfd, Watermark watermark, int pageNumber)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        AddWatermarkCore(map, watermark, pageNumber);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>文件版：向指定页添加水印，输出新的 OFD 文件。</summary>
    public static void AddWatermark(string inputPath, string outputPath, Watermark watermark, int pageNumber)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        AddWatermarkCore(map, watermark, pageNumber);
        ZipMapIO.WriteZip(outputPath, map);
    }

    /// <summary>向已有 OFD 字节的所有页添加水印（逐页平铺），返回新的 OFD 字节。</summary>
    public static byte[] AddWatermarkAllPages(byte[] inputOfd, Watermark watermark)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        int n = CountPages(map);
        for (int p = 1; p <= n; p++) AddWatermarkCore(map, watermark, p);
        return ZipMapIO.WriteZipToBytes(map);
    }

    /// <summary>文件版：向所有页添加水印，输出新的 OFD 文件。</summary>
    public static void AddWatermarkAllPages(string inputPath, string outputPath, Watermark watermark)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        int n = CountPages(map);
        for (int p = 1; p <= n; p++) AddWatermarkCore(map, watermark, p);
        ZipMapIO.WriteZip(outputPath, map);
    }

    /// <summary>统计文档页数（供逐页水印等批量操作使用）。</summary>
    internal static int CountPages(Dictionary<string, byte[]> map)
    {
        var ctx = LoadContext(map);
        return CollectPages(ctx.Document).Count;
    }

    internal static void AddWatermarkCore(Dictionary<string, byte[]> map, Watermark watermark, int pageNumber)
    {
        if (watermark is null) throw new ArgumentNullException(nameof(watermark));
        if (watermark.Drawer is null)
            throw new ArgumentException("水印缺少绘制器（Drawer）", nameof(watermark));
        AddContentCore(map, pageNumber, watermark.Drawer);
    }

    /// <summary>
    /// 向已有 OFD 中登记的「区域占位区块」填充「单元格化」内容，返回新的 OFD 字节。
    /// <para>对齐 Java <c>AreaHolderContext.getCell(name)</c>：自动以区域边界构造
    /// <see cref="CellContentDrawer"/>（定位在区域绝对坐标），交由 <paramref name="configure"/> 设置
    /// 文字/字号/字体/颜色/对齐等样式后绘制挂入 PageBlock。无需手动计算坐标。</para>
    /// </summary>
    /// <returns>true=区域存在且已填充；false=区域不存在或占位无效。</returns>
    public static bool FillAreaHolder(byte[] inputOfd, string areaName, Action<CellContentDrawer> configure, out byte[] outputOfd)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        var ok = FillAreaHolderCellCore(map, areaName, configure);
        outputOfd = ok ? ZipMapIO.WriteZipToBytes(map) : inputOfd;
        return ok;
    }

    /// <summary>向已有 OFD 文件中的登记区域填充单元格化内容，输出新文件。返回 false=区域不存在。</summary>
    public static bool FillAreaHolder(string inputPath, string outputPath, string areaName, Action<CellContentDrawer> configure)
    {
        var map = ZipMapIO.ReadZip(inputPath);
        var ok = FillAreaHolderCellCore(map, areaName, configure);
        if (ok) ZipMapIO.WriteZip(outputPath, map);
        return ok;
    }

    /// <summary>
    /// 查询登记区域（AreaHolderBlocks.xml）的边界（Java <c>AreaHolderContext.get</c> 内部 boundary 提取，只读形态）。
    /// </summary>
    /// <returns>true=区域存在且边界有效；false=区域不存在或占位无效。</returns>
    public static bool TryGetAreaHolder(byte[] inputOfd, string areaName, out STBox boundary)
    {
        var map = ZipMapIO.ReadZip(inputOfd);
        return TryGetAreaHolderCore(map, areaName, out boundary);
    }

    // ── 核心实现 ─────────────────────────────────────────────────

    internal static void AddContentCore(Dictionary<string, byte[]> map, int pageNumber, IDrawer drawer)
    {
        if (pageNumber < 1)
            throw new ArgumentException("页码（pageNumber）必须从 1 开始", nameof(pageNumber));
        if (drawer is null)
            throw new ArgumentNullException(nameof(drawer));

        // ── 1. 解析文档上下文 ─────────────────────────────────────
        var ctx = LoadContext(map);

        // ── 2. 定位目标页面的 Content.xml ─────────────────────────
        var pageList = CollectPages(ctx.Document);
        if (pageNumber > pageList.Count)
            throw new ArgumentException($"页码超出范围：文档共 {pageList.Count} 页", nameof(pageNumber));
        var page = pageList[pageNumber - 1];
        var baseLoc = page.BaseLoc?.Value
            ?? throw new InvalidOperationException("目标页面缺少 BaseLoc 属性，无法定位页面内容文件");
        var contentPath = ZipMapIO.TryResolve(map, baseLoc, ctx.DocDirName)
            ?? throw new InvalidOperationException($"页面内容文件不存在：{baseLoc}");

        // ── 3. 初始化最大对象 ID 与资源提供器 ─────────────────────
        long maxUnitId = InitMaxUnitId(ctx);
        var provider = new MapResourceProvider(map, ctx.DocDirName, () => ++maxUnitId, ctx.Document.ObtainCommonData());

        // ── 4. 解析页面对象并选取最上层图层 ───────────────────────
        var pageObj = new PageObject(ZipMapIO.LoadXml(map, contentPath).Root!);
        var layer = ObtainTopLayer(pageObj, () => ++maxUnitId);

        // ── 5. 绘制内容并回写 ─────────────────────────────────────
        var box = GetPageBox(pageObj, ctx.Document);
        var drawCtx = new DrawContext(layer, box, () => ++maxUnitId, provider);
        drawer.Draw(drawCtx);
        provider.Flush();

        map[contentPath] = ZipMapIO.ToUtf8Bytes(pageObj.Element);
        WriteBack(map, ctx, maxUnitId);
    }

    internal static int AppendPageCore(Dictionary<string, byte[]> map)
    {
        // ── 1. 解析文档上下文 ─────────────────────────────────────
        var ctx = LoadContext(map);
        long maxUnitId = InitMaxUnitId(ctx);

        // ── 2. 创建新页面目录与 Content.xml ───────────────────────
        var nextIndex = NextPageIndex(map, ctx.DocDirName);
        var baseLoc = $"Pages/Page_{nextIndex}/Content.xml";
        var contentPath = $"{ctx.DocDirName}/{baseLoc}";
        map[contentPath] = ZipMapIO.ToUtf8Bytes(new PageObject().Element);

        // ── 3. 向页树注册新页面 ───────────────────────────────────
        var pages = ctx.Document.ObtainPages();
        pages.AddPage(new Page
        {
            ID = new STID(++maxUnitId),
            BaseLoc = new STLoc(baseLoc)
        });
        WriteBack(map, ctx, maxUnitId);

        // 新页码 = 追加后的总页数
        return CollectPages(ctx.Document).Count;
    }

    // ── 内部辅助 ─────────────────────────────────────────────────

    internal sealed record DocContext(OFDRoot OfdRoot, string DocPath, string DocDirName, Document Document);

    internal static DocContext LoadContext(Dictionary<string, byte[]> map)
    {
        if (!map.TryGetValue("OFD.xml", out var ofdBytes))
            throw new InvalidOperationException("错误的OFD结构，缺少 OFD.xml 文件");
        var ofdRoot = new OFDRoot(XDocument.Parse(System.Text.Encoding.UTF8.GetString(ofdBytes)).Root!);
        var docLoc = ofdRoot.GetFirstDocBody()?.DocRoot?.Value ?? "Doc_0/Document.xml";
        var docPath = ZipMapIO.Normalize(docLoc);
        if (!map.TryGetValue(docPath, out var docBytes))
            throw new InvalidOperationException($"错误的OFD结构，缺少文档描述文件 {docPath}");
        var document = new Document(XDocument.Parse(System.Text.Encoding.UTF8.GetString(docBytes)).Root!);
        return new DocContext(ofdRoot, docPath, ZipMapIO.ParentDir(docPath), document);
    }

    private static long InitMaxUnitId(DocContext ctx) =>
        ctx.OfdRoot.GetFirstDocBody()?.DocInfo?.MaxUnitID?.Value
        ?? ctx.Document.CommonData?.MaxUnitID?.Value
        ?? 0;

    private static void WriteBack(Dictionary<string, byte[]> map, DocContext ctx, long maxUnitId)
    {
        ctx.Document.ObtainCommonData().MaxUnitID = new STID(maxUnitId + 1);
        map[ctx.DocPath] = ZipMapIO.ToUtf8Bytes(ctx.Document.Element);
        ctx.OfdRoot.ObtainDocBody().ObtainDocInfo().MaxUnitID = new STID(maxUnitId);
        map["OFD.xml"] = ZipMapIO.ToUtf8Bytes(ctx.OfdRoot.Element);
    }

    internal static List<Page> CollectPages(Document document)
    {
        var pages = document.Pages
            ?? throw new InvalidOperationException("文档缺少页面树（Pages）");
        var res = new List<Page>();
        Collect(pages.Element, res, 0);
        return res;
    }

    internal static void Collect(XElement parent, List<Page> sink, int depth)
    {
        if (depth > 32) return; // 防御异常深嵌套
        foreach (var e in parent.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "Page":
                    sink.Add(new Page(e));
                    break;
                case "Pages":
                    Collect(e, sink, depth + 1);
                    break;
            }
        }
    }

    private static int NextPageIndex(Dictionary<string, byte[]> map, string docDirName)
    {
        var prefix = $"{docDirName}/Pages/";
        int max = -1;
        foreach (var key in map.Keys)
        {
            if (!key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) continue;
            var rest = key.Substring(prefix.Length);
            var slash = rest.IndexOf('/');
            if (slash <= 0) continue;
            var m = System.Text.RegularExpressions.Regex.Match(
                rest[..slash], @"^Page_(\d+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success && int.TryParse(m.Groups[1].Value, out var idx) && idx > max)
                max = idx;
        }
        return max + 1;
    }

    /// <summary>
    /// 获取处于最上层的图层（对齐 Java AdditionVPage.obtainTopLayer）：
    /// 同类型取位置靠后者；页面无图层时创建新图层。
    /// </summary>
    private static CTLayer ObtainTopLayer(PageObject pageObj, Func<long> nextId)
    {
        var content = pageObj.Content;
        if (content is null)
        {
            // 页面中没有任何图层时，创建一个新的图层加入页面
            var created = new CTLayer { ID = new STID(nextId()) };
            pageObj.ObtainContent().AddLayer(created);
            return created;
        }

        CTLayer? top = null;
        var currentOrder = (int)LayerType.Background_Template; // 默认为最底层
        foreach (var l in content.GetLayers())
        {
            var order = (int)l.LayerType;
            if (order >= currentOrder)
            {
                top = l;
                currentOrder = order;
            }
        }
        if (top is not null) return top;

        // Content 存在但没有任何图层
        var fallback = new CTLayer { ID = new STID(nextId()) };
        content.AddLayer(fallback);
        return fallback;
    }

    /// <summary>获取绘制区域：优先页面自身 Area，否则使用文档默认页面区域。</summary>
    private static STBox GetPageBox(PageObject pageObj, Document document)
    {
        var area = pageObj.Area
            ?? document.CommonData?.PageArea
            ?? throw new InvalidOperationException("无法确定页面区域（缺少 Area 与 CommonData/PageArea）");
        var physical = area.PhysicalBox
            ?? throw new InvalidOperationException("页面区域缺少 PhysicalBox");
        return new STBox(physical.TopLeftX, physical.TopLeftY, physical.Width, physical.Height);
    }

    // ── 区域占位区块填充：核心实现 ───────────────────────────────

    /// <summary>
    /// 定位登记区域（对齐 Java AreaHolderContext.get 前半：AreaHolderBlocksProcess 查找 + boundary/pageLoc/blockId 提取）。
    /// </summary>
    private static (STBox Boundary, string ContentPath, long BlockId)? FindAreaHolder(
        Dictionary<string, byte[]> map, DocContext ctx, string areaName)
    {
        var ahbPath = $"{ctx.DocDirName}/AreaHolderBlocks.xml";
        if (!map.TryGetValue(ahbPath, out var ahbBytes))
            return null; // 无区域占位区块列表文件（Java AreaHolderBlocksProcess.exist==false）
        var blocks = new Areaholder.AreaHolderBlocks(
            XDocument.Parse(Encoding.UTF8.GetString(ahbBytes)).Root!);
        var holder = Areaholder.AreaHolderBlocksProcess.Find(blocks, areaName);
        if (holder is null)
            return null; // 未找到区域（Java find==null）
        var pageLoc = holder.GetPageFile()?.Value;      // 绝对 loc：/Doc_0/Pages/Page_0/Content.xml
        var blockId = holder.GetPageBlockID()?.Value;   // 占位 PageBlock 的对象 ID
        var boundary = holder.GetBoundary();
        if (pageLoc is null || blockId is null || boundary is null)
            return null; // 登记缺关键元组（Java blockId/pageLoc/boundary==null）
        var contentPath = ZipMapIO.Normalize(pageLoc.TrimStart('/'));
        if (!map.ContainsKey(contentPath))
            return null; // 页面内容文件不存在
        return (boundary, contentPath, blockId.Value);
    }

    /// <summary>只读查询登记区域边界（区域不存在或边界无效返回 false）。</summary>
    internal static bool TryGetAreaHolderCore(Dictionary<string, byte[]> map, string areaName, out STBox boundary)
    {
        boundary = default!;
        if (string.IsNullOrEmpty(areaName)) return false;
        var ctx = LoadContext(map);
        var found = FindAreaHolder(map, ctx, areaName);
        if (found is null) return false;
        var b = found.Value.Boundary;
        if (b.Width <= 0 || b.Height <= 0) return false;
        boundary = b;
        return true;
    }

    /// <summary>
    /// 向登记区域填充内容（对齐 Java AreaHolderContext.get → AdditionVPage → OFDDoc#addVPage 的
    /// NetOfd 编辑会话等价物）：
    /// ① 按 areaName 定位登记（AreaHolderBlocks.xml）→ Boundary / PageFile / PageBlockID；
    /// ② 解析页面 Content.xml，按对象 ID 找到登记的空 PageBlock；
    /// ③ 以该 PageBlock 为图元容器创建绘制上下文（替代 AddContent 的「最上层图层」），
    ///    绘制内容直接挂入占位块——Java Canvas.setPreferBlock 语义，编辑期可再次定位。
    /// </summary>
    internal static bool FillAreaHolderCore(Dictionary<string, byte[]> map, string areaName, IDrawer drawer)
    {
        if (string.IsNullOrEmpty(areaName))
            throw new ArgumentException("区域名称（areaName）不能为空", nameof(areaName));
        if (drawer is null)
            throw new ArgumentNullException(nameof(drawer));

        var ctx = LoadContext(map);
        var found = FindAreaHolder(map, ctx, areaName);
        if (found is null) return false;
        var (boundary, contentPath, blockId) = found.Value;
        if (boundary.Width <= 0 || boundary.Height <= 0) return false; // 无效绘制区域（Java 同判）

        // ── 1. 初始化最大对象 ID 与资源提供器 ─────────────────────
        long maxUnitId = InitMaxUnitId(ctx);
        var provider = new MapResourceProvider(map, ctx.DocDirName, () => ++maxUnitId, ctx.Document.ObtainCommonData());

        // ── 2. 解析页面并定位登记的空 PageBlock（按 ID，任意层级）──
        var pageObj = new PageObject(ZipMapIO.LoadXml(map, contentPath).Root!);
        var idText = blockId.ToString();
        var blockEl = pageObj.Element.Descendants().FirstOrDefault(e =>
            e.Name.LocalName == "PageBlock" && (string?)e.Attribute("ID") == idText);
        if (blockEl is null) return false; // 页面中找不到占位块（Java XPath 同 null 返回）

        // ── 3. 以 PageBlock 为容器绘制并回写 ──────────────────────
        var box = GetPageBox(pageObj, ctx.Document);
        var container = new AreaHolderBlockContainer(blockEl);
        var drawCtx = new DrawContext(container, box, () => ++maxUnitId, provider);
        drawer.Draw(drawCtx);
        provider.Flush();

        map[contentPath] = ZipMapIO.ToUtf8Bytes(pageObj.Element);
        WriteBack(map, ctx, maxUnitId);
        return true;
    }

    /// <summary>
    /// 图元容器适配器：把绘制图元直接挂入既有 PageBlock 元素。
    /// CT_Layer.AddTextObject 等本质是 <c>_el.Add(entity._el)</c>（AddOFDEntity），
    /// 此处对 PageBlock 同式挂载（图元新建无节点漂移风险）。
    /// </summary>
    private sealed class AreaHolderBlockContainer : NetOfd.Core.BasicStructure.PageObj.Layer.IGraphicContainer
    {
        private readonly XElement _block;

        internal AreaHolderBlockContainer(XElement block) => _block = block;

        public void AddTextObject(CTText text) => _block.Add(text.Element);
        public void AddImageObject(CTImage image) => _block.Add(image.Element);
        public void AddPathObject(CTPath path) => _block.Add(path.Element);
    }

    /// <summary>
    /// 单元格化填充核心（对齐 Java <c>AreaHolderContext.getCell(name)</c>）：
    /// 以登记区域边界构造 <see cref="CellContentDrawer"/>（自动定位到区域绝对坐标），交由
    /// <paramref name="configure"/> 设置内容/样式后，复用 <see cref="FillAreaHolderCore"/> 绘制挂入 PageBlock。
    /// </summary>
    internal static bool FillAreaHolderCellCore(Dictionary<string, byte[]> map, string areaName, Action<CellContentDrawer> configure)
    {
        if (string.IsNullOrEmpty(areaName))
            throw new ArgumentException("区域名称（areaName）不能为空", nameof(areaName));
        if (configure is null)
            throw new ArgumentNullException(nameof(configure));

        var ctx = LoadContext(map);
        var found = FindAreaHolder(map, ctx, areaName);
        if (found is null) return false;
        var b = found.Value.Boundary;
        if (b.Width <= 0 || b.Height <= 0) return false; // 无效绘制区域

        var cell = new CellContentDrawer(b.TopLeftX, b.TopLeftY, b.Width, b.Height);
        configure(cell);
        return FillAreaHolderCore(map, areaName, cell);
    }
}
