using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core.BasicType;

namespace NetOfd.Core.PageDescription.Color;

/// <summary>
/// 渐变底纹公共基类，承载轴向渐变与径向渐变共享的属性与段列表
/// （GB/T 33190 §8.3.4.2 表 29）。
/// <para>.NET 侧实现细节：Java ofdrw 中 CT_AxialShd 与 CT_RadialShd 为两个互继承无关的独立类，
/// 此处抽取公共基类仅为复用，序列化结果（元素名/属性名）与 Java 完全一致。</para>
/// </summary>
public abstract class CT_Shd : OFDElement, ColorClusterType
{
    protected CT_Shd(string localName) : base(localName) { }
    protected CT_Shd(XElement element) : base(element) { }

    /// <summary>【可选 属性】渐变绘制的方式，默认值 <see cref="MapType.Direct"/>。</summary>
    public MapType MapType
    {
        get => MapTypeUtil.Parse(GetAttr("MapType"));
        set => SetAttr("MapType", MapTypeUtil.ToXml(value));
    }

    /// <summary>
    /// 【可选 属性】轴线一个渐变区间的长度，仅当 MapType 不为 Direct 时出现，默认值为轴线长度。
    /// </summary>
    public double? MapUnit
    {
        get
        {
            var v = GetAttr("MapUnit");
            return v is null ? null : STBase.ToDouble(v);
        }
        set => SetAttr("MapUnit", value is null ? null : STBase.Fmt(value.Value));
    }

    /// <summary>【可选 属性】轴线延长线方向是否继续绘制渐变，默认 <see cref="Extend.None"/>（0）。</summary>
    public Extend Extend
    {
        get => ExtendUtil.Parse(GetAttr("Extend"));
        set => SetAttr("Extend", ExtendUtil.ToXml(value));
    }

    /// <summary>【必选 属性】轴线/起始椭圆的中心点。</summary>
    public STPos? StartPoint
    {
        get
        {
            var v = GetAttr("StartPoint");
            return string.IsNullOrWhiteSpace(v) ? null : STPos.Parse(v);
        }
        set => SetAttr("StartPoint", value?.ToString());
    }

    /// <summary>【必选 属性】轴线结束点/结束椭圆的中心点。</summary>
    public STPos? EndPoint
    {
        get
        {
            var v = GetAttr("EndPoint");
            return string.IsNullOrWhiteSpace(v) ? null : STPos.Parse(v);
        }
        set => SetAttr("EndPoint", value?.ToString());
    }

    /// <summary>【必选】追加一个颜色段。</summary>
    public CT_Shd AddSegment(Segment segment)
    {
        if (segment is null) throw new ArgumentNullException(nameof(segment), "段（Segment）不能为空");
        AddOFDEntity(segment);
        return this;
    }

    /// <summary>【必选】追加一个颜色段（便捷重载：位置 + 颜色）。</summary>
    public CT_Shd AddSegment(double position, CT_Color color) => AddSegment(new Segment(position, color));

    /// <summary>获取全部颜色段（按文档顺序）。</summary>
    public List<Segment> GetSegments() =>
        GetChildElements("Segment").Select(e => new Segment(e)).ToList();
}
