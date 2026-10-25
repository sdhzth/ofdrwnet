namespace NetOfd.Sign;

using System.Xml.Linq;
using NetOfd.Core;

/// <summary>
/// 本标准支持设置文档权限声明（Permission）节点，以达到文档防扩散等应用目的。
/// <para>
/// 7.5 小节 CT_Permission
/// </para>
/// </summary>
public class CT_Permission : OFDElement
{
    /// <summary>
    /// 无参构造函数，创建一个空的权限节点。
    /// </summary>
    public CT_Permission()
        : base("Permissions")
    {
    }

    /// <summary>
    /// 是否允许编辑
    /// <para>
    /// 默认值为 true
    /// </para>
    /// </summary>
    public bool Edit
    {
        get
        {
            var el = Element.Element(OfdNs + "Edit");
            return el != null && Boolean.Parse(el.Value);
        }
        set => Element.Element(OfdNs + "Edit")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 是否允许添加或修改标注
    /// <para>
    /// 默认值为 true
    /// </para>
    /// </summary>
    public bool Annot
    {
        get
        {
            var el = Element.Element(OfdNs + "Annot");
            return el != null && Boolean.Parse(el.Value);
        }
        set => Element.Element(OfdNs + "Annot")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 是否允许导出
    /// <para>
    /// 默认值为 true
    /// </para>
    /// </summary>
    public bool Export
    {
        get
        {
            var el = Element.Element(OfdNs + "Export");
            return el != null && Boolean.Parse(el.Value);
        }
        set => Element.Element(OfdNs + "Export")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 是否允许进行数字签名
    /// <para>
    /// 默认值为 true
    /// </para>
    /// </summary>
    public bool Signature
    {
        get
        {
            var el = Element.Element(OfdNs + "Signature");
            return el != null && Boolean.Parse(el.Value);
        }
        set => Element.Element(OfdNs + "Signature")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 是否允许添加水印
    /// <para>
    /// 默认值为 true
    /// </para>
    /// </summary>
    public bool Watermark
    {
        get
        {
            var el = Element.Element(OfdNs + "Watermark");
            return el != null && Boolean.Parse(el.Value);
        }
        set => Element.Element(OfdNs + "Watermark")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 是否允许截屏
    /// <para>
    /// 默认值为 true
    /// </para>
    /// </summary>
    public bool PrintScreen
    {
        get
        {
            var el = Element.Element(OfdNs + "PrintScreen");
            return el != null && Boolean.Parse(el.Value);
        }
        set => Element.Element(OfdNs + "PrintScreen")?.SetValue(value.ToString().ToLower());
    }

    /// <summary>
    /// 打印权限
    /// <para>
    /// 具体的权限和份数设置由其属性 Printable 及 Copics 控制。
    /// 若不设置 Print 节点，则默认可以打印，并且打印份数不受限制
    /// </para>
    /// </summary>
    public Print Print
    {
        get
        {
            var el = Element.Element(OfdNs + "Print");
            if (el != null)
            {
                var printable = el.Attribute("Printable")?.Value;
                var copies = el.Attribute("Copies")?.Value;
                return new Print
                {
                    Printable = printable != null && Boolean.Parse(printable),
                    Copies = copies != null ? int.Parse(copies) : 1
                };
            }
            return new Print() { Printable = true, Copies = 1 };
        }
        set
        {
            var el = Element.Element(OfdNs + "Print");
            if (el == null)
            {
                el = new XElement(OfdNs + "Print");
                Element.Add(el);
            }
            el.SetAttributeValue("Printable", value.Printable.ToString().ToLower());
            if (value.Copies > 0) el.SetAttributeValue("Copies", value.Copies.ToString());
        }
    }
}

/// <summary>
/// 打印权限属性
/// </summary>
public class Print
{
    /// <summary>
    /// 是否允许打印
    /// <para>
    /// 默认可以打印
    /// </para>
    /// </summary>
    public bool Printable { get; set; } = true;

    /// <summary>
    /// 打印份数
    /// </summary>
    public int Copies { get; set; } = 1;
}