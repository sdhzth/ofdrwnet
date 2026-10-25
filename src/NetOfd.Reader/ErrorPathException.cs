using System;

namespace NetOfd.Reader;

/// <summary>
/// 错误路径异常：OFD 包内资源定位失败（路径不存在、越界或非法）时抛出。
/// 对应 Java: org.ofdrw.reader.ErrorPathException
/// </summary>
public class ErrorPathException : Exception
{
    public ErrorPathException() { }

    public ErrorPathException(string message) : base(message) { }

    public ErrorPathException(string message, Exception innerException) : base(message, innerException) { }

    public ErrorPathException(Exception innerException) : base(innerException?.Message, innerException) { }
}
