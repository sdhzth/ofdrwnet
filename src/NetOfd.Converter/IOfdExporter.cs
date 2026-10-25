namespace NetOfd.Converter;

/// <summary>
/// OFD 导出器统一接口。
/// 对应 Java: org.ofdrw.converter.export.OFDExporter
/// <para>
/// 所有导出器都遵循同一约定：构造时打开源 OFD 与目标位置，
/// 调用 <see cref="Export(int[])"/> 导出指定页（不传则全部），
/// 最后 <see cref="IDisposable.Dispose"/> 释放并收尾（如写入 HTML 结尾标签）。
/// </para>
/// </summary>
public interface IOfdExporter : IDisposable
{
    /// <summary>
    /// 导出指定页。
    /// </summary>
    /// <param name="indexes">页码序列（<b>从 0 起</b>）；为空表示导出全部页。越界页码将被忽略。</param>
    /// <exception cref="GeneralConvertException">转换过程发生异常</exception>
    void Export(params int[] indexes);
}

/// <summary>
/// 转换过程通用异常。
/// 对应 Java: org.ofdrw.converter.GeneralConvertException
/// </summary>
public class GeneralConvertException : Exception
{
    public GeneralConvertException(string message) : base(message) { }
    public GeneralConvertException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>导出器共用的页码归一化辅助。</summary>
internal static class ExportPageSelector
{
    /// <summary>
    /// 把用户给定的页码序列归一化为有效页码列表。
    /// 空序列 → 全部页；越界页码被剔除（与 Java 各 Exporter 的 <c>export</c> 行为一致）。
    /// </summary>
    public static List<int> Normalize(int[]? indexes, int pageCount)
    {
        var result = new List<int>();
        if (indexes is null || indexes.Length == 0)
        {
            for (int i = 0; i < pageCount; i++) result.Add(i);
            return result;
        }
        foreach (int i in indexes)
        {
            if (i < 0 || i >= pageCount) continue;
            result.Add(i);
        }
        return result;
    }
}
