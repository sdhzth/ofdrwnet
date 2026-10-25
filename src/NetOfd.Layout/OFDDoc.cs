using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Core.BasicStructure.OFD;
using NetOfd.Core.BasicStructure.PageObj;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription;
using NetOfd.Layout.Areaholder;
using NetOfd.Layout.Element;
using NetOfd.Layout.Element.Canvas;
using NetOfd.Layout.Engine;

namespace NetOfd.Layout;

/// <summary>
/// 虚拟文档对象
/// </summary>
public class OFDDoc : IDisposable
{
    internal Pkg.Container.OFDDir? _ofdDir;
    private readonly string? _outPath;
    private readonly Stream? _outStream;
    private long _maxUnitId;
    internal ResManager Prm { get; private set; } = null!;
    private readonly List<Div> _streamQueue = new();
    private readonly List<VirtualPage> _vPageList = new();

    /// <summary>
    /// 流式排版光标：当前流式内容所在页（文档最新页）内容区已排布的底部绝对 Y（mm）。
    /// <see cref="FlushStreamQueue"/> 结束后供表格等后续渲染接续（避免与段落流叠画）。
    /// </summary>
    private double _flowCursorY;

    // ── 浮动行状态（大缺口 #1 最小可观测子集，语义对齐 Java Segment 行模型）──────
    // Java 中一个 Segment（段）= 一行：多个可共享元素同行排布——left 浮动贴左依次右排、
    // right 浮动贴右依次左排（可同行共存）；center 浮动独占居中行；行内元素 Y 顶对齐、
    // 行高 = 行内最高元素整块高。独占元素（Paragraph/普通块级/表格，Java isBlockElement）
    // 不与浮动行共享 → 排布前先结束当前浮动行（光标推进到行底）。换页（AddPage）后
    // 浮动行状态整体重置（对齐 Java addNewPage：remainArea 重置，浮动不跨页延续）。
    private bool _floatRowActive;      // 当前页是否存在活动浮动行（行内已放入元素）
    private double _floatRowTop;       // 行顶 Y：行内首元素放入时刻的流光标（行内元素同 Y 顶对齐）
    private double _floatRowBottom;    // 行底 Y：行顶 + 行内最高元素整块高
    private double _floatRowLeftX;     // 行内已放 left 浮动元素的右缘（下一 left 落点，Java startX）
    private double _floatRowRightX;    // 行内已放 right 浮动元素的左缘（下一 right 落点，Java endX）
    // ── 居中行状态（Java isCenterFloat：多个连续 center 元素共享一行、整体水平居中）──
    private bool _floatRowIsCenter;                    // 活动行是否为 center 居中行
    private double _floatRowCenterStartX;              // 居中组起始 X（首个元素落点，未知总宽前暂为 0）
    private double _floatRowCenterTotalW;              // 居中组累计总宽（用于最终整体居中偏移）
    private readonly List<Div> _floatRowCenterDivs = new(); // 本次居中行已放入的元素（EndFloatRow 时整体对齐）

    /// <summary>模板页目录名自增计数器（Doc_N/Tpl_M），保证整个文档内目录名唯一</summary>
    private int _nextTplIndex;

    // ── AreaHolderBlock 写侧消费（对齐 Java AreaHolderBlockRender，2026-09-04 增量 I）──
    // 渲染到 AreaHolderBlock 元素时：向当前页图层插入空 PageBlock（仅 ID，占位）并登记
    // AreaHolderBlocks.xml（boundary/pageLoc/pageBlockID）。登记在 Flush 末尾统一落盘。
    private string? _renderPageLoc;                                    // 当前渲染页面 XML 绝对 loc（"/Doc_0/Pages/Page_0/Content.xml"）
    private AreaHolderBlocks? _areaHolderBlocks;                       // 登记缓存（懒 Obtain，Flush 末尾 PutFile）

    // 编辑模式（打开既有 OFD 从末页续写，对齐 Java OFDDoc(reader,out)+addStreamCollect）
    private bool _editMode;                                             // 是否编辑模式
    private double _editStartY;                                        // 续写起点 Y（末页已有内容底，无图元时为内容区顶）

    // 持有对 Document 对象的引用，以便 Flush 时更新 MaxUnitID
    private Document? _ofdDocument;
    private CTCommonData? _commonData;

    /// <summary>页面样式（默认 A4）</summary>
    private PageLayout _pageLayout = PageLayout.A4();

    public double Width
    {
        get => _pageLayout.Width;
        set => _pageLayout.Width = value;
    }
    public double Height
    {
        get => _pageLayout.Height;
        set => _pageLayout.Height = value;
    }
    public double MarginLeft
    {
        get => _pageLayout.MarginLeft;
        set => _pageLayout.MarginLeft = value;
    }
    public double MarginTop
    {
        get => _pageLayout.MarginTop;
        set => _pageLayout.MarginTop = value;
    }
    public double MarginRight
    {
        get => _pageLayout.MarginRight;
        set => _pageLayout.MarginRight = value;
    }
    public double MarginBottom
    {
        get => _pageLayout.MarginBottom;
        set => _pageLayout.MarginBottom = value;
    }

    private bool _disposed;

    public OFDDoc(string outPath)
    {
        _outPath = outPath ?? throw new ArgumentNullException(nameof(outPath));
        InitNew();
    }

    /// <summary>
    /// 以流为输出目标创建新文档（对齐 Java <c>OFDDoc(OutputStream)</c>）。
    /// 文档内容在 <see cref="Close"/> 时打包写入该流；流所有权归调用方（本类不关闭它）。
    /// </summary>
    public OFDDoc(Stream outStream)
    {
        _outStream = outStream ?? throw new ArgumentNullException(nameof(outStream));
        if (!outStream.CanWrite) throw new ArgumentException("输出流必须可写", nameof(outStream));
        InitNew();
    }

    // ── 编辑模式：打开既有 OFD 从末页续写（对齐 Java OFDDoc(reader,out)+addStreamCollect）──
    /// <summary>
    /// 编辑模式：打开既有 OFD 文档，从其末页内容底部续写流式元素（对齐 Java
    /// <c>OFDDoc(reader, out)</c> + <c>addStreamCollect</c> 的"在已有文档插入流式段"语义）。
    /// 追加的元素超出页面内容区时会自动分页并新建页面插入页树。
    /// 续写完成后通过 <see cref="Save(string)"/> 或 <see cref="Save(Stream)"/> 写回。
    /// </summary>
    public static OFDDoc OpenEdit(byte[] existingOfd)
    {
        if (existingOfd == null || existingOfd.Length == 0)
            throw new ArgumentNullException(nameof(existingOfd));
        var doc = new OFDDoc();
        doc._ofdDir = Pkg.Container.OFDDir.FromStream(new MemoryStream(existingOfd));
        var docDir = doc._ofdDir.GetDocByIndex(0)
            ?? throw new InvalidOperationException("既有 OFD 文档缺少 Doc_0");
        doc.LoadExisting(docDir);
        return doc;
    }

    /// <summary>无参构造（供 OpenEdit 内部使用，不触发 InitNew 新建文档）。</summary>
    private OFDDoc() { }

    /// <summary>
    /// 编辑模式初始化：加载既有文档状态（CommonData/PageLayout/ResManager/MaxUnitID），
    /// 并计算续写起点 Y = 末页已有图元底（无图元则为内容区顶）。
    /// </summary>
    private void LoadExisting(Pkg.Container.DocDir docDir)
    {
        _editMode = true;
        _ofdDocument = docDir.ObtainDocument();
        _commonData = _ofdDocument.CommonData;
        // 页面尺寸从既有 PhysicalBox 推导；边距沿用默认（与 NetOfd 生成文档一致）
        if (_commonData?.PageArea?.PhysicalBox is { } pb)
            _pageLayout = new PageLayout(pb.Width, pb.Height);
        _maxUnitId = _commonData?.MaxUnitID?.Value ?? 0;
        Prm = new ResManager(_ofdDir!, docDir, () => ++_maxUnitId);
        _editStartY = ComputeLastPageBottom(docDir);
    }

    /// <summary>计算末页已有图元的底部 Y（续写起点）。</summary>
    private double ComputeLastPageBottom(Pkg.Container.DocDir docDir)
    {
        var pageDir = docDir.GetLatestPage();
        var pageObj = pageDir?.ObtainContent();
        if (pageObj == null) return MarginTop;
        double maxBottom = MarginTop;
        foreach (var layer in pageObj.ObtainContent().GetLayers())
            foreach (var go in layer.GetGraphicObjects())
            {
                var b = go.Boundary;
                if (b != null) maxBottom = Math.Max(maxBottom, b.TopLeftY + b.Height);
            }
        return maxBottom;
    }

    private void InitNew()
    {
        // 1. 创建 OFD 根目录
        _ofdDir = new Pkg.Container.OFDDir();

        // 2. 构建 OFD.xml：包含 DocBody -> DocRoot 指向 Doc_0/Document.xml
        var docBody = new DocBody
        {
            DocRoot = new STLoc("Doc_0/Document.xml")
        };
        var ofdRoot = new OFDRoot();
        ofdRoot.AddDocBody(docBody);
        _ofdDir.SetOfd(ofdRoot);

        // 3. 构建 Document.xml：CommonData（PageArea）+ 空 Pages
        _commonData = new CTCommonData();
        _commonData.MaxUnitID = new STID(1);
        // 设置默认页面区域（A4）
        var pageArea = _commonData.ObtainPageArea();
        pageArea.PhysicalBox = new STBox(0, 0, Width, Height);
        pageArea.ApplicationBox = new STBox(0, 0, Width, Height);

        _ofdDocument = new Document();
        _ofdDocument.CommonData = _commonData;
        _ofdDocument.Pages = new NetOfd.Core.BasicStructure.PageTree.Pages();

        // 4. 写入 Doc_0 目录
        var docDir = _ofdDir.ObtainDoc();
        docDir.SetDocument(_ofdDocument);

        // 5. 初始化资源管理器
        Prm = new ResManager(_ofdDir, docDir, () => ++_maxUnitId);
    }

    /// <summary>添加流式布局元素</summary>
    public OFDDoc Add(Div element)
    {
        if (element == null) throw new ArgumentNullException(nameof(element));
        _streamQueue.Add(element);
        return this;
    }

    /// <summary>
    /// 设置页面默认样式。
    /// </summary>
    public OFDDoc SetDefaultPageLayout(PageLayout pageLayout)
    {
        if (pageLayout == null) return this;
        _pageLayout = pageLayout;
        if (_commonData != null)
        {
            var pageArea = _commonData.ObtainPageArea();
            pageArea.PhysicalBox = new STBox(0, 0, pageLayout.Width, pageLayout.Height);
            pageArea.ApplicationBox = new STBox(0, 0, pageLayout.Width, pageLayout.Height);
        }
        return this;
    }

    /// <summary>
    /// 获取页面样式（只读副本）。
    /// </summary>
    public PageLayout GetPageLayout() => _pageLayout.Clone();

