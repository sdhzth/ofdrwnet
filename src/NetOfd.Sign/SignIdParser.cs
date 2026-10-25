using System.Text.RegularExpressions;

namespace NetOfd.Sign;

/// <summary>
/// 签名 ID 解析器。
/// 对应 Java: org.ofdrw.sign.SignIdParser
/// </summary>
public static class SignIdParser
{
    /// <summary>标准推荐 ID 格式："sNNN"，NNN 从 1 开始。</summary>
    public static readonly Regex IdPattern = new(@"s(\d+)", RegexOptions.Compiled);

    /// <summary>
    /// 解析签章 ID 中的数字序号。
    /// <para>支持标准推荐格式 "sNNN"、"sN" 以及纯数字格式。</para>
    /// </summary>
    /// <param name="id">ID 字符串</param>
    /// <returns>ID 序号</returns>
    public static int ParseIndex(string id)
    {
        var m = IdPattern.Match(id);
        if (m.Success)
            return int.Parse(m.Groups[1].Value);
        return int.Parse(id);
    }
}