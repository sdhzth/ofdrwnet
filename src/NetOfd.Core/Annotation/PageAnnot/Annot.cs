using System.Globalization;
using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.Annotation.PageAnnot;

/// <summary>
/// 注释对象。
/// <para>GB/T 33190-2016 第 15.2 节</para>
/// 对应 Java: org.ofdrw.core.annotation.pageannot.Annot
/// </summary>
public class Annot : OFDElement
{
    public Annot() : base("Annot") { }

    public Annot(XElement element) : base(element) { }

    // ── 必选属性 ─────────────────────────────────────────────

    /// <summary>注释的标识（ID 属性）。</summary>
    public STID? ID
    {
        get => GetObjID();
        set => SetObjID(value!);
    }

    /// <summary>【必选】注释类型。</summary>
    public AnnotType Type
    {
        get => AnnotTypeParser.Parse(GetAttr("Type"));
        set => SetAttr("Type", value.ToString());
    }

    /// <summary>【必选】注释创建者。</summary>
    public string? Creator
    {
        get => GetAttr("Creator");
        set => SetAttr("Creator", value);
    }

    /// <summary>【必选】最近一次修改的时间（格式 yyyy-MM-dd）。</summary>
    public DateOnly? LastModDate
    {
        get
        {
            var v = GetAttr("LastModDate");
            if (v is null) return null;
            if (DateOnly.TryParseExact(v, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var d)) return d;
            return null;
        }
        set => SetAttr("LastModDate",
            value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
    }

    // ── 可选属性 ─────────────────────────────────────────────

    /// <summary>【可选】注释子类型。</summary>
    public string? Subtype
    {
        get => GetAttr("Subtype");
        set => SetAttr("Subtype", value);
    }

    /// <summary>【可选】该注释对象是否显示，默认 true。</summary>
    public bool? Visible
    {
        get
        {
            var v = GetAttr("Visible");
            return v is null ? null : ParseBool(v);
        }
        set => SetAttr("Visible", value?.ToString().ToLowerInvariant());
    }

    /// <summary>【可选】对象的 Remark 信息是否随页面一起打印，默认 true。</summary>
    public bool? Print
    {
        get
        {
            var v = GetAttr("Print");
            return v is null ? null : ParseBool(v);
        }
        set => SetAttr("Print", value?.ToString().ToLowerInvariant());
    }

    /// <summary>【可选】对象的 Remark 信息是否不随页面缩放而同步缩放，默认 false。</summary>
    public bool? NoZoom
    {
        get
        {
            var v = GetAttr("NoZoom");
            return v is null ? null : ParseBool(v);
        }
        set => SetAttr("NoZoom", value?.ToString().ToLowerInvariant());
    }

    /// <summary>【可选】对象的 Remark 信息是否不随页面旋转而旋转，默认 false。</summary>
    public bool? NoRotate
    {
        get
        {
            var v = GetAttr("NoRotate");
            return v is null ? null : ParseBool(v);
        }
        set => SetAttr("NoRotate", value?.ToString().ToLowerInvariant());
    }

    /// <summary>【可选】对象的 Remark 信息是否不能被用户更改，默认 true。</summary>
    public bool? ReadOnly
    {
        get
        {
            var v = GetAttr("ReadOnly");
            return v is null ? null : ParseBool(v);
        }
        set => SetAttr("ReadOnly", value?.ToString().ToLowerInvariant());
    }

    // ── 子元素 ───────────────────────────────────────────────

    /// <summary>【可选】注释说明内容。</summary>
    public string? Remark
    {
        get => GetChildText("Remark");
        set => SetChildText("Remark", value);
    }

    /// <summary>【可选】增加注释参数（键名相同则覆盖）。</summary>
    public Annot AddParameter(string name, string parameter)
    {
        if (string.IsNullOrWhiteSpace(name)) return this;
        var parameters = GetChildElement("Parameters");
        if (parameters is null)
        {
            parameters = new XElement(OfdNs + "Parameters");
            _el.Add(parameters);
        }
        foreach (var e in parameters.Elements())
        {
            if (e.Attribute("Name")?.Value == name)
            {
                e.Value = parameter ?? "";
                return this;
            }
        }
        var p = new XElement(OfdNs + "Parameter", parameter ?? "");
        p.SetAttributeValue("Name", name);
        parameters.Add(p);
        return this;
    }

    /// <summary>【可选】获取一组注释参数（不存在返回空字典）。</summary>
    public Dictionary<string, string> GetParameters()
    {
        var res = new Dictionary<string, string>();
        var e = GetChildElement("Parameters");
        if (e is null) return res;
        foreach (var p in e.Elements())
        {
            if (p.Name.LocalName != "Parameter") continue;
            var name = p.Attribute("Name")?.Value;
            if (name is not null) res[name] = p.Value;
        }
        return res;
    }

    /// <summary>【必选】注释的静态显示效果（使用页面块定义来描述）。</summary>
    public Appearance? Appearance
    {
        get
        {
            var e = GetChildElement("Appearance");
            return e is null ? null : new Appearance(e);
        }
        set
        {
            if (value is null)
            {
                GetChildElement("Appearance")?.Remove();
                return;
            }
            SetOFDEntity(value);
        }
    }

    private static bool ParseBool(string v) =>
        v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1";
}
