namespace NetOfd.Reader;

/// <summary>
/// OFD 文档结构或格式错误异常。
/// 当 OFD 包结构不符合规范、缺少必要文件或 XML 解析失败时抛出。
/// 对应 Java: org.ofdrw.reader.BadOFDException
/// </summary>
public class BadOFDException : Exception
{
    public BadOFDException() { }

    public BadOFDException(string message) : base(message) { }

    public BadOFDException(string message, Exception innerException)
        : base(message, innerException) { }
}