    /// <summary>
    /// 向文档中加入虚拟页面（适合固定布局）。
    /// </summary>
    public OFDDoc AddVPage(VirtualPage virtualPage)
    {
        if (virtualPage == null) throw new ArgumentNullException(nameof(virtualPage));
        _vPageList.Add(virtualPage);
        return this;
    }

    /// <summary>
    /// 给整个文档增加水印（对已有页面逐页添加）。
    /// </summary>
    public OFDDoc AddWatermark(Edit.Watermark watermark)
    {
        if (watermark == null) return this;
        // 将水印追加到已创建的每个页面的前景层
        var docDir = _ofdDir!.GetDocByIndex(0);
        if (docDir == null) return this;
        var pageCount = docDir.GetPagesCount();
        for (int i = 0; i < pageCount; i++)
        {
            var pageDir = docDir.ObtainPages().ObtainPageDir(i);
            var pageObj = pageDir.ObtainContent();
            var content = pageObj.ObtainContent();
            var layer = content.GetLayers().FirstOrDefault(l => l.LayerType == LayerType.Foreground);
            if (layer == null)
            {
                layer = new CTLayer { LayerType = LayerType.Foreground, ID = new STID(++_maxUnitId) };
                content.AddLayer(layer);
            }
            var ctx = new Element.Canvas.DrawContext(layer, watermark.Boundary, () => ++_maxUnitId, Prm);
            watermark.Drawer.Draw(ctx, watermark.Boundary);
            pageDir.SetContent(pageObj);
        }
        return this;
    }

    /// <summary>
    /// 新页回调（对应 Java <c>VPageHandler</c>/<c>setOnPage</c> 语义）：每当新页面创建并注册后触发，
    /// 覆盖流式自动分页、首张初始页与手动 <see cref="AddPage"/>。参数为文档自身与 0 起算页序号。
    /// 典型用途：自动页眉/页脚、页码。回调内请用 <see cref="DrawFixedDiv"/> 立即绘制每页固定内容；
    /// 不要在回调中调用 <see cref="AddPage"/>（会无限递归）或 <see cref="Add"/>（流式队列语义在回调中不成立）。
    /// </summary>
    public Action<OFDDoc, int>? OnNewPage { get; set; }

    /// <summary>添加新的固定页面（用于 DrawContext）</summary>
    public OFDDoc AddPage()
    {
        var docDir = _ofdDir!.GetDocByIndex(0);
        if (docDir == null) return this;

        // 换页后浮动行整体重置（对齐 Java addNewPage：新页 remainArea 重置，浮动行不跨页延续）
        _floatRowActive = false;
        _floatRowTop = 0;
        _floatRowBottom = 0;
        _floatRowLeftX = 0;
        _floatRowRightX = 0;
        _floatRowIsCenter = false;
        _floatRowCenterStartX = 0;
        _floatRowCenterTotalW = 0;

        // 创建新页面目录（Pages/Page_N）
        var pageDir = docDir.ObtainNewPage();

        // Content.xml —— 存储 <Page> 元素（包含页面尺寸 + Content/Layer）
        // 对应 Java: PageDir.setContent(Page page)，Content.xml 中的根元素是 <ofd:Page>
        var pageObj = new PageObject();
        // 写入 Content.xml（根节点为 <Page>）
        pageDir.PutFile("Content.xml", pageObj.Element);

        // 向 Document.xml 的 Pages 注册此页，BaseLoc 指向 Content.xml
        RegisterPageInDocument(docDir, pageDir);

        // 记录当前渲染页绝对 loc（AreaHolderBlock 登记 PageFile 用，对齐 Java ST_Loc 绝对路径）
        _renderPageLoc = $"/{docDir.Name}/Pages/{pageDir.Name}/Content.xml";

        // 新页回调（对应 Java VPageHandler）：页面就绪后触发，供回调画页眉/页脚等固定内容
        OnNewPage?.Invoke(this, docDir.GetPagesCount() - 1);

        return this;
    }

    /// <summary>向 Document.xml 的 Pages 节点注册新页面</summary>
    private void RegisterPageInDocument(Pkg.Container.DocDir docDir, Pkg.Container.PageDir pageDir)
    {
        var doc = docDir.ObtainDocument();
        var pages = doc.ObtainPages();

        // 页面 ID 用自增 ID
        var pageId = new STID(++_maxUnitId);
        // BaseLoc 指向 Content.xml（Java 规范：Page BaseLoc 指向页面内容文件）
        var baseLoc = new STLoc($"Pages/{pageDir.Name}/Content.xml");

        var pageRef = new NetOfd.Core.BasicStructure.PageTree.Page
        {
            ID = pageId,
            BaseLoc = baseLoc
        };
        pages.AddPage(pageRef);
    }

    /// <summary>创建绘制上下文</summary>
    public Element.Canvas.DrawContext CreateDrawContext(STBox boundary)
    {
        var docDir = _ofdDir!.GetDocByIndex(0);
        var pageDir = docDir?.GetLatestPage();
        if (pageDir == null)
        {
            AddPage();
            docDir = _ofdDir!.GetDocByIndex(0);
            pageDir = docDir!.GetLatestPage();
        }

        // Content.xml 根节点是 <Page>（Java 规范），从中获取/创建 Content 子节点
        var contentFileEl = pageDir!.GetFile("Content.xml");
        PageObject pageObj;
        if (contentFileEl != null)
        {
            pageObj = new PageObject(contentFileEl);
        }
        else
        {
            pageObj = new PageObject();
            pageDir.PutFile("Content.xml", pageObj.Element);
        }

        // 获取或创建 Body 图层
        var content = pageObj.ObtainContent();
        CTLayer bodyLayer;
        var existing = content.GetBodyLayer();
        if (existing != null)
        {
            bodyLayer = existing;
        }
        else
        {
            bodyLayer = new CTLayer { LayerType = LayerType.Body, ID = new STID(++_maxUnitId) };
            content.AddLayer(bodyLayer);
        }

        return new Element.Canvas.DrawContext(bodyLayer, boundary, () => ++_maxUnitId, Prm);
    }

    /// <summary>
    /// 将 Div 以其自身 X/Y 立即绘制到当前最新页（不进流式队列、不推进流光标、不参与自动分页）。
    /// 主要供 <see cref="OnNewPage"/> 回调绘制页眉/页脚等每页固定内容（对齐 Java VPageHandler 往
    /// VirtualPage 添加 Div 的用法），也可单独使用。页面尚不存在时会先建页（届时会再次触发 OnNewPage）。
    /// </summary>
    public OFDDoc DrawFixedDiv(Div div)
    {
        ArgumentNullException.ThrowIfNull(div);
        var docDir = _ofdDir!.GetDocByIndex(0);
        if (docDir == null) return this;
        if (docDir.GetPagesCount() == 0) AddPage();
        RenderDiv(CreateDrawContext(new STBox(0, 0, Width, Height)), div);
        return this;
    }

    /// <summary>添加水印</summary>
    public OFDDoc AddWatermark(string text, Action<WatermarkOption>? configure = null)
    {
        var option = new WatermarkOption();
        configure?.Invoke(option);
        var watermark = new Edit.Watermark(-30, -30, Width + 60, Height + 60)
            .SetValue(string.IsNullOrEmpty(text) ? option.Text : text)
            .SetColor(option.Color)
            .SetFontName(option.Font)
            .SetFontSize(option.FontSize)
            .SetGlobalAlpha(option.Opacity)
            .SetAngle(option.Angle);
        return AddWatermark(watermark);
    }

    /// <summary>关闭文档并写出</summary>
    public void Close()
    {
        if (_disposed) return;
        Flush();
        // 编辑模式下不在此写盘（避免覆盖既有文件），写盘由 Save 负责；仅刷新内存状态
        if (!_editMode)
        {
            if (_outStream is not null) _ofdDir?.Jar(_outStream);
            else _ofdDir?.Jar(_outPath!);
        }
    }

    /// <summary>编辑模式：将修改后的文档写入指定路径。</summary>
    public void Save(string outPath)
    {
        if (_ofdDir == null) return;
        Flush();
        _ofdDir.Jar(outPath);
    }

    /// <summary>编辑模式：将修改后的文档写入指定流。</summary>
    public void Save(Stream outStream)
    {
        if (_ofdDir == null) return;
        Flush();
        _ofdDir.Jar(outStream);
    }

    /// <summary>
    /// 底层 OFD 包容器（对齐 Java <c>getOfdDir()</c>）。构造后即可访问，
    /// 用于在 <see cref="Close"/> 前对包体做检查/扩展。
    /// </summary>
    public Pkg.Container.OFDDir GetOfdDir() => _ofdDir!;

    /// <summary>
    /// 当前组装中的文档树根（Document.xml 模型，含 CommonData/Pages 状态）。
    /// 对齐 Java <c>getOfdDocument()</c>。
    /// </summary>
    public Document GetOfdDocument() => _ofdDocument!;

    private void Flush()
    {
        if (_ofdDir == null) return;
        var docDir = _ofdDir.GetDocByIndex(0);
        if (docDir == null) return;

        // 处理流式布局：若有元素但还没有页，先建一页
        if (_streamQueue.Count > 0 && docDir.GetPagesCount() == 0)
            AddPage();

        if (_streamQueue.Count > 0)
        {
            FlushStreamQueue();
            _streamQueue.Clear();
        }

        // 处理虚拟页面（固定布局）
        if (_vPageList.Count > 0)
        {
            foreach (var vPage in _vPageList)
                RenderVPage(docDir, vPage);
            _vPageList.Clear();
        }

        if (docDir.GetPagesCount() == 0)
        {
            throw new InvalidOperationException("OFD文档中没有页面，无法生成OFD文档");
        }

        // ── 注册字体资源到 PublicRes.xml ─────────────────────────────
        RegisterFontResources(docDir);

        // ── 注册多媒体资源（图像）到 DocumentRes.xml ─────────────────
        RegisterMediaResources(docDir);

        // ── AreaHolderBlock 登记落盘（渲染期已累积，Jar 前统一持久化）──
        if (_areaHolderBlocks != null)
            docDir.PutFile(AreaHolderBlocks.AreaHolderBlocksFile, _areaHolderBlocks.Element);

        // 更新 Document.xml 中的 MaxUnitID
        if (_commonData != null)
        {
            _commonData.MaxUnitID = new STID(_maxUnitId + 1);
            // 把 Document 对象重新写入（确保持久化最新状态）
            docDir.SetDocument(docDir.ObtainDocument());
        }
    }

