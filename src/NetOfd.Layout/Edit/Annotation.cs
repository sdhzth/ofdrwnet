using NetOfd.Core.Annotation.PageAnnot;
using NetOfd.Core.BasicType;
using NetOfd.Layout.Element.Canvas;

namespace NetOfd.Layout.Edit;

/// <summary>
/// 注释对象构建器。
/// <para>
/// 注释是板式文档形成后附加的图文信息，通过绘制器描述注释的外观内容，
/// 经由 <see cref="OfdAnnotator"/> 写入已有 OFD 文档。
/// </para>
/// 对应 Java: org.ofdrw.layout.edit.Annotation
/// </summary>
public class Annotation
{
    /// <summary>注释绘制器</summary>
    public IDrawer Drawer { get; set; }

    /// <summary>注释类型</summary>
    public AnnotType Type { get; set; }

    /// <summary>注释创建者</summary>
    public string? Creator { get; set; }

    /// <summary>最近一次修改的时间</summary>
    public DateOnly? LastModDate { get; set; }

    /// <summary>注释子类型</summary>
    public string? Subtype { get; private set; }

    /// <summary>该注释对象是否显示（null 表示未设置，默认 true）</summary>
    public bool? Visible { get; private set; }

    /// <summary>对象的 Remark 信息是否不随页面旋转而旋转</summary>
    public bool? NoRotate { get; private set; }

    /// <summary>一组注释参数（保持加入顺序）</summary>
    private readonly List<KeyValuePair<string, string>> _parameters = new();

    /// <summary>注释说明内容</summary>
    public string? Remark { get; set; }

    /// <summary>对象的 Remark 信息是否随页面一起打印</summary>
    public bool? Print { get; private set; }

    /// <summary>对象的 Remark 信息是否不能被用户更改</summary>
    public bool? ReadOnly { get; private set; }

    /// <summary>对象的 Remark 信息是否不随页面缩放而同步缩放</summary>
    public bool? NoZoom { get; private set; }

    /// <summary>注释空间</summary>
    public STBox Boundary { get; set; }

    /// <summary>创建注释对象。</summary>
    /// <param name="x">所处页面 X 坐标</param>
    /// <param name="y">所处页面 Y 坐标</param>
    /// <param name="width">画布宽度</param>
    /// <param name="height">画布高度</param>
    /// <param name="type">注释类型</param>
    /// <param name="drawer">注释绘制器</param>
    public Annotation(double x, double y, double width, double height,
        AnnotType type, IDrawer drawer)
        : this(new STBox(x, y, width, height), type, drawer)
    {
    }

    /// <summary>创建注解对象。</summary>
    /// <param name="boundary">注释对象绘制空间</param>
    /// <param name="type">注释类型</param>
    /// <param name="drawer">注释绘制器</param>
    public Annotation(STBox boundary, AnnotType type, IDrawer drawer)
    {
        Boundary = boundary ?? throw new ArgumentException("注释对象绘制空间(boundary)不能为空");
        Type = type;
        Drawer = drawer ?? throw new ArgumentException("注释绘制器(drawer)不能为空");
    }

    /// <summary>
    /// 构建注释底层对象。
    /// </summary>
    /// <param name="objId">注释对象 ID（由调用方从文档最大对象 ID 递增分配）</param>
    public Annot Build(long objId)
    {
        var annot = new Annot
        {
            Type = Type,
            Creator = Creator ?? "OFD R&W",
            LastModDate = LastModDate ?? DateOnly.FromDateTime(DateTime.Now)
        };
        annot.SetObjID(new STID(objId));

        if (Subtype != null) annot.Subtype = Subtype;
        if (Visible != null) annot.Visible = Visible.Value;
        if (Print != null) annot.Print = Print.Value;
        if (NoZoom != null) annot.NoZoom = NoZoom.Value;
        if (NoRotate != null) annot.NoRotate = NoRotate.Value;
        if (ReadOnly != null) annot.ReadOnly = ReadOnly.Value;
        if (Remark != null) annot.Remark = Remark;
        foreach (var kv in _parameters)
            annot.AddParameter(kv.Key, kv.Value);

        annot.Appearance = new Appearance(Boundary);
        return annot;
    }

    public STBox GetBoundary() => Boundary;

    public Annotation SetBoundary(STBox boundary)
    {
        Boundary = boundary ?? throw new ArgumentException("注释对象绘制空间(boundary)不能为空");
        return this;
    }

    public Annotation SetCreator(string creator) { Creator = creator; return this; }

    public Annotation SetLastModDate(DateOnly lastModDate) { LastModDate = lastModDate; return this; }

    public Annotation SetSubtype(string subtype) { Subtype = subtype; return this; }

    public Annotation SetRemark(string remark) { Remark = remark; return this; }

    /// <summary>设置该注释对象是否显示（默认 true）。</summary>
    public Annotation SetVisible(bool visible) { Visible = visible; return this; }

    /// <summary>设置对象的 Remark 信息是否随页面一起打印（默认 true）。</summary>
    public Annotation SetPrint(bool print) { Print = print; return this; }

    /// <summary>设置对象的 Remark 信息是否不随页面缩放而同步缩放（默认 false）。</summary>
    public Annotation SetNoZoom(bool noZoom) { NoZoom = noZoom; return this; }

    /// <summary>设置对象的 Remark 信息是否不随页面旋转而旋转（默认 false）。</summary>
    public Annotation SetNoRotate(bool noRotate) { NoRotate = noRotate; return this; }

    /// <summary>设置对象的 Remark 信息是否不能被用户更改（默认 true）。</summary>
    public Annotation SetReadOnly(bool readOnly) { ReadOnly = readOnly; return this; }

    /// <summary>增加注释参数（键名相同则覆盖，保持首次加入顺序）。</summary>
    public Annotation AddParameter(string name, string parameter)
    {
        if (string.IsNullOrWhiteSpace(name)) return this;
        for (int i = 0; i < _parameters.Count; i++)
        {
            if (_parameters[i].Key == name)
            {
                _parameters[i] = new KeyValuePair<string, string>(name, parameter);
                return this;
            }
        }
        _parameters.Add(new KeyValuePair<string, string>(name, parameter));
        return this;
    }

    /// <summary>获取注释参数列表（不存在为空列表）。</summary>
    public IReadOnlyList<KeyValuePair<string, string>> GetParameters() => _parameters;
}
