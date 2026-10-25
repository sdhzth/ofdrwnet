namespace NetOfd.Reader.Model;

/// <summary>
/// 电子印章数据：印章图片字节 + 图片类型。
/// 对应 Java: org.ofdrw.reader.model.SealDataVo
/// <para>
/// 常见取值：<c>ofd</c> / <c>png</c> / <c>gif</c> / <c>svg</c> / <c>jpeg</c>，
/// 需按类型选择对应的解码方式。
/// </para>
/// </summary>
public class SealDataVo
{
    /// <summary>印章图片原始字节。</summary>
    public byte[]? SealBytes { get; set; }

    /// <summary>印章图片类型（ofd / png / gif / svg 等）。</summary>
    public string? SealType { get; set; }
}
