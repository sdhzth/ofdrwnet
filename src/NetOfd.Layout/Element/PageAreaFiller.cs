namespace NetOfd.Layout.Element;

/// <summary>
/// 页面剩余空间填充。
/// <para>
/// 该元素不会被布局分析器解析，只是作为一个命令标志，
/// 告诉分析器使剩余的空间为 0，也就是构造一个特殊的段。
/// </para>
/// 对应 Java: org.ofdrw.layout.element.PageAreaFiller
/// </summary>
public class PageAreaFiller : Div
{
    public PageAreaFiller()
    {
        Float = AFloat.Center;
        Position = Position.Static;
    }

    public override string ElementType => "PageAreaFiller";
}