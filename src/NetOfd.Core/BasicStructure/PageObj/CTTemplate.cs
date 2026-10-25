using System.Xml.Linq;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.PageObj;

/// <summary>
/// CT_Template - 页面模板引用，记录页面所使用的模板页及其叠加顺序。
/// 通过 TemplateID 引用具体模板，通过 ZOrder 控制模板在页面中的绘制顺序。
/// 对应 Java: org.ofdrw.core.basicStructure.pageObj.Template
/// </summary>
public class CTTemplate : OFDElement
{
    public CTTemplate() : base("Template") { }
    public CTTemplate(XElement element) : base(element) { }

    /// <summary>模板页 ID 引用</summary>
    public STRefID? TemplateID
    {
        get
        {
            var v = GetAttr("TemplateID");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("TemplateID", value?.Value.ToString());
    }

    /// <summary>模板在页面中的绘制顺序（默认 Background）</summary>
    public LayerType ZOrder
    {
        get
        {
            string? v = GetAttr("ZOrder");
            if (v is null) return LayerType.Background;
            return v switch
            {
                "Background" => LayerType.Background,
                "Body" => LayerType.Body,
                "Foreground" => LayerType.Foreground,
                "Background_Template" => LayerType.Background_Template,
                "Body_Template" => LayerType.Body_Template,
                "Foreground_Template" => LayerType.Foreground_Template,
                _ => LayerType.Background
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
                _ => "Background"
            };
            SetAttr("ZOrder", s);
        }
    }
}