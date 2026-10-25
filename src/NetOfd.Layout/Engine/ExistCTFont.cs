using NetOfd.Core.PageDescription.Font;

namespace NetOfd.Layout.Engine;

/// <summary>
/// 文档中已存在的字体对象。
/// <para>
/// 布局引擎注册字体时的返回载体：既给出文档内的 <see cref="CT_Font"/> 声明，
/// 也保留字体文件的绝对路径（<b>系统字体可能为空</b>——此时无需随文档打包字体文件）。
/// </para>
/// 对应 Java: org.ofdrw.layout.engine.ExistCTFont
/// </summary>
public sealed class ExistCTFont
{
    /// <summary>文档中的字体对象。</summary>
    public CT_Font Font { get; }

    /// <summary>字体文件绝对路径；若为操作系统自带字体则可能为 <c>null</c>。</summary>
    public string? AbsPath { get; }

    public ExistCTFont(CT_Font font, string? absPath)
    {
        Font = font;
        AbsPath = absPath;
    }
}
