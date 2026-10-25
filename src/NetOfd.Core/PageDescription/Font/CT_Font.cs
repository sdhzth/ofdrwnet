using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Font;

/// <summary>
/// CT_Font - 字体描述对象（嵌入资源中的字体定义）。
/// 对应 Java: org.ofdrw.core.pageDescription.font.CT_Font
/// </summary>
public class CT_Font : OFDElement
{
    public CT_Font() : base("Font") { }
    public CT_Font(XElement element) : base(element) { }

    /// <summary>字体 ID</summary>
    public STID ID
    {
        get => new STID(GetLongAttr("ID"));
        set => SetLongAttr("ID", value.Value);
    }

    /// <summary>字体名称</summary>
    public string? FontName
    {
        get => GetAttr("FontName");
        set => SetAttr("FontName", value);
    }

    /// <summary>字体族名</summary>
    public string? FamilyName
    {
        get => GetAttr("FamilyName");
        set => SetAttr("FamilyName", value);
    }

    /// <summary>字体文件路径（相对于资源文件）</summary>
    public STLoc? FontFile
    {
        get
        {
            var v = GetChildText("FontFile");
            return v is null ? null : new STLoc(v);
        }
        set => SetChildText("FontFile", value?.Value);
    }

    /// <summary>
    /// 字形适用的字符分类（GB/T 33190 §11.1 表 44）。
    /// null 表示不写出 Charset 属性（解析时按 Unicode 处理）。
    /// 对应 Java: org.ofdrw.core.text.font.Charset + CT_Font.setCharset(Charset)
    /// </summary>
    public Charset? Charset
    {
        get
        {
            var v = GetAttr("Charset");
            return v is null ? null : CharsetUtil.Parse(v);
        }
        set => SetAttr("Charset", value.HasValue ? CharsetUtil.ToXml(value.Value) : null);
    }

    /// <summary>是否粗体</summary>
    public bool Bold
    {
        get => GetBoolAttr("Bold", false);
        set => SetBoolAttr("Bold", value);
    }

    /// <summary>是否斜体</summary>
    public bool Italic
    {
        get => GetBoolAttr("Italic", false);
        set => SetBoolAttr("Italic", value);
    }

    /// <summary>是否可嵌入</summary>
    public bool? Serif
    {
        get
        {
            var v = GetAttr("Serif");
            return v is null ? null : GetBoolAttr("Serif");
        }
        set { if (value.HasValue) SetBoolAttr("Serif", value.Value); }
    }
}
