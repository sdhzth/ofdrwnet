namespace NetOfd.Core.Resources;

/// <summary>
/// 多媒体格式（7.9 资源 图 21 表 19）。
/// 对应 Java: org.ofdrw.core.basicStructure.res.MediaType
/// </summary>
public enum MediaType
{
    /// <summary>位图图像</summary>
    Image,

    /// <summary>音频</summary>
    Audio,

    /// <summary>视频</summary>
    Video
}

/// <summary>
/// <see cref="MediaType"/> 解析扩展（大小写不敏感）。
/// 对应 Java: MediaType#getInstance
/// </summary>
public static class MediaTypeParser
{
    /// <summary>解析媒体格式字符串；无法识别时抛 <see cref="ArgumentException"/>。</summary>
    public static MediaType Parse(string? str)
    {
        var s = (str ?? string.Empty).Trim();
        if (s.Equals("Image", StringComparison.OrdinalIgnoreCase)) return MediaType.Image;
        if (s.Equals("Audio", StringComparison.OrdinalIgnoreCase)) return MediaType.Audio;
        if (s.Equals("Video", StringComparison.OrdinalIgnoreCase)) return MediaType.Video;
        throw new ArgumentException($"多媒体格式不支持：{str}");
    }

    /// <summary>尝试解析；失败返回 false。</summary>
    public static bool TryParse(string? str, out MediaType result)
    {
        try
        {
            result = Parse(str);
            return true;
        }
        catch (ArgumentException)
        {
            result = default;
            return false;
        }
    }
}
