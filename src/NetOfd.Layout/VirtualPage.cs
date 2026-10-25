using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;
using NetOfd.Layout.Element;

namespace NetOfd.Layout;

/// <summary>
/// 虚拟页面。
/// <para>
/// 虚拟页面介于盒式模型和板式模型之间，内部包含多个 Div 对象，
/// 这些对象均为绝对定位。由于是绝对定位，因此不存在分页的情况。
/// </para>
/// 对应 Java: org.ofdrw.layout.VirtualPage
/// </summary>
public class VirtualPage
{
    /// <summary>虚拟页面的样式</summary>
    public PageLayout Style { get; set; }

    /// <summary>页面区域（STBox 形式，等价于 Style 的物理页面区域）</summary>
    public STBox Area
    {
        get => new(0, 0, Style.Width, Style.Height);
        set
        {
            if (value is not null)
            {
                Style.Width = value.Width;
                Style.Height = value.Height;
            }
        }
    }

    /// <summary>页面宽度（mm）</summary>
    public double Width => Style.Width;

    /// <summary>页面高度（mm）</summary>
    public double Height => Style.Height;

    /// <summary>虚拟页面的内容（绝对定位的 Div）</summary>
    private readonly List<Div> _content = new();

    /// <summary>插入的虚拟页面页码（插入位置），仅在不为空时表示插入到指定页码。</summary>
    private int? _pageNum;

    /// <summary>页面模板（可作为可复用母版；渲染时生成独立的 Doc_N/Tpl_M/Content.xml，并以 &lt;Template&gt; 引用挂到本页）</summary>
    private readonly List<VirtualPage> _templates = new();

    public VirtualPage() : this(PageLayout.A4()) { }

    public VirtualPage(PageLayout style)
    {
        Style = style ?? throw new ArgumentNullException(nameof(style));
    }

    public VirtualPage(double width, double height) : this(new PageLayout(width, height)) { }

    public IReadOnlyList<Div> Content => _content;

    /// <summary>
    /// 不对元素进行分析直接加入到虚拟页面容器内。
    /// 请在使用该接口前对待加入的元素完成分析，否则可能抛出异常。
    /// </summary>
    public VirtualPage AddUnsafe(Div d)
    {
        _content.Add(d);
        return this;
    }

    /// <summary>
    /// 向虚拟页面中加入对象（采用绝对定位的元素）。
    /// </summary>
    public VirtualPage Add(Div d)
    {
        if (d is null) return this;
        if (d.Float != AFloat.Left)
        {
            Console.Error.WriteLine("虚拟页面下不支持浮动属性，仅支持绝对定位");
        }
        if (d.Position != Position.Absolute)
        {
            throw new ArgumentException("加入虚拟页面的对象应该采用绝对定位（Position: Absolute）");
        }
        if (d.X is null || d.Y is null)
        {
            throw new ArgumentException("处于绝对定位的模式下的元素应该设置 X 和 Y 坐标");
        }
        if (d.Width is null)
        {
            throw new ArgumentException("绝对定位元素至少需要指定元素宽度（Width）");
        }
        d.DoPrepare(d.Width.Value + d.MarginLeft + d.MarginRight);
        _content.Add(d);
        return this;
    }

    /// <summary>
    /// 获取指定图层类型的 Div。
    /// </summary>
    public List<Div> GetContent(LayerType layer)
    {
        return _content.Where(d => d.Layer == layer).ToList();
    }

    /// <summary>
    /// 返回按图层分组的内容：前景层、正文层、背景层。
    /// </summary>
    public List<List<Div>> GetLayerContent()
    {
        var result = new List<List<Div>>(3);
        if (_content.Count == 0) return result;
        var foreground = new List<Div>();
        var body = new List<Div>();
        var background = new List<Div>();
        result.Add(foreground);
        result.Add(body);
        result.Add(background);
        foreach (var div in _content)
        {
            switch (div.Layer)
            {
                case LayerType.Foreground: foreground.Add(div); break;
                case LayerType.Body: body.Add(div); break;
                case LayerType.Background: background.Add(div); break;
            }
        }
        return result;
    }

    internal VirtualPage SetContent(List<Div> content)
    {
        _content.Clear();
        _content.AddRange(content);
        return this;
    }

    /// <summary>页码（从 1 起），null 表示不指定插入位置。</summary>
    public int? PageNum => _pageNum;

    public VirtualPage SetPageNum(int pageNum)
    {
        if (pageNum <= 0) throw new ArgumentException("虚拟页面页码(pageNum)错误");
        _pageNum = pageNum;
        return this;
    }

    /// <summary>
    /// 添加页面模板（母版）。被添加的模板在渲染时生成独立的模板页
    /// （Doc_N/Tpl_M/Content.xml），并作为 &lt;Template&gt; 引用挂到本虚拟页面上，
    /// 叠加顺序默认 Background（即绘制在正文之下）。
    /// 对应 Java: org.ofdrw.layout.VirtualPage.addTemplate
    /// </summary>
    public VirtualPage AddTemplate(VirtualPage template)
    {
        if (template is null) throw new ArgumentNullException(nameof(template));
        _templates.Add(template);
        return this;
    }

    /// <summary>本虚拟页面所引用的模板页列表</summary>
    public IReadOnlyList<VirtualPage> Templates => _templates;
}