namespace NetOfd.Sign;

/// <summary>
/// OFD 中的签章信息（从签名值文件中提取的印章图片）。
/// <para>对应 Java: org.ofdrw.reader.model.StampAnnotEntity（仅保留图片相关字段）。</para>
/// </summary>
public sealed class StampAnnotEntity
{
    /// <summary>电子签章数据版本（SES v1/v4/v5）。</summary>
    public SesVersion Version { get; }

    /// <summary>印章图片类型（如 PNG、GIF、OFD、SVG 等，来自 eSealInfo.picture.type）。</summary>
    public string ImgType { get; }

    /// <summary>印章图片数据（根据 <see cref="ImgType"/> 判断格式）。</summary>
    public byte[] ImageByte { get; }

    public StampAnnotEntity(SesVersion version, string imgType, byte[] imageByte)
    {
        Version = version;
        ImgType = imgType ?? string.Empty;
        ImageByte = imageByte ?? System.Array.Empty<byte>();
    }

    /// <summary>以只读流形式访问印章图片数据（对齐 Java getImageStream）。</summary>
    public System.IO.Stream GetImageStream() => new System.IO.MemoryStream(ImageByte, writable: false);
}
