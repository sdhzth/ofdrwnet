using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.CompositeObj;
using NetOfd.Core.PageDescription;

namespace NetOfd.Core.BasicStructure.PageObj.Layer;

/// <summary>
/// 图层类型枚举（对应 OFD 标准的图层顺序）
/// </summary>
public enum LayerType
{
    Background_Template,
    Background,
    Body_Template,
    Body,
    Foreground_Template,
    Foreground
}

/// <summary>
/// CT_Layer - 页面图层，包含各类绘制对象。
/// 对应 Java: org.ofdrw.core.basicStructure.pageObj.layer.CT_Layer
/// </summary>
public class CTLayer : OFDElement, IGraphicContainer
{
    public CTLayer() : base("Layer") { }
    public CTLayer(XElement element) : base(element) { }

    /// <summary>图层 ID</summary>
    public STID? ID
    {
        get
        {
            long v = GetLongAttr("ID", -1);
            return v < 0 ? null : new STID(v);
        }
        set => SetAttr("ID", value?.Value.ToString());
    }

    /// <summary>图层类型</summary>
    public LayerType LayerType
    {
        get
        {
            string? v = GetAttr("Type");
            if (v is null) return LayerType.Body;
            return v switch
            {
                "Background" => LayerType.Background,
                "Body" => LayerType.Body,
                "Foreground" => LayerType.Foreground,
                "Background_Template" => LayerType.Background_Template,
                "Body_Template" => LayerType.Body_Template,
                "Foreground_Template" => LayerType.Foreground_Template,
                _ => LayerType.Body
            };
        }
        set
        {
            string s = value switch
            {
                LayerType.Background => "Background",
                LayerType.Body => "Body",
                LayerType.Foreground => "Foreground",
                LayerType.Background_Template => "Background_Template",
                LayerType.Body_Template => "Body_Template",
                LayerType.Foreground_Template => "Foreground_Template",
                _ => "Body"
            };
            SetAttr("Type", s);
        }
    }

    /// <summary>绘制参数引用</summary>
    public STRefID? DrawParam
    {
        get
        {
            var v = GetAttr("DrawParam");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("DrawParam", value?.Value.ToString());
    }

    /// <summary>添加文字对象</summary>
    public CTLayer AddTextObject(CTText text) { AddOFDEntity(text); return this; }

    /// <summary>添加图像对象</summary>
    public CTLayer AddImageObject(CTImage image) { AddOFDEntity(image); return this; }

    /// <summary>添加路径对象</summary>
    public CTLayer AddPathObject(CTPath path) { AddOFDEntity(path); return this; }

    /// <summary>添加页块（图元分组容器）</summary>
    public CTLayer AddPageBlock(XElement pageBlock) { _el.Add(pageBlock); return this; }

    /// <summary>
    /// 获取图层内的所有绘制对象，**按文档顺序（即 Z 序）递归展开 <c>PageBlock</c> 与嵌套 <c>Layer</c>**。
    /// <para>
    /// OFD 标准中 <c>CT_Layer</c> 的内容模型为
    /// <c>(TextObject | PathObject | ImageObject | CompositeObject | PageBlock)*</c>，
    /// 真实文件里图元通常包在一层或多层 <c>&lt;PageBlock&gt;</c> 内。
    /// <c>CT_PageBlock</c> 除 <c>ID</c> 外没有 CTM / Boundary 等几何属性，
    /// 因此展开为平铺序列不会改变绘制结果。
    /// 对齐 Java <c>AWTMaker.writeContent</c> 对 <c>CT_PageBlock</c> 的递归处理。
    /// </para>
    /// </summary>
    public List<CTGraphicUnit> GetGraphicObjects()
    {
        var result = new List<CTGraphicUnit>();
        Collect(_el, result, 0);
        return result;
    }

    /// <summary>
    /// 仅获取图层的**直接**子级绘制对象（不展开 <c>PageBlock</c>）。
    /// 需要保留原始层级结构时使用。
    /// </summary>
    public List<CTGraphicUnit> GetDirectGraphicObjects()
    {
        var result = new List<CTGraphicUnit>();
        foreach (var e in _el.Elements())
        {
            var obj = Wrap(e);
            if (obj is not null) result.Add(obj);
        }
        return result;
    }

    /// <summary>获取图层内的所有页块元素（直接子级）。</summary>
    public List<XElement> GetPageBlocks()
        => _el.Elements().Where(e => e.Name.LocalName == "PageBlock").ToList();

    // 递归收集：遇到 PageBlock / 嵌套 Layer 就下钻，其余按图元包装
    private static void Collect(XElement parent, List<CTGraphicUnit> sink, int depth)
    {
        // 防御：异常文件可能构造出自引用或超深嵌套，限制递归层数
        if (depth > 32) return;

        foreach (var e in parent.Elements())
        {
            switch (e.Name.LocalName)
            {
                case "PageBlock":
                case "Layer":
                    Collect(e, sink, depth + 1);
                    break;
                default:
                    var obj = Wrap(e);
                    if (obj is not null) sink.Add(obj);
                    break;
            }
        }
    }

    private static CTGraphicUnit? Wrap(XElement e) => e.Name.LocalName switch
    {
        "TextObject" => new CTText(e),
        "ImageObject" => new CTImage(e),
        "PathObject" => new CTPath(e),
        // 复合对象（内容在 CompositeGraphicUnit 资源中）：由渲染器经 ResourceID 解引用后递归绘制
        "CompositeObject" => new CT_Composite(e),
        _ => null
    };

    // 显式接口实现（保持公开方法的流式返回值）
    void IGraphicContainer.AddTextObject(CTText text) { AddTextObject(text); }
    void IGraphicContainer.AddImageObject(CTImage image) { AddImageObject(image); }
    void IGraphicContainer.AddPathObject(CTPath path) { AddPathObject(path); }
}
