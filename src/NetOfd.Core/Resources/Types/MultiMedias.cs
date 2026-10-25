using System.Xml.Linq;

namespace NetOfd.Core.Resources;

/// <summary>
/// 多媒体资源集合（7.9 图 20 表 18）：图像、音频、视频等。
/// 对应 Java: org.ofdrw.core.basicStructure.res.resources.MultiMedias
/// </summary>
public sealed class MultiMedias : OFDElement, IOFDResource
{
    public MultiMedias() : base("MultiMedias") { }

    public MultiMedias(XElement element) : base(element) { }

    /// <summary>
    /// 【必选】添加多媒体资源描述；null 时忽略，缺少 ID 时抛 <see cref="ArgumentException"/>。
    /// </summary>
    public MultiMedias AddMultiMedia(CT_MultiMedia multiMedia)
    {
        if (multiMedia is null) return this;
        if (multiMedia.GetObjID() is null) throw new ArgumentException("多媒体资源描述ID不能为空");
        AddOFDEntity(multiMedia);
        return this;
    }

    /// <summary>【必选】获取多媒体资源描述列表。</summary>
    public List<CT_MultiMedia> GetMultiMedias() =>
        GetChildElements("MultiMedia").Select(e => new CT_MultiMedia(e)).ToList();
}
