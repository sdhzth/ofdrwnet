using NetOfd.Layout;
using NetOfd.Layout.Element;
using NetOfd.Core.PageDescription.Color;
using Xunit;

namespace NetOfd.Tests;

/// <summary>
/// 测试 Paragraph/Span 渲染功能
/// </summary>
public class ParagraphRenderTest
{
    [Fact]
    public void TestMultiSpanStyles_ShouldRenderTextObjects()
    {
        // Arrange
        var outPath = Path.Combine(Path.GetTempPath(), "MultiSpanStyles_Test.ofd");
        Exception? caughtException = null;

        try
        {
            // Act
            using (var doc = new OFDDoc(outPath))
            {
                var sp1 = new Span("坚持就是胜利，").SetFontSize(5d).SetUnderline();
                var sp2 = new Span("加油！").SetFontSize(10d).SetBold();
                var sp3 = new Span("奥力给！").SetFontSize(15d)
                            .SetColor(new CT_Color { Value = "255,0,0" });

                var p = new Paragraph()
                    .SetWidth(100)
                    .Add(sp1).Add(sp2).Add(sp3);
                doc.Add(p);
            }
        }
        catch (Exception ex)
        {
            caughtException = ex;
        }

        // 如果有异常，输出详细信息
        if (caughtException != null)
        {
            Console.WriteLine($"异常类型: {caughtException.GetType().FullName}");
            Console.WriteLine($"异常消息: {caughtException.Message}");
            Console.WriteLine($"堆栈跟踪:\n{caughtException.StackTrace}");
            if (caughtException.InnerException != null)
            {
                Console.WriteLine($"内部异常: {caughtException.InnerException.Message}");
            }
            Assert.Fail($"OFDDoc 抛出异常: {caughtException.Message}");
        }

        // 检查文件是否存在
        Console.WriteLine($"检查文件: {outPath}");
        Console.WriteLine($"文件是否存在: {File.Exists(outPath)}");

        // Assert - 验证文件已生成
        Assert.True(File.Exists(outPath), "OFD 文件应该被生成");

        // 解压并验证内容
        var extractPath = Path.Combine(Path.GetTempPath(), "MultiSpanStyles_Extract");
        if (Directory.Exists(extractPath))
            Directory.Delete(extractPath, true);

        System.IO.Compression.ZipFile.ExtractToDirectory(outPath, extractPath);

        var contentPath = Path.Combine(extractPath, "Doc_0", "Pages", "Page_0", "Content.xml");
        Assert.True(File.Exists(contentPath), "Content.xml 应该存在");

        var content = File.ReadAllText(contentPath);
        Console.WriteLine($"Content.xml 内容:\n{content}");

        // 验证包含 TextObject
        Assert.Contains("TextObject", content, StringComparison.OrdinalIgnoreCase);

        // 清理
        File.Delete(outPath);
        Directory.Delete(extractPath, true);
    }
}
