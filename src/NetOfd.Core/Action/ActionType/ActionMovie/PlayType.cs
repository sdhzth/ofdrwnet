namespace NetOfd.Core.Action.ActionType.ActionMovie;

/// <summary>
/// 放映参数属性（表 59 放映参数属性）。
/// 对应 Java: org.ofdrw.core.action.actionType.actionMovie.PlayType
/// </summary>
public enum PlayType
{
    /// <summary>播放</summary>
    Play,
    /// <summary>停止</summary>
    Stop,
    /// <summary>暂停</summary>
    Pause,
    /// <summary>继续</summary>
    Resume
}

/// <summary>PlayType 解析扩展（对齐 Java PlayType.getInstance）。</summary>
public static class PlayTypeExtensions
{
    /// <summary>根据字符串解析放映参数；未知值抛 ArgumentException（对齐 Java）。</summary>
    public static PlayType ToPlayType(this string? type)
    {
        if (Enum.TryParse<PlayType>(type?.Trim(), ignoreCase: true, out var pt))
            return pt;
        throw new ArgumentException($"未知的放映参数： {type}");
    }
}
