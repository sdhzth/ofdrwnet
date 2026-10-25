namespace NetOfd.Layout.Engine;

/// <summary>
/// 绘制上下文所需的资源管理能力抽象。
/// <para>
/// 抽象自 <see cref="ResManager"/>，使绘制上下文既能服务于新建文档
/// （虚拟容器模式），也能服务于已有文档的注释等编辑场景（映射模式）。
/// </para>
/// </summary>
public interface IResourceProvider
{
    /// <summary>注册（或复用）字体并返回字体 ID 字符串。</summary>
    string GetFontId(string fontFamily);

    /// <summary>注册外部字体文件并返回字体 ID。</summary>
    string AddFont(string familyName, string fontFilePath);

    /// <summary>添加媒体资源（图片）并返回媒体 ID。</summary>
    string AddMedia(string filePath);

    /// <summary>分配新的对象 ID。</summary>
    long NewId();
}
