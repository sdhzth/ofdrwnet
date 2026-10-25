using Xunit;

namespace NetOfd.Tests;

public class BasicTypesTest
{
    [Fact]
    public void STBox_SerializeDeserialize()
    {
        var box = new NetOfd.Core.BasicType.STBox(10.5, 20.5, 100.0, 200.0);
        var str = box.ToString();
        Assert.Contains("10.5", str);
        Assert.Contains("20.5", str);
        Assert.Contains("100", str);
        Assert.Contains("200", str);
    }

    [Fact]
    public void STPos_Basic()
    {
        var pos = new NetOfd.Core.BasicType.STPos(100.0, 200.0);
        Assert.Equal(100.0, pos.X);
        Assert.Equal(200.0, pos.Y);
    }

    [Fact]
    public void STID_ValidInvalid()
    {
        var validId = new NetOfd.Core.BasicType.STID(123L);
        Assert.True(validId.IsValid);

        var invalidId = new NetOfd.Core.BasicType.STID(0L);
        Assert.False(invalidId.IsValid);
    }

    [Fact]
    public void STLoc_PathOperations()
    {
        var loc = new NetOfd.Core.BasicType.STLoc("Doc_0/Document.xml");
        Assert.Equal("Document.xml", loc.GetFileName());
        Assert.Equal("Doc_0", loc.Parent().Value.TrimEnd('/'));
    }

    [Fact]
    public void STArray_SixElements()
    {
        var arr = new NetOfd.Core.BasicType.STArray(new double[] { 1, 0, 0, 1, 0, 0 });
        Assert.Equal(6, arr.Count);
        Assert.Equal(1.0, arr[0]);
        Assert.Equal(1.0, arr[3]);
    }

    [Fact]
    public void STRefID_Value()
    {
        var refId = new NetOfd.Core.BasicType.STRefID(100L);
        Assert.Equal(100L, refId.Value);
    }
}