    /// <summary>
    /// 结束当前活动浮动行：光标推进到行底并复位行状态。
    /// 对齐 Java analyze() 中每个 Segment 依次占用 remainArea 垂直空间（行推进）——
    /// 独占元素（段落/普通块级/表格）排布前必须先结束浮动行，避免与浮动元素叠画。
    /// </summary>
    private double EndFloatRow(double cursorY)
    {
        if (!_floatRowActive) return cursorY;

        // 居中行：总宽已知后，把整组水平居中并统一渲染（Java elementPositioning：offsetX = areaX + (areaW - totalW)/2）
        if (_floatRowIsCenter)
        {
            double areaLeft = _floatRowLeftX;
            double areaRight = _floatRowRightX;
            double areaW = areaRight - areaLeft;
            double offset = areaLeft + (areaW - _floatRowCenterTotalW) / 2;
            double shift = offset - _floatRowCenterStartX;
            foreach (var div in _floatRowCenterDivs)
            {
                if (Math.Abs(shift) > 0.0001) div.X = (div.X ?? 0) + shift;
                RenderFloatDiv(div, div.X ?? 0, _floatRowTop);
            }
        }

        if (_floatRowBottom > cursorY) cursorY = _floatRowBottom;
        _floatRowActive = false;
        _floatRowTop = 0;
        _floatRowBottom = 0;
        _floatRowLeftX = 0;
        _floatRowRightX = 0;
        _floatRowIsCenter = false;
        _floatRowCenterStartX = 0;
        _floatRowCenterTotalW = 0;
        _floatRowCenterDivs.Clear();
        return cursorY;
    }

    /// <summary>
    /// 渲染浮动块到指定行位置：X = 行内落点（contentLeft 起的绝对页坐标），
    /// Y 与块级分支同公式（光标 + 上侧 margin/border/padding）。占位符只占空间不绘制
    /// （Java elementPositioning：placeholder 跳过绘制，仅用于行内占位）。
    /// </summary>
    private void RenderFloatDiv(Div div, double x, double rowTop)
    {
        div.X = x;
        div.Y = rowTop + div.MarginTop + div.BorderTop + div.PaddingTop;
        if (div.IsPlaceholder()) return;
        RenderDiv(CreateDrawContext(new STBox(0, 0, Width, Height)), div);
    }

    /// <summary>
    /// 渲染浮动块 / 行内共享元素（<see cref="Div.Float"/> 非 None，或 <see cref="Div.Clear"/>
    /// 非 Both 的 InlineBlock 元素），对齐 Java Segment 行模型 tryAdd/isBlockElement：
    /// <list type="bullet">
    /// <item><see cref="AFloat.Left"/>：贴内容区左缘，行内已放 left 元素依次向右（Java startX 累加）；
    /// <see cref="AFloat.None"/> 的 InlineBlock 元素归一 Left（Java <c>Div.aFloat</c> 默认 left，
    /// clear=none 即行内共享贴左）；</item>
    /// <item><see cref="AFloat.Right"/>：贴右缘向左（Java endX 递减），可与行内 left 元素同行共享；</item>
    /// <item><see cref="AFloat.Center"/>：独占一行、水平居中（Java isCenterFloat：居中行不混入其它浮动）；</item>
    /// <item>独占判定（Java isBlockElement）：clear=both（Div 默认）/ right+clear:left 对立组合 →
    /// 不与浮动行共享，先结束当前行、自身成行独占；left+clear:right 在 Java 中<b>非独占</b>（无专门
    /// tryAdd 拦截规则），按普通 left 浮动共享——修正此前「三项独占」过度判定；</item>
    /// <item>clear 冲突（Java tryAdd false 开新 Segment）：left+clear:left 且行内已有 left 浮动、
    /// right+clear:right 且行内已有 right 浮动 → 结束当前行换新行。</item>
    /// </list>
    /// 浮动块行首可拆纯盒超高时按 split 切断跨页（对齐 Java segmentBlocking：integrity=false 的
    /// 普通 Div 于切缝断开，上半段填满本页剩余、下半段续排下一页）；含内容/图片/画布/独占/居中
    /// 元素保持整块移入新页。行内共享元素保持光标冻结在行顶（行结束由 <see cref="EndFloatRow"/>
    /// 统一推进到行底）。
    /// </summary>
    private double HandleFloatDiv(Div div, int streamIndex, double cursorY, double contentLeft, double contentWidth, double contentBottom)
    {
        double contentRight = contentLeft + contentWidth;
        double contentTop = MarginTop;
        // Java Div.aFloat 默认 left：InlineBlock（clear=none + Float=None）元素按左浮动共享贴左
        var f = div.Float == AFloat.None ? AFloat.Left : div.Float;
        if (div.Width is null && div is not Img) div.Width = contentWidth;
        // Java doPrepare：w = contentWidth + widthPlus()（含 margin/padding/border 水平总和）
        double w = (div.Width ?? contentWidth) + div.WidthPlus();

        // 整块高（与块级分支 blockH 同公式：height + heightPlus）
        double blockH = (div.Height ?? 0) + div.HeightPlus();

        // ── 独占判定（Java isBlockElement：Both || (Right && Clear:Left)）→ 不与浮动行共享 ──
        bool exclusive = div.Clear == Clear.Both
            || (f == AFloat.Right && div.Clear == Clear.Left);

        // ── center：尝试加入活动居中行（Java tryAdd：段内已全为 center 时可续加；若已有 left/right 元素则不可混入）──
        if (f == AFloat.Center)
        {
            // 居中行区域固定为整个内容区（leftX=contentLeft / rightX=contentRight 不随元素缩减），
            // 行内已放元素总宽 _floatRowCenterTotalW，剩余 = 内容宽 - 累计总宽
            double remainW = (_floatRowRightX - _floatRowLeftX) - _floatRowCenterTotalW;
            bool canJoinCenter = _floatRowActive && _floatRowIsCenter
                && div.Clear != Clear.Left                                    // 新元素 clear:left 不可入居中段
                && w <= remainW + 0.01                                        // 剩余宽度足
                && _floatRowTop + blockH <= contentBottom + 0.01;
            if (canJoinCenter)
            {
                double x = _floatRowCenterStartX + _floatRowCenterTotalW;    // 组内顺序向右
                div.X = x;                                                   // 占位 X（暂不渲染）
                _floatRowCenterDivs.Add(div);
                _floatRowCenterTotalW += w;
                if (_floatRowTop + blockH > _floatRowBottom) _floatRowBottom = _floatRowTop + blockH;
                return cursorY; // 光标冻结在行顶，行结束时 EndFloatRow 整体居中后渲染
            }
            // 不能加入 → 结束当前行（非居中或放不下），然后自身开新居中行
            cursorY = EndFloatRow(cursorY);
        }
        else if (exclusive)
        {
            // 独占元素：结束当前行后自身成行独占
            cursorY = EndFloatRow(cursorY);
        }
        else if (_floatRowActive && _floatRowIsCenter && f != AFloat.Center)
        {
            // 活动行为居中行，却来了非 center 元素 → 居中行放不下，结束之（Java 段模型：非 center 不可入居中段）
            cursorY = EndFloatRow(cursorY);
        }

        if (f == AFloat.Center || exclusive)
        {
            // 垂直剩余不足且非页首 → 整块移入新页（浮动块不可拆）
            if (cursorY > contentTop + 0.01 && blockH > contentBottom - cursorY + 0.01)
            {
                AddPage(); // 内部已重置浮动行状态
                cursorY = contentTop;
            }
            double x = f == AFloat.Center
                ? contentLeft  // center 组起始：先贴 contentLeft 顺序排，EndFloatRow 时整体居中
                : f == AFloat.Right ? contentRight - w : contentLeft; // 独占 left/right 仍贴相应边
            if (f == AFloat.Center)
            {
                // 开新的居中行：推迟渲染，行结束（EndFloatRow）时按总宽居中后统一绘制
                div.X = x;
                _floatRowActive = true;
                _floatRowIsCenter = true;
                _floatRowTop = cursorY;
                _floatRowBottom = cursorY + blockH;
                _floatRowLeftX = contentLeft;
                _floatRowRightX = contentRight;
                _floatRowCenterStartX = x;
                _floatRowCenterTotalW = w;
                _floatRowCenterDivs.Clear();
                _floatRowCenterDivs.Add(div);
                return cursorY; // 行顶冻结（行结束统一居中渲染）
            }
            RenderFloatDiv(div, x, cursorY);
            return cursorY + blockH;
        }

        // ── 非独占 left/right：尝试与活动浮动行水平共享 ──
        if (_floatRowActive)
        {
            bool hasLeft = _floatRowLeftX > contentLeft + 0.01;
            bool hasRight = _floatRowRightX < contentRight - 0.01;
            // Java tryAdd：left+clear:left 且行内已有 left、right+clear:right 且行内已有 right → 冲突
            bool conflict = (f == AFloat.Left && div.Clear == Clear.Left && hasLeft)
                         || (f == AFloat.Right && div.Clear == Clear.Right && hasRight);
            double remainW = _floatRowRightX - _floatRowLeftX;
            if (!conflict && w <= remainW + 0.01 && _floatRowTop + blockH <= contentBottom + 0.01)
            {
                double rowTop = _floatRowTop;
                double x = f == AFloat.Left ? _floatRowLeftX : _floatRowRightX - w;
                if (f == AFloat.Left) _floatRowLeftX += w; else _floatRowRightX -= w;
                if (rowTop + blockH > _floatRowBottom) _floatRowBottom = rowTop + blockH;
                RenderFloatDiv(div, x, rowTop);
                return cursorY; // 光标冻结在行顶，行结束时由 EndFloatRow 推进
            }
            // 水平放不下 / clear 冲突 → 结束当前行，光标推进到行底后开新行
            cursorY = EndFloatRow(cursorY);
        }

        // ── 开新行：垂直换页检查后放入行首（行内首个元素）──
        if (cursorY > contentTop + 0.01 && blockH > contentBottom - cursorY + 0.01)
        {
            // 可拆纯盒浮动元素跨页（对齐 Java segmentBlocking：integrity=false 的普通 Div 在剩余
            // 空间不足时于切缝处断开——上半段填满本页剩余，下半段回流队列于新页续排；
            // 含内容/图片/画布/编辑区占位等元素保持整块移入新页，避免内容截断丢失）。
            // 仅处理非 center / 非独占的 left/right 开新行场景（独占/center 走上方第 604 行旧逻辑）。
            double remainH = contentBottom - cursorY;
            bool splitEligible = !div.IsIntegrity()
                                 && div.ElementType == "Div"
                                 && div is not Canvas
                                 && div.Height is not null
                                 && remainH > 0.5;
            if (splitEligible && blockH > remainH)
            {
                var halves = div.Split(remainH);
                var rest = halves[1];
                rest.Float = f == AFloat.Right ? AFloat.Right : AFloat.Left;
                div = halves[0];
                // 上半段以自身贴边排满本页剩余（X 语义与开行一致），随即结束该浮动行并把光标推到
                // 页底，使下半段于本页末尾被回收入队、在新页首重新进入行逻辑续排
                double sx = f == AFloat.Right ? contentRight - w : contentLeft;
                RenderFloatDiv(div, sx, cursorY);
                cursorY = cursorY + (div.Height ?? 0) + div.HeightPlus();
                _streamQueue.Insert(streamIndex + 1, rest);
                return cursorY;
            }
            AddPage(); // 内部已重置浮动行状态
            cursorY = contentTop;
        }
        _floatRowActive = true;
        _floatRowTop = cursorY;
        _floatRowBottom = cursorY + blockH;
        _floatRowLeftX = contentLeft + (f == AFloat.Left ? w : 0);
        _floatRowRightX = contentRight - (f == AFloat.Right ? w : 0);
        double nx = f == AFloat.Left ? contentLeft : contentRight - w;
        RenderFloatDiv(div, nx, cursorY);
        return cursorY; // 行顶冻结
    }

