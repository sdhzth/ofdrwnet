using System;
using System.Xml.Linq;
using NetOfd.Core;
using NetOfd.Core.BasicType;

namespace NetOfd.Layout.Areaholder;

/// <summary>
/// 区域占位区块 XML 记录（对应 Java: org.ofdrw.layout.areaholder.CT_AreaHolderBlock）。
/// <para>AreaHolderBlocks.xml 中的单条记录：AreaName/PageBlockID/Boundary 为属性，
/// PageFile 为子元素文本（ST_Loc 页面对象路径）。该文件为 ofdrw 文档扩展
/// （非 GB/T 33190 标准文件），命名空间同 OFD 核心命名空间。</para>
/// </summary>
public class CT_AreaHolderBlock : OFDElement
{
    /// <summary>从既有 XElement 包装（读取 AreaHolderBlocks.xml 时使用）。对应 Java (Element proxy) 构造。</summary>
    public CT_AreaHolderBlock(XElement element) : base(element) { }

    public CT_AreaHolderBlock() : base("AreaHolderBlock") { }

    /// <summary>创建区域占位区块并设置区域名称。</summary>
    public CT_AreaHolderBlock(string areaName) : this() => SetAreaName(areaName);

    /// <summary>【必选属性】设置区域名称。</summary>
    /// <exception cref="ArgumentException">areaName 为 null 时抛出</exception>
    public CT_AreaHolderBlock SetAreaName(string areaName)
    {
        if (areaName is null) throw new ArgumentException("区域名称（AreaName）不能为空");
        SetAttr("AreaName", areaName);
        return this;
    }

    /// <summary>获取区域名称。</summary>
    public string? GetAreaName() => GetAttr("AreaName");

    /// <summary>【可选属性】设置页块引用（PageBlockID）。</summary>
    /// <exception cref="ArgumentException">pageBlockId 为 null 时抛出</exception>
    public CT_AreaHolderBlock SetPageBlockID(STRefID? pageBlockId)
    {
        if (pageBlockId is null) throw new ArgumentException("页块引用（PageBlockID）不能为空");
        SetAttr("PageBlockID", pageBlockId.ToString());
        return this;
    }

    /// <summary>获取页块引用；属性缺失或空白返回 null。</summary>
    public STRefID? GetPageBlockID()
    {
        string? v = GetAttr("PageBlockID");
        if (string.IsNullOrWhiteSpace(v)) return null;
        return STRefID.Parse(v);
    }

    /// <summary>【可选属性】设置区域外接矩形（Boundary）。</summary>
    /// <exception cref="ArgumentException">boundary 为 null 时抛出</exception>
    public CT_AreaHolderBlock SetBoundary(STBox? boundary)
    {
        if (boundary is null) throw new ArgumentException("区域外接矩形（Boundary）为空");
        SetAttr("Boundary", boundary.ToString());
        return this;
    }

    /// <summary>获取区域外接矩形；属性缺失或空白返回 null。</summary>
    public STBox? GetBoundary()
    {
        string? v = GetAttr("Boundary");
        if (string.IsNullOrWhiteSpace(v)) return null;
        return STBox.Parse(v);
    }

    /// <summary>【必选元素】设置页面对象路径（PageFile 子元素，ST_Loc）。</summary>
    /// <exception cref="ArgumentException">pageLoc 为 null 或空时抛出</exception>
    public CT_AreaHolderBlock SetPageFile(STLoc? pageLoc)
    {
        if (pageLoc is null || pageLoc.IsEmpty) throw new ArgumentException("页面对象路径（PageFile）为空");
        SetChildText("PageFile", pageLoc.ToString());
        return this;
    }

    /// <summary>获取页面对象路径（PageFile 子元素）；不存在返回 null。</summary>
    public STLoc? GetPageFile()
    {
        string? v = GetChildText("PageFile");
        return v is null ? null : new STLoc(v);
    }

    /// <summary>
    /// 【Java 别名，忠实移植】Java CT_AreaHolderBlock.setFontFile(String absPath) 实为
    /// 委托 setPageFile(new ST_Loc(absPath))——方法名 FontFile 与行为 PageFile 不符属
    /// Java 命名误导（无字体复制逻辑），NetOfd 照抄行为并注明。
    /// </summary>
    public CT_AreaHolderBlock SetFontFile(string absPath) => SetPageFile(new STLoc(absPath));

    /// <summary>
    /// 【Java 别名，忠实移植】Java getFontFile() 实为读取 PageFile 子元素文本。
    /// </summary>
    public STLoc? GetFontFile() => GetPageFile();
}
