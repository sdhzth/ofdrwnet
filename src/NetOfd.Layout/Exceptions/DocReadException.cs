using System.IO;

namespace NetOfd.Layout.Exceptions;

/// <summary>
/// OFD 文件解析异常。
/// <para>
/// 用于标识"文档本身有问题/不受支持"这一类失败：调用方可以单独捕获它，
/// 向上给出"文件损坏或格式不支持"的提示，而不必笼统处理所有 <see cref="IOException"/>。
/// </para>
/// 对应 Java: org.ofdrw.layout.exception.DocReadException
/// </summary>
/// <remarks>
/// <b>命名空间为何不是 <c>Exception</c></b>：Java 包名是 <c>exception</c>，但 C# 里
/// <c>Exception</c> 是极常用的类型名——若命名空间也叫 <c>Exception</c>，
/// 文件内再写 <c>Exception inner</c> 会触发 CS0118（把命名空间当作类型使用）。
/// 故按 .NET 惯例改用复数 <c>Exceptions</c>。
/// </remarks>
public class DocReadException : IOException
{
    public DocReadException() { }

    public DocReadException(string message) : base(message) { }

    public DocReadException(string message, System.Exception inner) : base(message, inner) { }
}