    /// <summary>
    /// 流式布局排版（引擎 v1）：将 <see cref="Add"/> 收集的块级元素沿页面 y 轴流式堆叠——
    /// 维护排版光标逐块向下推进，块/行超出页面内容区底部时自动 <see cref="AddPage"/> 续排。
    /// 定位语义如下：
    /// </summary>
    /// <list type="bullet">
    /// <item><see cref="Position.Absolute"/>：保持元素自身 X/Y 不参与堆叠，直接画在当前最新页（不推进光标）；</item>
    /// <item><see cref="PageAreaFiller"/>：命令元素，使后续内容强制换页（队尾 filler 不产生多余空页）；</item>
    /// <item><see cref="Paragraph"/>：按行排版，行超出页底自动分页并续画（行级跨页，不截断文字）；</item>
    /// <item>其余块级（Div/Img/Canvas）：按盒高整块排布，超出页底则整块移入新页。</item>
    /// </list>
    /// <remarks>
    /// 流式内容从文档「最新页」的内容区顶部开始排布；引擎产生的自动分页会创建新页并持续推进。
    /// 排版结束后光标存于 <see cref="_flowCursorY"/>，供表格等后续渲染接续。
    /// </remarks>
    private void FlushStreamQueue()
    {
        var docDir = _ofdDir!.GetDocByIndex(0);
        if (docDir == null || _streamQueue.Count == 0) return;

        double contentTop = MarginTop;
        double contentBottom = Height - MarginBottom;
        double contentLeft = MarginLeft;
        double contentWidth = Math.Max(Width - MarginLeft - MarginRight, 1.0);

        // 起始页：无页先建一页；有页（含用户手动 AddPage 的页）则从最新页开始排版
        if (docDir.GetPagesCount() == 0) AddPage();
        // 编辑模式从末页已有内容底续写，新建模式从内容区顶起排
        double cursorY = _editMode ? _editStartY : contentTop;

        for (int i = 0; i < _streamQueue.Count; i++)
        {
            var div = _streamQueue[i];
            if (div == null) continue;

            // ── 绝对定位：原 X/Y 画在当前最新页，不参与流式堆叠 ──
            if (div.Position == Position.Absolute)
            {
                RenderDiv(CreateDrawContext(new STBox(0, 0, Width, Height)), div);
                continue;
            }

            // ── 换行符 BR：强制结束当前浮动行（对齐 Java SegmentationEngine 遇 BR 封段开新段；
            //     行空（无活动浮动行）时忽略，Java 空段跳过同语义）──
            if (div is BR)
            {
                cursorY = EndFloatRow(cursorY);
                continue;
            }

            // ── 浮动块 / 行内共享元素：对齐 Java Segment tryAdd/isBlockElement 的净语义——
            // shareEligible = Clear != Both（InlineBlock/clear:none，Java aFloat 默认 left 贴左共享）
            //                 || Float != None（显式浮动，含 clear=both 独占贴边、center 独占居中）。
            // 纯独占块 = Clear==Both && Float==None（Div 默认）→ 走下方块级独占分支（Java isBlockElement）。
            // Paragraph（独占段）、TableHostDiv（独占表）、PageAreaFiller（分页占位）上的
            // Float/Display 属性不在此消费。 ──
            if ((div.Clear != Clear.Both || div.Float != AFloat.None)
                && div is not Paragraph && div is not TableHostDiv && div is not PageAreaFiller)
            {
                cursorY = HandleFloatDiv(div, i, cursorY, contentLeft, contentWidth, contentBottom);
                continue;
            }

            // ── PageAreaFiller：其后仍有内容才强制换页（队尾 filler 不产生多余空页）──
            if (div is PageAreaFiller)
            {
                bool hasFollowing = false;
                for (int j = i + 1; j < _streamQueue.Count; j++)
                {
                    if (_streamQueue[j] is not PageAreaFiller && _streamQueue[j] is not null)
                    { hasFollowing = true; break; }
                }
                if (hasFollowing)
                {
                    AddPage();
                    cursorY = contentTop;
                }
                continue;
            }

            // ── 表格（自研扩展）：流元素载体，按 Add 书写顺序在段落流中就地排版 ──
            if (div is TableHostDiv tableHost)
            {
                var t = tableHost.Table;
                if (t.RowCount <= 0 || t.ColumnCount <= 0) continue; // 空表无内容，跳过

                // 表格独占：若当前页有活动浮动行，先结束（光标推进到行底），避免与浮动元素叠画
                cursorY = EndFloatRow(cursorY);

                // 显式 Y（>=0）→ 精确定位语义（Absolute 化），画完不推进流光标
                if (t.Y >= 0)
                {
                    RenderTable(t);
                    continue;
                }

                // 流式：X 默认从左页边距起、宽默认占满内容宽（修复旧 AddTable 一律
                // 从 X=0 顶页边排布、无视 MarginLeft/Right 的越界缺陷）
                if (t.X <= 0) t.X = contentLeft;
                if (t.Width == null) t.Width = contentWidth;

                // 页底余量不足「表头/标题 + 至少 1 行」时整表移入新页，避免表头孤悬页底
                double headRows = t.RepeatHeader ? Math.Min(t.HeaderRows, t.RowCount) : 0;
                double minH = (headRows + 1) * t.RowHeight
                              + (string.IsNullOrEmpty(t.Title) ? 0 : t.RowHeight);
                if (cursorY > contentTop + 0.01 && contentBottom - cursorY < minH)
                {
                    AddPage();
                    cursorY = contentTop;
                }

                // 表格接续流光标排布（RenderTable 内部自动跨页翻页/重复表头），
                // 结束后 _tableCursorY = 表格末页底部 + 表间距，流光标随之推进
                _tableCursorY = cursorY;
                RenderTable(t);
                cursorY = _tableCursorY;
                continue;
            }

            // ── 段落：行级排版（内部自动分页），光标按实际文本底推进 ──
            if (div is Paragraph para)
            {
                // 段落独占：先结束当前页的活动浮动行（Java Paragraph 默认 clear=both 独占段，
                // 不与浮动行共享——浮动行结束后段落从行底以下全宽续排，无文字环绕）
                cursorY = EndFloatRow(cursorY);
                // 未指定宽度的段落默认占满整行内容宽（替代旧 170mm 硬编码，避免越出右页边距）
                if (para.Width == null) para.Width = contentWidth;
                double px = para.X ?? contentLeft;
                para.X = px;
                para.Y = cursorY;
                // 相对定位偏移（段落不经 RenderDiv，此处单独施加 Left−Right / Top）
                ApplyRelativeOffset(para);

                double? lastBottom = RenderParagraph(
                    CreateDrawContext(new STBox(0, 0, Width, Height)), para, flowMode: true);

                if (lastBottom is double lb)
                {
                    // 段落盒底 = 最后文本行底 + 下 padding/border/margin（上侧已在段首消费）
                    cursorY = lb + para.PaddingBottom + para.BorderBottom + para.MarginBottom;
                }
                else
                {
                    // 无可见文本的段落：按一行占位推进，避免后续元素叠画
                    double lineH = Math.Max(para.DefaultFontSize * 1.2, 1.0);
                    cursorY += para.MarginTop + para.BorderTop + para.PaddingTop
                             + lineH + para.PaddingBottom + para.BorderBottom + para.MarginBottom;
                }
                continue;
            }

            // ── 其余块级（带盒 Div / Img / Canvas）：整块排布，可拆纯盒超高时按 split 切断跨页 ──
            // 普通块级独占：先结束当前页的活动浮动行（Java isBlockElement：clear=both 默认独占段）
            cursorY = EndFloatRow(cursorY);
            double mTop = div.MarginTop, mBottom = div.MarginBottom;
            double bTop = div.BorderTop, bBottom = div.BorderBottom;
            double pTop = div.PaddingTop, pBottom = div.PaddingBottom;
            double innerH = div.Height ?? 0;
            double blockH = mTop + bTop + pTop + innerH + pBottom + bBottom + mBottom;
            double usableH = contentBottom - contentTop;

            // 未显式定宽的块级默认占满整行内容宽（前移至分页判定前：split 切分要求宽度已知）
            if (div.Width == null && div is not Img) div.Width = contentWidth;

            // 可拆纯盒判定（对齐 Java segmentBlocking：integrity=false 的元素在剩余空间不足时
            // 调用 Div.split 于切缝处断开跨页；含内容的元素——段落(上游已拦截)/图片/画布/
            // 编辑区占位——保持整块移动，避免内容截断丢失）
            bool splitEligible = !div.IsIntegrity()
                                 && div.ElementType == "Div"
                                 && div is not Canvas
                                 && div.Height is not null;

            double remainH = contentBottom - cursorY;
            if (splitEligible && remainH > 0.5 && blockH > remainH)
            {
                // 在剩余空间处切断：上半段（div）占位恰填满本页剩余，下半段回流队列续排下一页。
                // 块高超一整页时（blockH > usableH）同样适用——div2 回流后于新页首继续切分，
                // 直到余段放得下，实现 Java 的超高块跨页连续（替代旧「移页首截断丢下半」行为）。
                var halves = div.Split(remainH);
                div = halves[0];
                _streamQueue.Insert(i + 1, halves[1]);
            }
            else if (cursorY > contentTop + 0.01 && blockH > remainH)
            {
                // 不可拆 / 含内容块 / 剩余不足半毫米：整块移入新页；块本身超一页且不可拆则
                // 放页首截断兜底（Java TODO：超高不可拆段应舍弃，此处保守保留可视部分）
                AddPage();
                cursorY = contentTop;
            }

            double bx = div.X ?? contentLeft;
            // 盒视觉左上 = 光标 + 上侧 margin/border/padding（与 RenderDiv 直接用 X/Y 画盒一致；
            // 上半段 split 已调整盒属性，此处按 div 实时属性计算）
            double by = cursorY + div.MarginTop + div.BorderTop + div.PaddingTop;
            div.X = bx;
            div.Y = by;
            RenderDiv(CreateDrawContext(new STBox(0, 0, Width, Height)), div);

            // 光标推进 = 内容高 + 下侧装饰（上半段占位恰为 remainH，光标自然落到页底）
            cursorY = by + (div.Height ?? 0) + div.PaddingBottom + div.BorderBottom + div.MarginBottom;
        }

        // 流式结束：收尾居中的浮动行——总宽已知，整体居中渲染并推进光标到行底
        cursorY = EndFloatRow(cursorY);
        _flowCursorY = cursorY;
    }

