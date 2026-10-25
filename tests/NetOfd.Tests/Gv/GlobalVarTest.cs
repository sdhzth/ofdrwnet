using NetOfd;
using Xunit;

namespace NetOfd.Tests.Gv;

/// <summary>
/// 对应 ofdrw-gv 全局变量模块（仅版本号常量）的等价性验证。
/// </summary>
public class GlobalVarTest
{
    [Fact]
    public void Version_IsDefined()
    {
        Assert.False(string.IsNullOrWhiteSpace(GlobalVar.Version));
    }
}
