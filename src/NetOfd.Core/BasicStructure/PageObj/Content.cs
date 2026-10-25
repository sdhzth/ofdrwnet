using System.Xml.Linq;

namespace NetOfd.Core.BasicStructure.PageObj;

/// <summary>
/// 页面内容，包含多个图层（CT_Layer）。
/// 对应 Java: org.ofdrw.core.basicStructure.pageObj.Content
/// </summary>
public class Content : OFDElement
{
    public Content() : base("Content") { }
    public Content(XElement element) : base(element) { }

    /// <summary>添加图层</summary>
    public Content AddLayer(Layer.CTLayer layer)
    {
        AddOFDEntity(layer);
        return this;
    }

    /// <summary>获取所有图层</summary>
    public List<Layer.CTLayer> GetLayers()
    {
        return GetChildElements("Layer")
            .Select(e => new Layer.CTLayer(e))
            .ToList();
    }

    /// <summary>
    /// 按绘制顺序（Z 序）获取所有图层：
    /// Background_Template → Background → Body_Template → Body → Foreground_Template → Foreground，
    /// 同类型内保持文档顺序（LINQ OrderBy 为稳定排序）。
    /// 对齐 Java <c>PageInfo.getAllLayer()</c> 的图层排序语义。
    /// </summary>
    public List<Layer.CTLayer> GetLayersInZOrder()
        => GetLayers().OrderBy(l => (int)l.LayerType).ToList();

    /// <summary>获取主体图层（Body 类型）</summary>
    public Layer.CTLayer? GetBodyLayer()
    {
        return GetLayers().FirstOrDefault(l => l.LayerType == Layer.LayerType.Body);
    }

    /// <summary>获取或创建主体图层</summary>
    public Layer.CTLayer ObtainBodyLayer()
    {
        var layer = GetBodyLayer();
        if (layer is not null) return layer;
        layer = new Layer.CTLayer { LayerType = Layer.LayerType.Body };
        AddOFDEntity(layer);
        return layer;
    }
}