    /// <summary>
    /// 将注册的字体资源写入 PublicRes.xml
    /// </summary>
    private void RegisterFontResources(Pkg.Container.DocDir docDir)
    {
        var fonts = Prm.Fonts;
        if (fonts.Count == 0) return;

        // 获取或创建 PublicRes
        var publicRes = docDir.ObtainPublicRes();

        // 添加字体到 PublicRes（如果还没有的话）
        foreach (var font in fonts)
        {
            // 检查是否已存在
            var existingFonts = publicRes.GetFonts();
            var existingFont = existingFonts.FirstOrDefault(f => f.FontName == font.FontName);
            if (existingFont == null)
            {
                publicRes.AddFont(font);
            }
        }

        // 在 CommonData 中注册 PublicRes 路径
        if (_commonData != null && _commonData.PublicResList.Count == 0)
        {
            _commonData.AddPublicRes(new STLoc(Pkg.Container.DocDir.PUBLIC_RES_FILE));
        }
    }

    /// <summary>
    /// 将注册的多媒体资源写入 DocumentRes.xml，并在 CommonData 中登记路径。
    /// 早期实现漏写此步，导致图像字节虽在包内（Doc_0/Res/Mx.png）却无资源声明，
    /// 读者按 ResourceID 找不到 MultiMedia 而加载失败。
    /// </summary>
    private void RegisterMediaResources(Pkg.Container.DocDir docDir)
    {
        var medias = Prm.MultiMedias;
        if (medias.Count == 0) return;

        var documentRes = docDir.ObtainDocumentRes();
        foreach (var mm in medias)
        {
            // 去重：已声明过同 ID 的跳过
            var exists = documentRes.GetMultiMedias()
                .Any(m => m.ID.Value == mm.ID.Value);
            if (!exists) documentRes.AddMultiMedia(mm);
        }

        // 在 CommonData 中登记 DocumentRes 路径（相对 Doc_0 目录）
        if (_commonData != null && _commonData.DocumentResList.Count == 0)
        {
            _commonData.AddDocumentRes(new STLoc(Pkg.Container.DocDir.DOCUMENT_RES_FILE));
        }
    }

    /// <summary>
    /// 相对定位偏移（对齐 Java <c>StreamingLayoutAnalyzer.itemIntoVirtualPage</c>）：
    /// 流式引擎已为元素算好自动落位 X/Y，此处对 <see cref="Position.Relative"/> 元素再叠加
    /// <c>X += Left - Right</c>、<c>Y += Top</c> 偏移，随后转 <see cref="Position.Absolute"/>，
    /// 供绘制逻辑统一按绝对坐标处理。非 Relative 元素原样跳过。
    /// <para>幂等：转 Absolute 后二次进入不再偏移，便于 RenderFloatDiv→RenderDiv 复用而不重复施偏。</para>
    /// </summary>
    private static void ApplyRelativeOffset(Div div)
    {
        if (div.Position != Position.Relative) return;
        div.X = (div.X ?? 0) + (div.Left ?? 0) - (div.Right ?? 0);
        div.Y = (div.Y ?? 0) + (div.Top ?? 0);
        div.Position = Position.Absolute;
    }

    private void RenderDiv(Element.Canvas.DrawContext ctx, Div div)
    {
        // ── 相对定位偏移（对齐 Java StreamingLayoutAnalyzer.itemIntoVirtualPage）──
        // 流式自动落位的 X/Y 已在上游分支算好，此处仅对 Position.Relative 元素叠加
        // Left−Right（X）/ Top（Y）偏移，随后转 Absolute，使后续绘制逻辑统一按绝对坐标处理。
        // 幂等：转 Absolute 后二次进入不会重复偏移（Absolute 分支 guard 跳过）。
        ApplyRelativeOffset(div);

        ctx.Save();

        // ── 元素自身不透明度（Java Div.opacity）──────────────────────────────
        // GlobalAlpha 参与 Save/Restore 状态栈：本元素渲染结束后 Restore 自动复位，
        // 不影响同页面后续兄弟元素的绘制。
        if (div.Opacity is double alpha && alpha < 1.0)
            ctx.GlobalAlpha = alpha;

        // ── 渲染背景（仅当 BackgroundColor 有值时）───────────────────────────────
        if (div.BackgroundColor != null)
        {
            ctx.FillStyle = div.BackgroundColor;
            ctx.FillRect(div.X ?? 0, div.Y ?? 0, div.Width ?? 100, div.Height ?? 20);
        }

        // ── 渲染边框 ─────────────────────────────────────────────
        var borderWidth = div.BorderTop > 0 || div.BorderRight > 0 || div.BorderBottom > 0 || div.BorderLeft > 0
            ? Math.Max(Math.Max(div.BorderTop, div.BorderRight), Math.Max(div.BorderBottom, div.BorderLeft))
            : 0;
        if (borderWidth > 0)
        {
            ctx.StrokeStyle = div.BorderColor ?? new NetOfd.Core.PageDescription.Color.CT_Color { Value = "0 0 0" };
            ctx.LineWidth = borderWidth;

            // 虚线边框（Java Div.BorderDash 语义：[偏移, 线长, 空白, ...]，首元素为偏移量）
            if (div.BorderDash is { Length: >= 1 } bd)
            {
                ctx.LineDashOffset = bd[0] > 0 ? bd[0] : 0;
                if (bd.Length > 1)
                {
                    var pattern = new double[bd.Length - 1];
                    Array.Copy(bd, 1, pattern, 0, bd.Length - 1);
                    ctx.LineDash = pattern;
                }
            }

            ctx.StrokeRect(div.X ?? 0, div.Y ?? 0, div.Width ?? 100, div.Height ?? 20);

            // 复位虚线状态，避免影响同一 ctx 上后续子元素的绘制
            ctx.LineDash = null;
            ctx.LineDashOffset = 0;
        }

        // ── 图片 ────────────────────────────────────────────────
        if (div is Img img && img.Src is not null)
        {
            var mediaId = Prm.AddMedia(img.Src);
            ctx.DrawImage(mediaId, div.X ?? 0, div.Y ?? 0, div.Width ?? 0, div.Height ?? 0);
        }

        // ── 画布 ────────────────────────────────────────────────
        if (div is Canvas canvas && canvas.Drawer is not null)
        {
            var canvasCtx = ctx;
            canvas.Drawer.Draw(canvasCtx);
        }

        // ── 渲染 Paragraph 文字内容 ───────────────────────────────
        if (div is Paragraph paragraph)
        {
            RenderParagraph(ctx, paragraph);
        }

        // ── 区域占位区块：占位空 PageBlock + AreaHolderBlocks.xml 登记 ──
        // 对齐 Java VPageParseEngine.convert2Layer：DivRender 先绘盒（背景/边框/内容），
        // 再执行 AreaHolderBlockRender（空块占位 + 登记）。登记在 Flush 末尾统一落盘。
        if (div is AreaHolderBlock areaHolder)
        {
            RegisterAreaHolder(ctx, areaHolder);
        }

        ctx.Restore();
    }

    /// <summary>
    /// AreaHolderBlock 写侧消费（对齐 Java engine/render/AreaHolderBlockRender.render）：
    /// 1) 在当前渲染图层追加一个空 <c>PageBlock</c>（仅 ID，作为占位锚点供编辑期引用）；
    /// 2) 向 AreaHolderBlocks.xml 登记记录 { AreaName, Boundary, PageFile, PageBlockID, ID }。
    /// <para>
    /// Boundary 采用元素声明坐标 (X, Y, Width, Height)：NetOfd 无 Java 的 border-box 坐标概念，
    /// <c>div.X/Y</c> 即内容绘制区起点（流式引擎已把 Y 推进到含上装饰的内容顶，Java 同位置为
    /// box 左上 + Border/Padding 修正）。Java 公式 x+border+padding 的「去装饰」在 NetOfd 由坐标
    /// 约定自身承担——无装饰场景两库数值完全一致（Java 测试/样本均无 padding，border 仅视觉线）。
    /// </para>
    /// 登记暂存 <see cref="_areaHolderBlocks"/>，由 <see cref="Flush"/> 末尾统一 PutFile 落盘
    /// （渲染期仅改动内存 XElement，Jar 前持久化即可进入产物）。
    /// </summary>
    private void RegisterAreaHolder(Element.Canvas.DrawContext ctx, AreaHolderBlock ah)
    {
        string? name = ah.GetAreaName();
        double w = ah.Width ?? 0;
        double h = ah.Height ?? 0;
        if (_renderPageLoc is null || string.IsNullOrEmpty(name) || w <= 0 || h <= 0) return;
        if (ctx.TargetContainer is not CTLayer layer) return;

        // 1) 空 PageBlock 占位（仅 ID；Java 样本形态 <PageBlock ID="10"/>）
        long blockId = ++_maxUnitId;
        layer.AddPageBlock(new XElement(OFDElement.OfdNs + "PageBlock", new XAttribute("ID", blockId)));

        // 2) 登记（记录自身 ID 紧随 blockId 自增，Java maxUnitID 同序）
        var docDir = _ofdDir!.GetDocByIndex(0);
        if (docDir is null) return;
        _areaHolderBlocks ??= AreaHolderBlocksProcess.Obtain(docDir);
        var holder = new CT_AreaHolderBlock(name)
            .SetBoundary(new STBox(ah.X ?? 0, ah.Y ?? 0, w, h))
            .SetPageFile(new STLoc(_renderPageLoc))
            .SetPageBlockID(new STRefID(blockId));
        holder.SetObjID(new STID(++_maxUnitId));
        _areaHolderBlocks.AddAreaHolderBlock(holder);
    }

