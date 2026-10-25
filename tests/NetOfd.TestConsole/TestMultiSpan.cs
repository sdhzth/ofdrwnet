using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Core.PageDescription.Color;

/// <summary>
/// 测试 MultiSpanStyles - 验证不同样式 Span 的渲染
/// </summary>
public static class TestMultiSpan
{
    public static void Run()
    {
        var outPath = "e:/电子病历/MultiSpanStyles.ofd";
        using var doc = new OFDDoc(outPath);

        // 创建三个不同样式的 Span
        var sp1 = new Span("坚持就是胜利，").SetFontSize(5d).SetUnderline();
        var sp2 = new Span("加油！").SetFontSize(10d).SetBold();
        var sp3 = new Span("奥力给！").SetFontSize(15d)
                    .SetColor(new CT_Color { Value = "255,0,0" });

        var p = new Paragraph()
            .SetWidth(100)
            .Add(sp1).Add(sp2).Add(sp3);
        doc.Add(p);
        doc.Close();

        Console.WriteLine($"✓ MultiSpanStyles.ofd 已生成: {outPath}");
    }
}
