using System.Xml.Linq;
using NetOfd.Core.BasicType;
using NetOfd.Core.PageDescription.Text;

namespace NetOfd.Core.PageDescription;

/// <summary>
/// CT_Text - 文字对象。
/// 对应 Java: org.ofdrw.core.pageDescription.content.layer.TextObject
/// </summary>
public class CTText : CTGraphicUnit
{
    public CTText() : base("TextObject") { }
    public CTText(XElement element) : base(element) { }

    /// <summary>字体 ID 引用</summary>
    public STRefID? Font
    {
        get
        {
            var v = GetAttr("Font");
            return v is null ? null : STRefID.Parse(v);
        }
        set => SetAttr("Font", value?.Value.ToString());
    }

    /// <summary>字体大小（磅值）</summary>
    public double Size
    {
        get => GetDoubleAttr("Size", 9.0);
        set => SetDoubleAttr("Size", value);
    }

    /// <summary>是否斜体</summary>
    public bool Italic
    {
        get => GetBoolAttr("Italic", false);
        set => SetBoolAttr("Italic", value);
    }

    /// <summary>是否粗体（文本级覆盖，优先级高于字体资源自身设置）</summary>
    public bool Bold
    {
        get => GetBoolAttr("Bold", false);
        set => SetBoolAttr("Bold", value);
    }

    /// <summary>字重（粗细值，对应 OFD 表 45 Weight 属性，取值 100/200/300/400/500/600/700/800/900）。
    /// null 表示不写出 Weight 属性（沿用字体资源默认 400）。
    /// 对应 Java <c>org.ofdrw.core.text.text.Weight</c> + <c>CT_Text.setWeight(Weight)</c>。</summary>
    public int? Weight
    {
        get
        {
            var v = GetAttr("Weight");
            return v is null ? null : (int)STBase.ToDouble(v);
        }
        set
        {
            if (value.HasValue) SetAttr("Weight", value.Value.ToString());
            else if (GetAttr("Weight") is not null) _el.Attribute("Weight")?.Remove();
        }
    }

    /// <summary>是否对文字进行描边（轮廓字）。OFD 默认 false（仅填充）。</summary>
    public bool Stroke
    {
        get => GetBoolAttr("Stroke", false);
        set => SetBoolAttr("Stroke", value);
    }

    /// <summary>是否对文字进行填充。OFD 默认 true。</summary>
    public bool Fill
    {
        get => GetBoolAttr("Fill", true);
        set => SetBoolAttr("Fill", value);
    }

    /// <summary>文本方向（0=水平，90=竖直）</summary>
    public int ReadDirection
    {
        get => (int)GetDoubleAttr("ReadDirection", 0);
        set => SetAttr("ReadDirection", value.ToString());
    }

    /// <summary>字符方向</summary>
    public int CharDirection
    {
        get => (int)GetDoubleAttr("CharDirection", 0);
        set => SetAttr("CharDirection", value.ToString());
    }

    /// <summary>字型横向缩放比例（HScale），1.0 为正常；&lt;1 压缩，&gt;1 拉伸。对应 OFD 的 HScale 属性。</summary>
    public double HScale
    {
        get => GetDoubleAttr("HScale", 1.0);
        set => SetDoubleAttr("HScale", value);
    }

    /// <summary>文字颜色填充</summary>
    public Color.CT_Color? TextColor
    {
        get
        {
            var e = GetChildElement("FillColor");
            return e is null ? null : new Color.CT_Color(e);
        }
        set
        {
            if (value is null) GetChildElement("FillColor")?.Remove();
            else
            {
                value._el.Name = OfdNs + "FillColor";
                SetOFDEntity(value);
            }
        }
    }

    /// <summary>添加文字代码块</summary>
    public CTText AddTextCode(TextCode code)
    {
        AddOFDEntity(code);
        return this;
    }

