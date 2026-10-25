using Xunit;

namespace NetOfd.Tests;

public class VirtualContainerTest
{
    [Fact]
    public void OFDDir_CreateAndDispose()
    {
        using var ofd = new NetOfd.Pkg.Container.OFDDir();
        Assert.NotNull(ofd);
        var doc = ofd.GetDocByIndex(0) ?? ofd.ObtainDoc();
        Assert.NotNull(doc);
    }

    [Fact]
    public void DocDir_PageOperations()
    {
        using var ofd = new NetOfd.Pkg.Container.OFDDir();
        var doc = ofd.GetDocByIndex(0) ?? ofd.ObtainDoc();
        Assert.NotNull(doc);

        var pageCount = doc.GetPagesCount();
        Assert.True(pageCount >= 0);

        var page = doc.GetLatestPage();
        Assert.Null(page);
    }

    [Fact]
    public void DocDir_ResOperations()
    {
        using var ofd = new NetOfd.Pkg.Container.OFDDir();
        var doc = ofd.GetDocByIndex(0) ?? ofd.ObtainDoc();
        var res = doc.ObtainRes();
        Assert.NotNull(res);
    }
}