    /// <summary>
    /// 渲染虚拟页面（固定布局）：按图层分组创建 Layer 并绘制每个 Div。
    /// </summary>
    private void RenderVPage(Pkg.Container.DocDir docDir, VirtualPage vPage)
    {
        var pageDir = docDir.ObtainNewPage();
        var pageObj = new PageObject();

        // 记录当前渲染页绝对 loc（AreaHolderBlock 登记 PageFile 用，对齐 Java ST_Loc 绝对路径）。
        // RenderVPage 不经 AddPage 建页，须在渲染图层内容前同步维护。
        _renderPageLoc = $"/{docDir.Name}/Pages/{pageDir.Name}/Content.xml";

        // 设置页面区域（虚拟页面样式）
        var pageLayout = vPage.Style;
        pageObj.Area = new Core.BasicStructure.Doc.CT_PageArea
        {
            PhysicalBox = new STBox(0, 0, pageLayout.Width, pageLayout.Height),
            ApplicationBox = new STBox(0, 0, pageLayout.Width, pageLayout.Height)
        };

        // 渲染本页图层内容
        RenderVPageLayers(vPage, pageObj);

        // 渲染并注册模板页（Doc_N/Tpl_M/Content.xml），在本页以 <Template> 引用
        foreach (var tpl in vPage.Templates)
        {
            var tplId = new STID(++_maxUnitId);
            int tplIndex = _nextTplIndex++;

            var tplPageObj = new PageObject();
            tplPageObj.Area = new Core.BasicStructure.Doc.CT_PageArea
            {
                PhysicalBox = new STBox(0, 0, tpl.Width, tpl.Height),
                ApplicationBox = new STBox(0, 0, tpl.Width, tpl.Height)
            };
            // 模板页渲染期间 _renderPageLoc 指向模板 Content.xml（模板内 AreaHolderBlock 罕见，
            // 但 loc 须正确；下个 RenderVPage/AddPage 会重设回各自页面 loc）
            var savedRenderLoc = _renderPageLoc;
            _renderPageLoc = $"/{docDir.Name}/Tpl_{tplIndex}/Content.xml";
            RenderVPageLayers(tpl, tplPageObj);
            _renderPageLoc = savedRenderLoc;

            // 模板页内容写入 Doc_N/Tpl_M/Content.xml
            var tplDir = docDir.ObtainContainer($"Tpl_{tplIndex}");
            tplDir.PutFile("Content.xml", tplPageObj.Element);

            // 注册模板页到公共数据（CommonData），供各页面通过 Template 引用
            _commonData!.AddTemplatePage(new CTTemplatePage
            {
                ID = tplId,
                ZOrder = LayerType.Background,
                BaseLoc = new STLoc($"Tpl_{tplIndex}/Content.xml")
            });

            // 本页添加模板引用（默认绘制在正文之下）
            pageObj.AddTemplate(new CTTemplate
            {
                TemplateID = tplId.Value,
                ZOrder = LayerType.Background
            });
        }

        pageDir.SetContent(pageObj);
        RegisterPageInDocument(docDir, pageDir);
    }

    /// <summary>
    /// 将虚拟页面的图层内容（前景/正文/背景）渲染到指定 PageObject 的对应图层上。
    /// 主页面与模板页共用此逻辑，保证渲染行为一致。
    /// </summary>
    private void RenderVPageLayers(VirtualPage vPage, PageObject pageObj)
    {
        var pageLayout = vPage.Style;
        var content = pageObj.ObtainContent();

        // 按图层分组：前景、正文、背景
        var layerGroups = vPage.GetLayerContent();
        var layerTypes = new[] { LayerType.Foreground, LayerType.Body, LayerType.Background };
        for (int i = 0; i < layerGroups.Count; i++)
        {
            var layerGroup = layerGroups[i];
            if (layerGroup.Count == 0) continue;

            var layer = new CTLayer
            {
                LayerType = layerTypes[i],
                ID = new STID(++_maxUnitId)
            };
            content.AddLayer(layer);

            var layerCtx = new Element.Canvas.DrawContext(layer, new STBox(0, 0, pageLayout.Width, pageLayout.Height), () => ++_maxUnitId, Prm);
            foreach (var div in layerGroup)
            {
                RenderDiv(layerCtx, div);
            }
        }
    }

    /// <summary>
    /// 渲染段落文字内容
    /// 每个 Span 在同一行内合并为一个 TextObject，超出边界时自动换行。
    /// <para>
    /// flowMode=true 时启用流式行级跨页：每行开始前检查页内容区剩余空间，不足则
    /// <see cref="AddPage"/> 并在新页续画（文字不断行不截断）；返回最后文本行底部的
    /// 页面绝对 Y（供流式排版光标推进）。flowMode=false（固定/绝对布局）保持旧行为，返回 null。
    /// </para>
    /// </summary>
    /// <summary>段落行缓冲中的一个文字 run（RenderParagraph 行级排版用，对齐 Java TxtLineBlock 行内 Span）。</summary>
    private struct LineRun
    {
        public Span Span;
        public string Text;
        public double StartX;        // 该 run 在行内的起始 X（绝对）
        public double CharWidth;     // 有效字号（mm，已归一非 0）
        public double LetterSpacing; // 字间距（mm）
        public bool IsPlaceholder;   // 占位符：只占宽不渲染
        public double HoldWidth;     // 占位宽度（mm）
        public double TotalWidth;    // run 总宽（mm，封口时预计算：Σ逐字宽 + 相邻间距）
        public double[]? DeltaX;     // 逐字符推进量（n-1 个，末字符无推进；≥2 字符时恒非 null——
                                     // 对齐 Java Span#getDeltaX() 无条件写策略；不再对
                                     // 「全中文等距 run」省略，否则不度量字宽的阅读器会把整行字叠绘在起点）
    }

