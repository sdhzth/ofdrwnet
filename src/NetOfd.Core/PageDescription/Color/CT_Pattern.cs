using System;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 底纹（Pattern）：复杂颜色的一种，用于图形和文字的填充以及描边处理
/// （GB/T 33190 §8.3.3 图 26 表 28）。
/// <para>对应 Java: org.ofdrw.core.pageDescription.color.pattern.CT_Pattern</para>
/// </summary>
public class CT_Pattern : OFDElement, ColorClusterType
{
    public CT_Pattern() : base("Pattern") { }
    public CT_Pattern(XElement element) : base(element) { }

    /// <summary>【必选 属性】底纹单元宽度（mm）。</summary>
    public double Width
    {
        get
        {
            var v = GetAttr("Width");
            if (v is null) throw new InvalidOperationException("底纹格式非法，底纹单元宽度不能为空");
            return STBase.ToDouble(v);
        }
        set => SetAttr("Width", STBase.Fmt(value));
    }

    /// <summary>【必选 属性】底纹单元高度（mm）。</summary>
    public double Height
    {
        get
        {
            var v = GetAttr("Height");
            if (v is null) throw new InvalidOperationException("底纹格式非法，底纹单元高度不能为空");
            return STBase.ToDouble(v);
        }
        set => SetAttr("Height", STBase.Fmt(value));
    }

    /// <summary>
    /// 【可选 属性】X 方向底纹单元间距，默认值为底纹单元宽度；
    /// 若设定值小于底纹单元宽度，按默认值（宽度）处理。
    /// </summary>
    public double XStep
    {
        get
        {
            var v = GetAttr("XStep");
            if (v is null) return Width;
            var step = STBase.ToDouble(v);
            return step < Width ? Width : step;
        }
        set => SetAttr("XStep", value < Width ? STBase.Fmt(Width) : STBase.Fmt(value));
    }

    /// <summary>
    /// 【可选 属性】Y 方向底纹单元间距，默认值为底纹单元高度；
    /// 若设定值小于底纹单元高度，按默认值（高度）处理。
    /// </summary>
    public double YStep
    {
        get
        {
            var v = GetAttr("YStep");
            if (v is null) return Height;
            var step = STBase.ToDouble(v);
            return step < Height ? Height : step;
        }
        set => SetAttr("YStep", value < Height ? STBase.Fmt(Height) : STBase.Fmt(value));
    }

    /// <summary>移除 XStep 属性（恢复默认 = 底纹单元宽度）。</summary>
    public CT_Pattern RemoveXStep() { SetAttr("XStep", null); return this; }

    /// <summary>移除 YStep 属性（恢复默认 = 底纹单元高度）。</summary>
    public CT_Pattern RemoveYStep() { SetAttr("YStep", null); return this; }

    /// <summary>【可选 属性】底纹单元的翻转方式，默认 <see cref="ReflectMethod.Normal"/>。</summary>
    public ReflectMethod ReflectMethod
    {
        get => ReflectMethodUtil.Parse(GetAttr("ReflectMethod"));
        set => SetAttr("ReflectMethod", ReflectMethodUtil.ToXml(value));
    }

    /// <summary>【可选 属性】底纹单元起始绘制位置，默认 <see cref="RelativeTo.Object"/>。</summary>
    public RelativeTo RelativeTo
    {
        get => RelativeToUtil.Parse(GetAttr("RelativeTo"));
        set => SetAttr("RelativeTo", RelativeToUtil.ToXml(value));
    }

    /// <summary>
    /// 【可选 属性】底纹单元的变换矩阵（6 数）。底纹呈现时先做 XStep/YStep 排列，
    /// 然后整体做 CTM 变换；默认为单位矩阵。
    /// </summary>
    public STArray? CTM
    {
        get
        {
            var v = GetAttr("CTM");
            return v is null ? null : STArray.Parse(v);
        }
        set => SetAttr("CTM", value?.ToString());
    }

    /// <summary>【必选】设置底纹单元。</summary>
    public CT_Pattern SetCellContent(CellContent cellContent)
    {
        if (cellContent is null) throw new ArgumentNullException(nameof(cellContent));
        RemoveCellContent();
        Element.Add(cellContent.Element);
        return this;
    }

    /// <summary>【必选】获取底纹单元，不存在时返回 null（Java 抛异常，.NET 以 null 表达更安全）。</summary>
    public CellContent? GetCellContent()
    {
        var e = GetChildElement("CellContent");
        return e is null ? null : new CellContent(e);
    }

    /// <summary>移除已有底纹单元子元素。</summary>
    public CT_Pattern RemoveCellContent()
    {
        Element.Elements().Where(x => x.Name.LocalName == "CellContent").Remove();
        return this;
    }
}
