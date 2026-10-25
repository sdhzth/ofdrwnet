using NetOfd.Core.BasicType;

namespace NetOfd.Reader;

/// <summary>
/// DeltaX / DeltaY 工具：展开 OFD 的 <c>g n v</c> 压缩语法，并按文本长度补齐。
/// <para>对应 Java: <c>org.ofdrw.reader.DeltaTool</c>。</para>
/// <para>GB/T 33190-2016 中 <c>DeltaX</c>/<c>DeltaY</c> 为 <c>ST_Array</c>，支持
/// <c>g n v</c> 表示「重复 n 次 v」的压缩写法（n 为整数，v 为数值），例如
/// <c>DeltaX="g 4 5.5"</c> 等价于 <c>"5.5 5.5 5.5 5.5"</c>。</para>
/// <para>展开后若长度小于文本字数（且非空），用最后一个值补齐到文本长度——
/// 使调用方可以 <c>坐标 += delta[i]</c> 逐字推进而无需处理越界。</para>
/// </summary>
public static class DeltaTool
{
    /// <summary>
    /// 展开并补齐一组 Delta 数据。
    /// </summary>
    /// <param name="delta">OFD 的 DeltaX/DeltaY 属性值（可为 null / 空白 / 含 <c>g</c> 压缩）</param>
    /// <param name="contentLength">文本（TextCode 内容）长度</param>
    /// <returns>展开并补齐后的坐标偏移列表；源为空时返回空列表（补齐仅当列表非空时进行）</returns>
    public static List<double> GetDelta(string? delta, int contentLength)
    {
        var list = new List<double>();
        if (!string.IsNullOrWhiteSpace(delta))
        {
            // Java: ST_Array 以空白分隔；此处额外兼容逗号分隔（NetOfd 既有解析行为）
            var tokens = delta!.Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] == "g")
                {
                    // g n v：重复 n 次 v（i 前进到 v 之后）
                    if (i + 2 >= tokens.Length) break; // 残缺语法，忽略（防御）
                    int n = int.Parse(tokens[i + 1]);
                    double v = STBase.ToDouble(tokens[i + 2]);
                    for (int j = 0; j < n; j++) list.Add(v);
                    i += 2;
                }
                else
                {
                    list.Add(STBase.ToDouble(tokens[i]));
                }
            }
        }

        int deltaSize = list.Count;
        if (deltaSize < contentLength && deltaSize > 0)
        {
            double lastDelta = list[deltaSize - 1];
            for (int i = 0; i < contentLength - deltaSize; i++)
                list.Add(lastDelta);
        }
        return list;
    }
}
