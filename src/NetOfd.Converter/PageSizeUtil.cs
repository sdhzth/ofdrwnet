using NetOfd.Core.BasicStructure.Doc;
using NetOfd.Reader;

namespace NetOfd.Converter;

/// <summary>
/// 页面物理尺寸解析辅助（单位：毫米）。
/// 对应 Java: org.ofdrw.reader.PageInfo#getSize 的取值优先级。
/// </summary>
public static class PageSizeUtil
{
    /// <summary>
    /// 解析指定页的物理尺寸（mm）。
    /// 取值优先级：<c>Page/Area/PhysicalBox</c> → <c>Document/CommonData/PageArea/PhysicalBox</c> → A4。
    /// </summary>
    /// <param name="reader">OFD 解析器</param>
    /// <param name="pageIndex">页码（0 起）</param>
    public static (double Width, double Height) GetPageSize(OFDReader reader, int pageIndex)
    {
        var page = reader.GetPage(pageIndex);
        if (page?.Area?.PhysicalBox is { } pb)
            return (pb.Width, pb.Height);

        var common = reader.GetDocument().CommonData;
        if (common?.PageArea?.PhysicalBox is { } cpb)
            return (cpb.Width, cpb.Height);

        return (CT_PageArea.A4.Width, CT_PageArea.A4.Height);
    }
}
