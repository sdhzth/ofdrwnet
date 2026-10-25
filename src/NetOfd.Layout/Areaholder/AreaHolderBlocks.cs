using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using NetOfd.Core;

namespace NetOfd.Layout.Areaholder;

/// <summary>
/// 区域占位区块列表（对应 Java: org.ofdrw.layout.areaholder.AreaHolderBlocks）。
/// <para>AreaHolderBlocks.xml 的根模型（ofdrw 文档扩展文件，存放于 Doc 目录，
/// 常量 <see cref="AreaHolderBlocksFile"/> 为文件名）。</para>
/// </summary>
public class AreaHolderBlocks : OFDElement
{
    /// <summary>AreaHolderBlocks.xml 文件名（对应 Java AreaHolderBlocksFile 常量）。</summary>
    public const string AreaHolderBlocksFile = "AreaHolderBlocks.xml";

    /// <summary>从既有 XElement 包装（读取 AreaHolderBlocks.xml 时使用）。对应 Java (Element proxy) 构造。</summary>
    public AreaHolderBlocks(XElement element) : base(element) { }

    public AreaHolderBlocks() : base("AreaHolderBlocks") { }

    /// <summary>追加一个或多个区域占位区块（null/空数组直接返回自身）。</summary>
    public AreaHolderBlocks AddAreaHolderBlock(params CT_AreaHolderBlock[]? area)
    {
        if (area is null || area.Length == 0) return this;
        foreach (CT_AreaHolderBlock holderBlock in area)
            AddOFDEntity(holderBlock);
        return this;
    }

    /// <summary>获取全部区域占位区块（按文档出现顺序）。</summary>
    public List<CT_AreaHolderBlock> GetAreaHolderBlocks()
        => GetChildElements("AreaHolderBlock").Select(e => new CT_AreaHolderBlock(e)).ToList();
}