    private double? RenderParagraph(Element.Canvas.DrawContext ctx, Paragraph paragraph, bool flowMode = false)
    {
        double startX = (paragraph.X ?? 0) + paragraph.MarginLeft + paragraph.BorderLeft + paragraph.PaddingLeft;
        // OFD 文字基线修正：
        // - TextCode 的 Y 是基线（baseline）位置
        // - 汉字顶部在 基线 - fontSize 处，所以基线必须 > fontSize 才能完整显示
        // - 为安全起见，初始 Y 偏移 = Max(最大fontSize, 10mm)
        double maxFontSize = paragraph.Spans
            .Where(s => !s.IsLineBreak && s.FontSize > 0)
            .Select(s => s.FontSize)
            .DefaultIfEmpty(paragraph.DefaultFontSize > 0 ? paragraph.DefaultFontSize : 5.0)
            .Max();
        double yOffset = Math.Max(maxFontSize + 2.0, 10.0); // 至少偏移 10mm
        double startY = (paragraph.Y ?? 0) + paragraph.MarginTop + paragraph.BorderTop + paragraph.PaddingTop + yOffset;
        double maxWidth = paragraph.Width ?? 170;
        double lineSpace = paragraph.LineSpace > 0 ? paragraph.LineSpace : 2.0;

        double contentBottom = Height - MarginBottom;
        // 获取 Layer 以便直接添加 TextObject（分页时随新页刷新）
        CTLayer? layer = ctx.TargetContainer as NetOfd.Core.BasicStructure.PageObj.Layer.CTLayer;
        double? lastBottom = null;

        // 流式换页：新建页面，把「行顶 Y」重置到新页内容顶部（保留段内上边距/基线偏移）
        void BreakToNewPage(ref double yTop)
        {
            AddPage();
            var nc = CreateDrawContext(new STBox(0, 0, Width, Height));
            if (paragraph.Opacity is double oa && oa < 1.0) nc.GlobalAlpha = oa;
            layer = nc.TargetContainer as NetOfd.Core.BasicStructure.PageObj.Layer.CTLayer;
            yTop = MarginTop + paragraph.MarginTop + paragraph.BorderTop + paragraph.PaddingTop + yOffset;
        }

        // 首行预检：流式模式下当前页剩余空间连一行都放不下 → 整段换页续排
        if (flowMode && startY + maxFontSize * 1.2 + lineSpace > contentBottom)
        {
            BreakToNewPage(ref startY);
        }

        // ── 行缓冲状态（对齐 Java TxtLineBlock 行模型）────────────────────────────
        // 一行内所有 run 收齐后统一做 TextAlign 整行平移 + 统一基线再绘制——修复旧
        // 「逐 run 各自平移/各自基线」导致的多 span 同行 Center/Right 错位与跨字号
        // span 基线不一致。普通 run 宽 = 字符数×字宽 + 相邻间距；占位符宽 = HoldWidth。

        List<LineRun> lineRuns = new();
        // 当前正在累积字符的 run（同一 Span 的连续文本片段；折行/换 span 时封口入 lineRuns）
        System.Text.StringBuilder? curSb = null;
        Span? curSpan = null;
        double curStartX = 0, curCharWidth = 0, curLetterSpacing = 0;
        List<double>? curWidths = null;  // 当前 run 每字符实际宽（fontSize×GetCharWidthScale，增量 H）

        double currentX = startX;
        double currentY = startY;
        // 行内最高文字高（1.2 系数 descent 近似，Java maxSpanHeight 的 .NET 版）
        double currentLineHeight = 0;
        // 行内最大字号 —— Java TxtLineBlock.maxSpanHeight：整行统一基线 = 行内最高字身
        double lineMaxFont = 0;

        // 段落首行标记：仅在段内第一行应用 FirstLineIndent 缩进；flowMode 跨页后不再缩进
        bool paraFirstLine = true;

        // CT_Color 挂载防御：同一 CT_Color 实例重复挂到不同父节点会把 XML 节点从旧父挪走
        // （Span.Color 同时给文字 FillColor 与下划线 PathObject.StrokeColor / 折行多 run 共享
        // 同一 Span 颜色实例 / Span.Color 与 StrokeColor 指向同一实例）。
        // 已挂载（Element.Parent != null）的实例按深拷贝独立挂载，避免 FillColor 漂移丢失。
        NetOfd.Core.PageDescription.Color.CT_Color? MountColor(NetOfd.Core.PageDescription.Color.CT_Color? c)
            => c is null || c.Element.Parent is null ? c
                : new NetOfd.Core.PageDescription.Color.CT_Color(new XElement(c.Element));

        // 封口当前累积 run（折行 / 换 Span / 行收尾前调用）
        void CloseCurRun()
        {
            if (curSb is { Length: > 0 } && curSpan is not null)
            {
                var widths = curWidths!;
                double total = 0;
                for (int k = 0; k < widths.Count; k++)
                {
                    total += widths[k];
                    if (k < widths.Count - 1) total += curLetterSpacing;
                }
                // DeltaX 写路径（对齐 Java ParagraphRender + Span#getDeltaX()）：
                // ≥2 字即生成 n-1 个推进量；每值 = 字符宽 + 字间距
                //（后字相对前字 X 偏移，Java DeltaX[i]=getW 同式）。
                //
                // 【2026-10-24 修正】原实现在此加了「全中文等距 run 不写 DeltaX」的优化，
                // 理由是「保持 XML 稳定」。该假设与 Java ofdrw 相反（ofdrw 无条件写），
                // 且实测有害：ofdgo 等不做字体度量的阅读器会把整行字符叠绘在起点——
                // 病案首页表格每个单元格只能显示一个字（已在 ofdgo 官方引擎实渲中复现）。
                // 故此处改为无条件写（≥2 字），不再依赖阅读器自行推算字宽。
                double[]? dx = null;
                if (widths.Count >= 2)
                {
                    dx = new double[widths.Count - 1];
                    for (int k = 0; k < widths.Count - 1; k++)
                        dx[k] = widths[k] + curLetterSpacing;
                }
                lineRuns.Add(new LineRun
                {
                    Span = curSpan, Text = curSb.ToString(),
                    StartX = curStartX, CharWidth = curCharWidth, LetterSpacing = curLetterSpacing,
                    TotalWidth = total, DeltaX = dx
                });
            }
            curSb = null; curSpan = null; curWidths = null;
        }

        // 行收尾（Java 每行 TxtLineBlock 封行）：整行统一平移后逐 run 绘制，
        // 随后光标推进到下一行顶。fullLine = 折行产生的整行（Justify 拉伸用）。
        void EndRow(bool fullLine)
        {
            CloseCurRun();
            if (lineRuns.Count == 0) return;   // 空行不推进（Java 空 Segment 跳过）
            if (layer is null) return;         // 无图层目标（异常态）：不绘制也不推进（旧 FlushRun 同语义）
            double rowTop = currentY;
            double lineWidth = 0;
            foreach (var r in lineRuns) lineWidth += r.TotalWidth;
            double rowHeight = Math.Max(currentLineHeight, 1);
            // 行级 TextAlign 整行平移（Java ParagraphRender switch textAlign —— 整行一次偏移）
            double delta = 0;
            if (lineWidth < maxWidth)
            {
                switch (paragraph.TextAlign)
                {
                    case TextAlign.Center: delta = (maxWidth - lineWidth) / 2; break;
                    case TextAlign.Right: delta = maxWidth - lineWidth; break;
                }
            }
            // 统一基线 = 行内最高字身（Java TextCode y = maxSpanHeight）
            double baseline = lineMaxFont;
            foreach (var r in lineRuns)
            {
                if (r.IsPlaceholder) continue;   // 占位符只占宽不渲染（Java ParagraphRender instanceof 跳过）
                var sp = r.Span;
                if (sp is null) continue;        // struct 零值防御（正常路径不触发）
                double drawX = r.StartX + delta;
                double w = r.TotalWidth;
                if (w <= 0) continue;
                var fontFamily = sp.Font?.Name ?? paragraph.DefaultFont?.Name ?? "SimSun";
                double fs = r.CharWidth;   // 有效字号（构造 run 时已归一非 0）
                var fontId = Prm.GetFontId(fontFamily);
                // 默认文字色：GB/T 33190 §7.4 规定 Color/@Value 各通道以空格分隔（"0 0 0"）。
                // 旧值 "0,0,0"（逗号）会让 ofdrw / ofdgo 的 pdfcpu 管线报
                // “expected 3 color components”，整页渲染失败。
                var color = sp.Color ?? new NetOfd.Core.PageDescription.Color.CT_Color { Value = "0 0 0" };

                var textObj = new NetOfd.Core.PageDescription.CTText
                {
                    // Boundary 使用页面绝对坐标：行内所有 run 同 Y = 行顶（对齐 Java 行 TextObject）
                    Boundary = new STBox(drawX, rowTop, Math.Max(w, 1), Math.Max(rowHeight, 1)),
                    Font = STRefID.Parse(fontId),
                    Size = fs,
                    FillColor = MountColor(color)
                };
                if (sp.Bold) textObj.Bold = true;
                if (sp.Italic) textObj.Italic = true;
                if (sp.Weight.HasValue) textObj.Weight = sp.Weight.Value;
                // 文字填充/描边：对齐 Java ParagraphRender `if (!s.isFill()) txtObj.setFill(false)`；
                // Stroke/StrokeColor 为 NetOfd 扩展（OFD CT_Text 规范属性直 map）。
                if (!sp.Fill) textObj.Fill = false;
                if (sp.Stroke) textObj.Stroke = true;
                if (sp.StrokeColor is not null) textObj.StrokeColor = MountColor(sp.StrokeColor);
                // TextCode X/Y 相对于 Boundary（OFD 规范）；Y = 行统一基线
                var code = new NetOfd.Core.PageDescription.TextCode
                {
                    X = 0,
                    Y = baseline,
                    Text = r.Text
                };
                // 两端对齐：折行产生的整行在行宽内均匀分布字形；段落末行保持左对齐
                if (paragraph.TextAlign == TextAlign.Justify && fullLine)
                    code.Width = maxWidth;
                // DeltaX：逐字符推进量（空格分隔定点值，Converter DeltaTool 展开逐字定位；
                // 均布 Width 场景由阅读器忽略阅读方向推进量，二者不冲突）
                if (r.DeltaX is not null)
                    code.DeltaX = string.Join(" ", r.DeltaX.Select(d => STBase.Fmt(d)));
                textObj.AddTextCode(code);
                layer.AddTextObject(textObj);

                // 下划线 / 删除线：以描边路径实现（随行内各 run 分别绘制，折行后各段独立）
                // 下划线消费 Span.UnderlineOffset / UnderlineWidth（对齐 Java setUnderline 三参）；
                // 默认沿用 Java：offset=1.2、width=fontSize*0.05；删除线保留 -0.30fs/0.06 经验值。
                if (sp.Underline || sp.StrikeThrough)
                {
                    double decoY, decoWidth;
                    if (sp.Underline)
                    {
                        decoY = rowTop + baseline + sp.UnderlineOffset;
                        decoWidth = sp.UnderlineWidth > 0 ? sp.UnderlineWidth : fs * 0.05;
                    }
                    else
                    {
                        decoY = rowTop + baseline - fs * 0.30;
                        decoWidth = fs * 0.06;
                    }
                    var deco = new NetOfd.Core.PageDescription.CTPath
                    {
                        Boundary = new STBox(drawX, decoY - 0.3, Math.Max(w, 1), 0.6),
                        Stroke = true,
                        Fill = false,
                        LineWidth = Math.Max(0.12, decoWidth),
                        StrokeColor = MountColor(color)
                    };
                    deco.AbbreviatedData =
                        $"M {STBase.Fmt(drawX)} {STBase.Fmt(decoY)} " +
                        $"L {STBase.Fmt(drawX + w)} {STBase.Fmt(decoY)}";
                    layer.AddPathObject(deco);
                }
            }
            lineRuns.Clear();
            // 记录本行文本底（行顶 + 行高），供流式光标推进
            lastBottom = rowTop + rowHeight;
            currentX = startX;
            currentY += rowHeight + lineSpace;
            currentLineHeight = 0;
            lineMaxFont = 0;
        }

        foreach (var span in paragraph.Spans)
        {
            // ── 占位符（PlaceholderSpan）：仅占行宽不渲染，参与行宽/行对齐与折行。
            //    放最前：占位符 Content 为 null，须先于 IsLineBreak 分支消费。──
            if (span is PlaceholderSpan ph)
            {
                double holdW = ph.HoldWidth;
                if (holdW <= 0) continue;
                double phFs = ph.FontSize > 0 ? ph.FontSize : paragraph.DefaultFontSize;
                // 占位宽超过整行可用宽 → 丢弃（Java doPrepare：blockWidth > lineMaxAvailableWidth
                // && isIntegrity → continue 忽略该元素；PlaceholderSpan 构造即 integrity=true）
                if (holdW > maxWidth) continue;
                // 行剩余空间放不下整块占位 → 封行开新行（Java integrity 整块换行语义）
                if (currentX + holdW > startX + maxWidth && (lineRuns.Count > 0 || curSb is { Length: > 0 }))
                {
                    EndRow(fullLine: true);
                    if (flowMode && currentY + maxFontSize * 1.2 + lineSpace > contentBottom)
                    {
                        BreakToNewPage(ref currentY);
                        currentX = startX;
                    }
                }
                CloseCurRun();
                lineRuns.Add(new LineRun
                {
                    Span = span, Text = "", StartX = currentX,
                    CharWidth = phFs, LetterSpacing = 0, IsPlaceholder = true, HoldWidth = holdW,
                    TotalWidth = holdW
                });
                currentX += holdW;
                currentLineHeight = Math.Max(currentLineHeight, phFs * 1.2);
                lineMaxFont = Math.Max(lineMaxFont, phFs);
                continue;
            }

            // ── 换行符：结束当前行并推进光标 ──
            if (span.IsLineBreak)
            {
                bool hadRow = lineRuns.Count > 0 || curSb is { Length: > 0 };
                EndRow(fullLine: false);
                if (!hadRow)
                {
                    // 空行仅推进行间距（对齐 Java 空行 = lineSpace 高度）
                    currentY += lineSpace;
                    currentLineHeight = 0;
                    lineMaxFont = 0;
                }
                // 行级跨页：换行后若下一行放不下页内容区 → 换页
                if (flowMode && currentY + maxFontSize * 1.2 + lineSpace > contentBottom)
                {
                    BreakToNewPage(ref currentY);
                    currentX = startX;
                }
                continue;
            }

            var content = span.Content ?? "";
            if (string.IsNullOrEmpty(content)) continue;

            var fontFamily = span.Font?.Name ?? paragraph.DefaultFont?.Name ?? "SimSun";
            var fontSize = span.FontSize > 0 ? span.FontSize : paragraph.DefaultFontSize;
            var fontId = Prm.GetFontId(fontFamily);
            var color = span.Color ?? new NetOfd.Core.PageDescription.Color.CT_Color { Value = "0 0 0" };
            // 度量字体（增量 H）：逐字宽度 = 字号 × GetCharWidthScale(c)——
            // 中文字体 ASCII 半角约 0.5em、CJK 全宽 1.0em（对齐 Java Font.getCharWidthScale
            // 无映射默认；内置字体走 FontNames 精确宽度表 / 其余 Skia 实测）
            var spanFont = span.Font ?? paragraph.DefaultFont
                ?? new NetOfd.Font.OFDFont(NetOfd.Font.FontName.SimSun);

            // OFD 规范：TextCode 的 X/Y 是相对于 Boundary 的坐标
            // Boundary 使用页面绝对坐标，TextCode 使用相对于 Boundary 的偏移量
            // 字符数缩进用全角字身宽（Java PlaceholderSpan.setHoldChars = n×(fs+ls) 同语义）
            double charWidth = fontSize * 1.0;
            double letterSpacing = span.LetterSpacing;

            // 段首行缩进：绝对宽度（FirstLineIndentWidth）优先于字符数（FirstLineIndent），
            // 仅 TextAlign=Left 时生效；对齐 Java Paragraph.setFirstLineIndentWidth / setFirstLineIndent
            if (paraFirstLine && paragraph.TextAlign == TextAlign.Left)
            {
                if (paragraph.FirstLineIndentWidth > 0)
                    currentX += paragraph.FirstLineIndentWidth;
                else if (paragraph.FirstLineIndent > 0)
                    currentX += paragraph.FirstLineIndent * charWidth;
                if (paragraph.FirstLineIndent > 0 || paragraph.FirstLineIndentWidth > 0)
                    paraFirstLine = false;
            }

            int totalChars = content.Length;
            for (int i = 0; i < totalChars; i++)
            {
                var c = content[i];
                // 逐字宽（mm）：比例表/实测 × 字号；度量兜底 ≤0 时按全宽 1.0
                double charW = fontSize * spanFont.GetCharWidthScale(c);
                if (charW <= 0) charW = fontSize;
                // 折行：当前行已放内容（同 span 已塞字符 或 行缓冲已有 run）且再放一字超界
                // → 收尾本行（整行 → Justify 拉伸），开新行续排。
                if (currentX + charW > startX + maxWidth && (lineRuns.Count > 0 || curSb is { Length: > 0 }))
                {
                    EndRow(fullLine: true);
                    // 行级跨页：折行后若下一行放不下页内容区 → 换页
                    if (flowMode && currentY + maxFontSize * 1.2 + lineSpace > contentBottom)
                    {
                        BreakToNewPage(ref currentY);
                        currentX = startX;
                    }
                }
                // 累积到「当前 run」：跨 Span / 折行后 → 开新 run（StartX = 当前行 X）
                if (curSpan != span || curSb is null)
                {
                    CloseCurRun();
                    curSpan = span;
                    curSb = new System.Text.StringBuilder();
                    curStartX = currentX;
                    curCharWidth = fontSize;
                    curLetterSpacing = letterSpacing;
                    curWidths = new List<double>();
                }
                curSb.Append(c);
                curWidths!.Add(charW);
                // 字符 X 推进：最后一个字符不计间距（行末/段末无后续字符）
                double advance = (i < totalChars - 1) ? charW + letterSpacing : charW;
                currentX += advance;
                currentLineHeight = Math.Max(currentLineHeight, fontSize * 1.2);
                lineMaxFont = Math.Max(lineMaxFont, fontSize);
            }
        }
        // 段落收尾：绘制最后一行
        EndRow(fullLine: false);

        return flowMode ? lastBottom : null;
    }

