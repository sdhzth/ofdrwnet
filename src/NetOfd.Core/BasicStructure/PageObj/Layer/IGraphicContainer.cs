using NetOfd.Core.PageDescription;

namespace NetOfd.Core.BasicStructure.PageObj.Layer;

/// <summary>
/// 可容纳图元对象（文字/图像/路径）的容器抽象。
/// <para>
/// 由页面图层 <see cref="CTLayer"/> 与注释外观
/// <see cref="NetOfd.Core.Annotation.PageAnnot.Appearance"/> 共同实现，
/// 使绘制上下文（DrawContext）既能向页面图层绘制，也能向注释外观绘制。
/// 对应 Java: org.ofdrw.core.basicStructure.pageObj.layer.CT_Layer 的内容模型。
/// </para>
/// </summary>
public interface IGraphicContainer
{
    /// <summary>添加文字对象</summary>
    void AddTextObject(CTText text);

    /// <summary>添加图像对象</summary>
    void AddImageObject(CTImage image);

    /// <summary>添加路径对象</summary>
    void AddPathObject(CTPath path);
}
