// NetOfd - .NET 8 OFD 读写库
// 对应 Java ofdrw-gv 的全局变量模块（GlobalVar）。
// ofdrw-gv 仅包含库版本号一个常量，故直接并入根工程而非单独成模块。
namespace NetOfd;

/// <summary>
/// OFD 库全局变量（对应 Java: org.ofdrw.gv.GlobalVar）。
/// </summary>
public static class GlobalVar
{
    /// <summary>
    /// NetOfd 库版本号。
    /// 本库为 ofdrw 2.4.0（Java）的 .NET 8 移植版。
    /// </summary>
    public const string Version = "1.0.0";
}