    /// <summary>获取所有文字代码块</summary>
    public List<TextCode> GetTextCodes()
    {
        return GetChildElements("TextCode")
            .Select(e => new TextCode(e))
            .ToList();
    }

    /// <summary>
    /// 添加字形变换（CGTransform，GB/T 33190 §11.4.1 表 48）。
    /// <para>当 OFD 内嵌子集化字体、字符码与字形索引不再一一对应时，必须据此取字形。</para>
    /// 对应 Java: CT_Text.addCGTransform(CT_CGTransform)
    /// </summary>
    public CTText AddCGTransform(CT_CGTransform cgTransform)
    {
        AddOFDEntity(cgTransform);
        return this;
    }

    /// <summary>
    /// 获取全部字形变换（CGTransform）。
    /// 对应 Java: CT_Text.getCGTransforms()
    /// </summary>
    public List<CT_CGTransform> GetCGTransforms()
    {
        return GetChildElements("CGTransform")
            .Select(e => new CT_CGTransform(e))
            .ToList();
    }

    /// <summary>移除全部字形变换（替换文本后原字形码位失效时应调用）。</summary>
    public CTText ClearCGTransforms()
    {
        foreach (var e in GetChildElements("CGTransform").ToList())
            e.Remove();
        return this;
    }

    /// <summary>
    /// 阅读方向的强类型视图。<see cref="ReadDirection"/> 的数值版本；
    /// 取值仅限 0/90/180/270，写入时经 <see cref="DirectionUtil"/> 校验。
    /// </summary>
    public Direction ReadDirectionAngle
    {
        get => DirectionUtil.Parse(ReadDirection);
        set => ReadDirection = DirectionUtil.Angle(value);
    }

    /// <summary>
    /// 字符方向的强类型视图。<see cref="CharDirection"/> 的数值版本；
    /// 取值仅限 0/90/180/270，写入时经 <see cref="DirectionUtil"/> 校验。
    /// </summary>
    public Direction CharDirectionAngle
    {
        get => DirectionUtil.Parse(CharDirection);
        set => CharDirection = DirectionUtil.Angle(value);
    }

    /// <summary>
    /// 字重的强类型视图。<see cref="Weight"/> 的数值版本；
    /// 取值仅限 100~900 的整百值，写入时经 <see cref="WeightUtil"/> 校验。
    /// </summary>
    public Text.Weight WeightKind
    {
        get => WeightUtil.Parse(Weight ?? 400);
        set => Weight = WeightUtil.Value(value);
    }
}

/// <summary>
/// 文字代码块（TextCode），包含位置和文字内容。
/// </summary>
public class TextCode : OFDElement
{
    public TextCode() : base("TextCode") { }
    public TextCode(XElement element) : base(element) { }

    /// <summary>起始 X 坐标</summary>
    public double X
    {
        get => GetDoubleAttr("X", 0);
        set => SetDoubleAttr("X", value);
    }

    /// <summary>起始 Y 坐标</summary>
    public double Y
    {
        get => GetDoubleAttr("Y", 0);
        set => SetDoubleAttr("Y", value);
    }

    /// <summary>字符间距（空格分隔）</summary>
    public string? DeltaX
    {
        get => GetAttr("DeltaX");
        set => SetAttr("DeltaX", value);
    }

    /// <summary>行间距（空格分隔）</summary>
    public string? DeltaY
    {
        get => GetAttr("DeltaY");
        set => SetAttr("DeltaY", value);
    }

    /// <summary>文字区域宽度。当指定 Width 且未给出 DeltaX 时，字形应在该宽度内均匀分布（两端对齐）。</summary>
    public double? Width
    {
        get => GetAttr("Width") is { } v ? STBase.ToDouble(v) : null;
        set => SetAttr("Width", value.HasValue ? STBase.Fmt(value.Value) : null);
    }

    /// <summary>文字内容</summary>
    public string? Text
    {
        get => _el.Value;
        set => _el.Value = value ?? string.Empty;
    }
}
