using System.Xml.Linq;
using NetOfd.Core.BasicStructure.PageObj.Layer;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.BasicStructure.PageObj;

/// <summary>
/// CT_TemplatePage - 模板页，定义于文档公共数据（CommonData）中，
/// 由各页面通过 Template 引用。模板页包含 ID、名称、默认绘制顺序和内容文件路径。
/// 对应 Java: org.ofdrw.core.basicStructure.pageObj.CT_TemplatePage
/// </summary>
public class CTTemplatePage : OFDElement
{
    public CTTemplatePage() : base("TemplatePage") { }
    public CTTemplatePage(XElement element) : base(element) { }

    /// <summary>模板页标识</summary>
    public STID? ID
    {
        get
        {
            long v = GetLongAttr("ID", -1);
            return v < 0 ? null : new STID(v);
        }
        set => SetLongAttr("ID", value?.Value ?? -1);
    }

    /// <summary>模板页名称（可选）</summary>
    public string? Name
    {
        get => GetAttr("Name");
        set
        {
            if (string.IsNullOrWhiteSpace(value)) SetAttr("Name", null);
            else SetAttr("Name", value);
        }
    }

    /// <summary>模板页默认绘制顺序（默认 Background）</summary>
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

    /// <summary>模板页内容文件路径（相对 Doc 目录）</summary>
    public STLoc? BaseLoc
    {
        get
        {
            var v = GetAttr("BaseLoc");
            return v is null ? null : new STLoc(v);
        }
        set => SetAttr("BaseLoc", value?.Value);
    }
}