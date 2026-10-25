namespace NetOfd.Layout.Element;

/// <summary>
/// 区域占位区块（对应 Java: org.ofdrw.layout.element.AreaHolderBlock）。
/// <para>该元素属性与 Div 一致，即可绘制边框等内容。用于构造页面中一个用于容纳将来可能出现的
/// 页面元素的结构，该结构不做任何事情仅仅是占位。被添加到页面中的区域占位区块将会生成一个
/// 特别定位坐标到 AreaHolderBlocks.xml 文件。</para>
/// <para>注意：AreaHolderBlock 受 Border 与 Padding 影响，可绘制区域仅为去除 Border 与
/// Padding 后的内部区域。</para>
/// <para>写侧消费由 <see cref="NetOfd.Layout.OFDDoc"/> 在流式/固定/绝对布局下统一拦截
/// （2026-09-04 增量 I，对齐 engine/render/AreaHolderBlockRender.render）：在页面 Body
/// 图层插入空 <c>PageBlock</c>（仅 ID），并向 AreaHolderBlocks.xml 登记
/// { AreaName, Boundary, PageFile, PageBlockID, ID }。编辑期消费（AreaHolderContext
/// get(areaName) → 在指定 PageBlock 内填充内容）仍为后续大工程，仅写侧已闭合。</para>
/// </summary>
public class AreaHolderBlock : Div
{
    /// <summary>占位区域名称（文档范围内唯一）。对应 Java 公开字段 areaName。</summary>
    private string _areaName;

    /// <summary>
    /// 创建 AreaHolderBlock 对象（宽度和高度必须在创建时指定）。
    /// </summary>
    /// <param name="areaName">区域名称，用于唯一定位区域占位区块，请确保该名称在文档范围内唯一。</param>
    /// <param name="width">宽度（单位：毫米 mm）</param>
    /// <param name="height">高度（单位：毫米 mm）</param>
    public AreaHolderBlock(string areaName, double width, double height)
    {
        _areaName = areaName;
        Width = width;
        Height = height;
    }

    /// <summary>
    /// 在指定位置创建 AreaHolderBlock 对象。
    /// </summary>
    /// <param name="areaName">区域名称，用于唯一定位区域占位区块，请确保该名称在文档范围内唯一。</param>
    /// <param name="x">画布左上角的 x 坐标（毫米 mm）</param>
    /// <param name="y">画布左上角的 y 坐标（毫米 mm）</param>
    /// <param name="w">画布的宽度（毫米 mm）</param>
    /// <param name="h">画布的高度（毫米 mm）</param>
    public AreaHolderBlock(string areaName, double x, double y, double w, double h)
    {
        _areaName = areaName;
        X = x;
        Y = y;
        Width = w;
        Height = h;
    }

    /// <summary>获取区域名称。</summary>
    public string GetAreaName() => _areaName;

    /// <summary>设置区域名称（链式，返回自身）。</summary>
    public AreaHolderBlock SetAreaName(string areaName)
    {
        _areaName = areaName;
        return this;
    }

    /// <summary>
    /// 获取元素类型名称（用于标识元素类型、选择合适的处理器，自定义元素请重写该方法）。
    /// Java elementType() = "AreaHolderBlock"。
    /// </summary>
    public override string ElementType => "AreaHolderBlock";

    // Java doPrepare(Double widthLimit) 是流式布局引擎的尺寸准备协议（返回
    // Rectangle(w+widthPlus, h+heightPlus)，注释称「Canvas 不接受宽度重设」）。
    // NetOfd 无流式引擎（RenderDiv 直接消费 Width/Height），故不移植该方法。
}
