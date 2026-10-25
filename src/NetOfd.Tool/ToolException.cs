using System;

namespace NetOfd.Tool;

/// <summary>
/// OFD 文档工具异常。
/// </summary>
public class ToolException : Exception
{
    public ToolException(string message) : base(message) { }

    public ToolException(string message, Exception inner) : base(message, inner) { }
}
