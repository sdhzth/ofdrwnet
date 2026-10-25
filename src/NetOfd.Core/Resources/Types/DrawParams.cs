using System.Xml.Linq;
using NetOfd.Core.PageDescription.DrawParam;

namespace NetOfd.Core.Resources;

/// <summary>
/// 绘制参数资源集合（7.9 图 20 表 18）。
/// 对应 Java: org.ofdrw.core.basicStructure.res.resources.DrawParams
/// </summary>
public sealed class DrawParams : OFDElement, IOFDResource
{
    public DrawParams() : base("DrawParams") { }

    public DrawParams(XElement element) : base(element) { }

    /// <summary>
    /// 【必选】添加绘制参数描述；null 时忽略，缺少 ID 时抛 <see cref="ArgumentException"/>。
    /// </summary>
    public DrawParams AddDrawParam(CT_DrawParam drawParam)
    {
        if (drawParam is null) return this;
        if (string.IsNullOrWhiteSpace(drawParam.Id))
            throw new ArgumentException("绘制参数描述ID不能为空");
        AddOFDEntity(drawParam);
        return this;
    }

    /// <summary>【必选】获取绘制参数描述序列。</summary>
    public List<CT_DrawParam> GetDrawParams() =>
        GetChildElements("DrawParam").Select(e => new CT_DrawParam(e)).ToList();
}