    /// <summary>文档总页数</summary>
    public int PageCount => _ofdDir?.GetDocByIndex(0)?.GetPagesCount() ?? 0;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // 确保文档被关闭并写出
                try { Close(); } catch { /* 忽略关闭时的错误 */ }
                _ofdDir?.Dispose();
            }
            _ofdDir = null;
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// 待添加的表格列表
    /// </summary>
    /// <summary>表格流内推进光标：RenderTable 每渲染完一张表格后记录其末页底部 + 表间距。</summary>
    private double _tableCursorY;

    /// <summary>
    /// 将表格渲染为 OFD 页面图元，支持自动分页：
    /// 当表格当前页剩余空间不足时调用 <see cref="AddPage"/> 新建页面并续画剩余行，
    /// 可选在新页顶部重复表头（<see cref="Table.RepeatHeader"/>）。
    /// 单页场景下的图元计数与旧实现完全一致（外框 + 内部网格线 + 单元格）。
    /// </summary>
    private void RenderTable(Table table)
    {
        if (table.ColumnCount <= 0 || table.RowCount <= 0) return;

        double x = table.X;
        double y = table.Y >= 0 ? table.Y : _tableCursorY;
        double totalW = table.Width ?? Math.Max(Width - x, 1);

        // 列宽（等宽或自定义，归一化使总和 = totalW）
        double[] colW;
        if (table.ColumnWidths != null && table.ColumnWidths.Length == table.ColumnCount)
            colW = (double[])table.ColumnWidths.Clone();
        else
        {
            colW = new double[table.ColumnCount];
            double each = totalW / table.ColumnCount;
            for (int i = 0; i < table.ColumnCount; i++) colW[i] = each;
        }
        double sumW = colW.Sum();
        if (sumW > 0)
        {
            double k = totalW / sumW;
            for (int i = 0; i < colW.Length; i++) colW[i] *= k;
        }

        int rows = table.RowCount;
        int cols = table.ColumnCount;
        double rowH = table.RowHeight;

        // 列左边距累积
        double[] colLeft = new double[cols + 1];
        colLeft[0] = x;
        for (int c = 0; c < cols; c++) colLeft[c + 1] = colLeft[c] + colW[c];

        // owner[r,c] = 该格所属跨区左上角 (tr,tc)，用于正确绘制跨行/跨列边框。
        // 覆盖所有网格（含跨区占位），未被任何单元格声明的格为 null（仍绘制常规网格线）。
        var owner = new (int tr, int tc)?[rows, cols];
        foreach (var cell in table.Cells)
        {
            int rs = Math.Max(1, cell.RowSpan);
            int cs = Math.Max(1, cell.ColumnSpan);
            for (int dr = 0; dr < rs; dr++)
                for (int dcx = 0; dcx < cs; dcx++)
                {
                    int rr = cell.RowIndex + dr, cc = cell.ColumnIndex + dcx;
                    if (rr >= 0 && rr < rows && cc >= 0 && cc < cols)
                        owner[rr, cc] = (cell.RowIndex, cell.ColumnIndex);
                }
        }

        double marginTop = MarginTop;
        double pageBottom = Height - MarginBottom;

        // 标题仅在第一页绘制一次，并把表格整体下移一行高度
        double firstTop = y;
        var dc = CreateDrawContext(new STBox(0, 0, Width, Height));
        if (!string.IsNullOrEmpty(table.Title))
        {
            dc.Font = $"{table.FontSize}mm {table.FontFamily}";
            dc.FillStyle = table.TextColor;
            dc.FillText(table.Title, x, y);
            firstTop = y + rowH;
        }

        bool repeatHeader = table.RepeatHeader && table.HeaderRows > 0;
        int headerRows = Math.Min(table.HeaderRows, rows);

        double lastChunkBottom = firstTop;
        int dataStart = 0;
        double chunkTop = firstTop;

        while (dataStart < rows)
        {
            // 重复表头：非首块在新页顶部先绘制表头行，再从其下方开始数据
            double bodyTop = chunkTop;
            if (repeatHeader && dataStart > 0)
            {
                RenderTableChunk(table, colLeft, colW, owner, x, totalW,
                                 chunkTop, 0, headerRows - 1, dc);
                bodyTop = chunkTop + headerRows * rowH;
            }

            // 计算本块可容纳的数据行数（单行超页时强制至少放 1 行，避免死循环）
            int fit = 0;
            while (dataStart + fit < rows)
            {
                double rowBottom = bodyTop + (fit + 1) * rowH;
                if (rowBottom <= pageBottom + 0.01) fit++;
                else break;
            }
            if (fit <= 0) fit = 1;
            int chunkEnd = dataStart + fit - 1;

            RenderTableChunk(table, colLeft, colW, owner, x, totalW,
                             bodyTop, dataStart, chunkEnd, dc);

            double chunkBottom = bodyTop + (chunkEnd - dataStart + 1) * rowH;
            // 覆盖式记录本块底：多页表格每块按「新页顶部」重排，块底可能回落（首块填满页底时
            // 底最大），若取全程 max 会把光标错误推到首块页底 +5，把表格后的段落顶去新页
            lastChunkBottom = chunkBottom;

            dataStart = chunkEnd + 1;
            if (dataStart < rows)
            {
                AddPage();
                dc = CreateDrawContext(new STBox(0, 0, Width, Height));
                chunkTop = marginTop;
            }
        }

        // 推进自动堆叠光标
        _tableCursorY = Math.Max(_tableCursorY, lastChunkBottom + 5);
    }

    /// <summary>
    /// 渲染表格在某一页内、行区间 [startRow, endRow] 对应的图元块（边框 + 内部网格 + 单元格）。
    /// 用于自动分页：每页只画落在当前页内的行，跨页断点处不绘制跨越的网格线/单元格，
    /// 因此单页场景下与「整表一笔画完」产出完全相同的图元集合。
    /// </summary>
    private void RenderTableChunk(
        Table table,
        double[] colLeft,
        double[] colW,
        (int tr, int tc)?[,] owner,
        double x,
        double totalW,
        double chunkTop,
        int startRow,
        int endRow,
        Element.Canvas.DrawContext dc)
    {
        int cols = table.ColumnCount;
        double rowH = table.RowHeight;

        dc.Font = $"{table.FontSize}mm {table.FontFamily}";
        dc.StrokeStyle = table.BorderColor;
        dc.LineWidth = table.BorderWidth;

        int chunkRows = endRow - startRow + 1;

        // 外边框（仅本块）
        dc.StrokeRect(x, chunkTop, totalW, chunkRows * rowH);

        // 内部竖线（列边界 j=1..cols-1）：相邻两格在块内且 owner 不同
        for (int j = 1; j < cols; j++)
        {
            double lx = colLeft[j];
            for (int r = startRow; r <= endRow; r++)
            {
                if (owner[r, j - 1] != owner[r, j])
                    dc.MoveTo(lx, chunkTop + (r - startRow) * rowH)
                      .LineTo(lx, chunkTop + (r - startRow + 1) * rowH)
                      .Stroke();
            }
        }
        // 内部横线（行边界 i=startRow+1..endRow）
        for (int i = startRow + 1; i <= endRow; i++)
        {
            double ly = chunkTop + (i - startRow) * rowH;
            for (int c = 0; c < cols; c++)
            {
                if (owner[i - 1, c] != owner[i, c])
                    dc.MoveTo(colLeft[c], ly).LineTo(colLeft[c + 1], ly).Stroke();
            }
        }

        // 背景 + 文字（仅跨区左上角格，且 RowIndex 落在本块内）
        foreach (var cell in table.Cells)
        {
            if (cell.RowIndex < startRow || cell.RowIndex > endRow) continue;
            int rs = Math.Max(1, cell.RowSpan);
            int cs = Math.Max(1, cell.ColumnSpan);
            double rx = colLeft[cell.ColumnIndex];
            double ry = chunkTop + (cell.RowIndex - startRow) * rowH;
            double rw = 0;
            for (int k = 0; k < cs; k++) rw += colW[cell.ColumnIndex + k];
            double rh = rs * rowH;

            if (!string.IsNullOrEmpty(cell.BackgroundColor))
            {
                dc.FillStyle = cell.BackgroundColor!;
                dc.FillRect(rx, ry, rw, rh);
            }
            if (!string.IsNullOrEmpty(cell.Content))
            {
                dc.FillStyle = table.TextColor;
                dc.FillText(cell.Content!, rx + table.CellPadding, ry + table.CellPadding);
            }
        }
    }

    /// <summary>
    /// 添加表格（自研扩展，对齐 Java 中以 canvas/Cell 定位后产出 Path/Text 的做法）。
    /// 表格被包装为流式元素加入统一书写队列：与 <see cref="Add"/> 的段落/Div 按调用先后
    /// 就地排版——段落 → 表格 → 段落 的混排顺序得到保留，表格 X/Y 默认融入页边距流光标；
    /// 显式指定 <see cref="Table.Y"/>（&gt;=0）时按精确定位渲染（不推进流光标）。
    /// </summary>
    /// <param name="table">要添加的表格</param>
    public void AddTable(Table table)
    {
        if (table == null) throw new ArgumentNullException(nameof(table));
        _streamQueue.Add(new TableHostDiv(table));
    }
}

/// <summary>水印选项</summary>
public class WatermarkOption
{
    public string Text { get; set; } = "WATERMARK";
    public double Opacity { get; set; } = 0.3;
    public string Font { get; set; } = "SimSun";
    public double FontSize { get; set; } = 40;
    public string Color { get; set; } = "#cccccc";
    public double Angle { get; set; } = -45;
}
