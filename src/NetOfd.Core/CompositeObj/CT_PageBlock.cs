using System.Xml.Linq;

namespace NetOfd.Core.CompositeObj;

/// <summary>
/// 页块结构（7.7 页对象 图 17 表 16），可嵌套。
/// 对应 Java: org.ofdrw.core.basicStructure.pageObj.layer.block.CT_PageBlock
/// </summary>
/// <remarks>
/// Java 中本类实现 <c>PageBlockType</c> 接口，并由 TextObject/PathObject/ImageObject/
/// CompositeObject 等具体块类型继承；NetOfd 采用「CTLayer + 具体图元类（CTText/CTPath/CTImage）」
/// 的等价架构，故此处不引入 PageBlockType 体系，<see cref="GetPageBlocks"/> 统一以
/// <see cref="CT_PageBlock"/> 包装返回（保留各子元素原有标签名）。
/// </remarks>
public class CT_PageBlock : OFDElement
{
    public CT_PageBlock() : this("PageBlock") { }

    public CT_PageBlock(XElement element) : base(element) { }

    /// <summary>以指定元素名构造（供子类使用）。</summary>
    protected CT_PageBlock(string name) : base(name) { }

    /// <summary>【可选】嵌套添加子页块；null 时忽略。</summary>
    public CT_PageBlock AddPageBlock(CT_PageBlock pageBlock)
    {
        if (pageBlock is not null) AddOFDEntity(pageBlock);
        return this;
    }

    /// <summary>【可选】获取当前页块内的所有子页块（保留原标签名）。</summary>
    public List<CT_PageBlock> GetPageBlocks() =>
        _el.Elements().Select(e => new CT_PageBlock(e)).ToList();
}
