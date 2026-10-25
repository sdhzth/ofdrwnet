using NetOfd.Core.BasicType;
using NetOfd.Core.Signatures;
using Xunit;

namespace NetOfd.Tests.Core;

/// <summary>
/// 覆盖 GB/T 33190-2016 §18.2 签名类型链：
/// <see cref="SignedInfo"/> / <see cref="Provider"/> / <see cref="Parameters"/> /
/// <see cref="References"/> / <see cref="Reference"/> / <see cref="Seal"/> /
/// <see cref="StampAnnot"/> / <see cref="SigType"/> / <see cref="CheckMethod"/>。
/// </summary>
public class SignedInfoTest
{
    // ────────────────────────────────────────────────────────
    //  枚举互转
    // ────────────────────────────────────────────────────────

    [Theory]
    [InlineData(null, SigType.Seal)]
    [InlineData("", SigType.Seal)]
    [InlineData("Seal", SigType.Seal)]
    [InlineData("seal", SigType.Seal)]      // 大小写不敏感
    [InlineData("Sign", SigType.Sign)]
    [InlineData("SIGN", SigType.Sign)]
    public void SigTypeUtil_Parse(string? input, SigType expected)
        => Assert.Equal(expected, SigTypeUtil.Parse(input));

    [Fact]
    public void SigTypeUtil_RejectsUnknown()
    {
        Assert.Throws<ArgumentException>(() => SigTypeUtil.Parse("Unknown"));
        Assert.Equal("Seal", SigTypeUtil.ToXml(SigType.Seal));
    }

    [Theory]
    [InlineData("MD5", CheckMethod.MD5)]
    [InlineData("sha1", CheckMethod.SHA1)]
    [InlineData("SHA256", CheckMethod.SHA256)]
    [InlineData("sm3", CheckMethod.SM3)]    // 国密，大小写不敏感
    public void CheckMethodUtil_Parse(string input, CheckMethod expected)
        => Assert.Equal(expected, CheckMethodUtil.Parse(input));

    [Fact]
    public void CheckMethodUtil_DefaultsToMd5AndRejectsUnknown()
    {
        Assert.Equal(CheckMethod.MD5, CheckMethodUtil.Parse(null));
        Assert.Equal(CheckMethod.MD5, CheckMethodUtil.Parse("  "));
        Assert.Throws<ArgumentException>(() => CheckMethodUtil.Parse("CRC32"));
        Assert.Equal("SM3", CheckMethodUtil.ToXml(CheckMethod.SM3));
    }

    // ────────────────────────────────────────────────────────
    //  Reference / References
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Reference_FileRefAndCheckValueRoundTrip()
    {
        var digest = new byte[] { 0x01, 0x02, 0x03, 0xFF };
        var r = new Reference(new STLoc("/Doc_0/Pages/Page_0/Content.xml"), digest);

        Assert.Equal("/Doc_0/Pages/Page_0/Content.xml", r.FileRef!.Value);
        Assert.Equal(digest, r.CheckValue);

        // CheckValue 在 XML 中是 Base64 文本
        var cv = r.Element.Element(r.Element.Name.Namespace + "CheckValue");
        Assert.Equal(Convert.ToBase64String(digest), cv?.Value);
    }

    [Fact]
    public void Reference_CheckValueMissingReturnsNullAndNullRemoves()
    {
        var r = new Reference();
        Assert.Null(r.CheckValue);

        r.CheckValue = new byte[] { 9, 9 };
        Assert.NotNull(r.CheckValue);

        r.CheckValue = null;
        Assert.Null(r.CheckValue);
    }

    [Fact]
    public void References_AddAndHasFile()
    {
        var refs = new References { CheckMethod = "SM3" };
        refs.AddReference(new Reference(new STLoc("/Doc_0/Pages/Page_0/Content.xml"), new byte[] { 1 }))
            .AddReference(new Reference(new STLoc("/OFD.xml"), new byte[] { 2 }));

        Assert.Equal(2, refs.GetReferences().Count);
        Assert.True(refs.HasFile("/OFD.xml"));
        Assert.False(refs.HasFile("/Doc_0/Nope.xml"));
        Assert.False(refs.HasFile("   "));

        // 强类型视图（属性遮蔽陷阱的回归点）
        Assert.Equal(CheckMethod.SM3, refs.CheckMethodKind);
    }

    [Fact]
    public void References_CheckMethodKindNullWhenAbsent()
    {
        var refs = new References();
        Assert.Null(refs.CheckMethodKind);

        refs.CheckMethodKind = CheckMethod.SHA256;
        Assert.Equal("SHA256", refs.CheckMethod);
    }

    // ────────────────────────────────────────────────────────
    //  Seal / StampAnnot
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Seal_BaseLocIsChildElementNotAttribute()
    {
        var seal = new Seal(new STLoc("/Signs/Sign_0/Seal.esl"));
        seal.ImageLoc = new STLoc("/Signs/Sign_0/Seal.png");

        Assert.Equal("/Signs/Sign_0/Seal.esl", seal.BaseLoc!.Value);
        Assert.Equal("/Signs/Sign_0/Seal.png", seal.ImageLoc!.Value);

        // BaseLoc 是子元素而非属性（与 StampAnnot 的 Boundary 属性写法区分）
        Assert.Null(seal.Element.Attribute("BaseLoc"));
        Assert.NotNull(seal.Element.Element(seal.Element.Name.Namespace + "BaseLoc"));

        seal.ImageLoc = null;
        Assert.Null(seal.ImageLoc);
    }

    [Fact]
    public void StampAnnot_AttributesRoundTrip()
    {
        var sa = new StampAnnot
        {
            ID = "s001",
            PageRef = new STRefID(7),
            Boundary = STBox.Parse("10 20 30 40"),
            Clip = STBox.Parse("1 2 3 4"),
        };

        Assert.Equal("s001", sa.ID);
        Assert.Equal(7L, sa.PageRef!.Value);
        Assert.Equal(new STBox(10, 20, 30, 40), sa.Boundary);
        Assert.Equal(new STBox(1, 2, 3, 4), sa.Clip);

        // 反向：从 XML 重新解析
        var reparsed = new StampAnnot(sa.Element);
        Assert.Equal("s001", reparsed.ID);
        Assert.Equal(7L, reparsed.PageRef!.Value);
    }

    // ────────────────────────────────────────────────────────
    //  Provider / Parameter / Parameters
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Provider_AttributesAndExtendData()
    {
        var p = new Provider("NetOfd.Sign")
        {
            Version = "1.0.25",
            Company = "Jimo3rd",
            ProtocolVer = "1.0",
            ExtendData = new byte[] { 0xDE, 0xAD },
        };

        Assert.Equal("NetOfd.Sign", p.ProviderName);
        Assert.Equal("1.0.25", p.Version);
        Assert.Equal("Jimo3rd", p.Company);
        Assert.Equal("1.0", p.ProtocolVer);
        Assert.Equal(new byte[] { 0xDE, 0xAD }, p.ExtendData);

        p.ExtendData = null;
        Assert.Null(p.ExtendData);
    }

    [Fact]
    public void Parameter_SettingValueKeepsAttributes()
    {
        var p = new Parameter("SignTime", "string", "2026-09-17T00:00:00Z");

        Assert.Equal("SignTime", p.Name);
        Assert.Equal("string", p.Type);
        Assert.Equal("2026-09-17T00:00:00Z", p.Value);

        // 回归点：早期 OFDElement.SetText() 会连属性一起删（增量 Q 的坑）
        p.Value = "changed";
        Assert.Equal("SignTime", p.Name);
        Assert.Equal("string", p.Type);
        Assert.Equal("changed", p.Value);
    }

    [Fact]
    public void Parameters_AddGetRemove()
    {
        var ps = new Parameters();
        ps.AddParameter("A", "1")
          .AddParameter("B", "int", "2");

        Assert.Equal(2, ps.GetParameters().Count);
        Assert.Equal("1", ps.GetParameter("A")!.Value);
        Assert.Equal("int", ps.GetParameter("B")!.Type);

        // 同名覆盖（后写覆盖，先删旧的）
        ps.AddParameter("A", "1-modified");
        Assert.Equal(2, ps.GetParameters().Count);
        Assert.Equal("1-modified", ps.GetParameter("A")!.Value);

        Assert.Equal("1-modified", ps.RemoveParameter("A")!.Value);
        Assert.Null(ps.GetParameter("A"));
        Assert.Single(ps.GetParameters());
    }

    // ────────────────────────────────────────────────────────
    //  SignedInfo 组装
    // ────────────────────────────────────────────────────────

    [Fact]
    public void SignedInfo_FullAssemblyAndReread()
    {
        var info = new SignedInfo
        {
            Provider = new Provider("NetOfd.Sign") { Version = "1.0.25" },
            SignatureMethod = "1.2.156.10197.1.501",
            SignatureDateTime = "2026-09-17T00:00:00Z",
            References = BuildRefs(),
            Seal = new Seal(new STLoc("/Signs/Sign_0/Seal.esl")),
        };
        info.AddStampAnnot(new StampAnnot { ID = "s001", PageRef = new STRefID(3) });
        info.Parameters = new Parameters().AddParameter("Ext", "v");

        // 从同一棵 XML 树重新读回，验证各节点都真正落盘
        var reread = new SignedInfo(info.Element);
        Assert.Equal("NetOfd.Sign", reread.Provider!.ProviderName);
        Assert.Equal("1.0.25", reread.Provider!.Version);
        Assert.Equal("1.2.156.10197.1.501", reread.SignatureMethod);
        Assert.Equal("2026-09-17T00:00:00Z", reread.SignatureDateTime);
        Assert.Equal(2, reread.References!.GetReferences().Count);
        Assert.Equal("/Signs/Sign_0/Seal.esl", reread.Seal!.BaseLoc!.Value);
        Assert.Single(reread.GetStampAnnots());
        Assert.Equal("s001", reread.GetStampAnnots()[0].ID);
        Assert.Equal("v", reread.Parameters!.GetParameter("Ext")!.Value);
    }

    [Fact]
    public void SignedInfo_NullRemovesSubtree()
    {
        var info = new SignedInfo
        {
            Seal = new Seal(new STLoc("/Seal.esl")),
            References = BuildRefs(),
        };
        Assert.NotNull(info.Seal);

        info.Seal = null;
        Assert.Null(info.Seal);

        info.References = null;
        Assert.Null(info.References);   // 缺失返回 null（Java 抛异常）
    }

    // ────────────────────────────────────────────────────────
    //  Signature 桥接与注册项属性
    // ────────────────────────────────────────────────────────

    [Fact]
    public void Signature_SignedInfoStrongTypedBridge()
    {
        var sig = new Signature();
        Assert.Null(sig.GetSignedInfo());

        var info = new SignedInfo { Provider = new Provider("P") };
        sig.SetSignedInfo(info);

        var back = sig.GetSignedInfo();
        Assert.NotNull(back);
        Assert.Equal("P", back!.Provider!.ProviderName);

        sig.SetSignedInfo(null);
        Assert.Null(sig.GetSignedInfo());
    }

    [Fact]
    public void Signature_RegistryAttributesAndDefaultSigType()
    {
        var sig = new Signature
        {
            ID = "s001",
            BaseLoc = new STLoc("/Signs/Sign_0/Signature.xml"),
        };

        // Type 未设置时按 Java 缺省语义返回 Seal
        Assert.Equal(SigType.Seal, sig.Type);

        sig.Type = SigType.Sign;
        Assert.Equal("Sign", sig.Element.Attribute("Type")?.Value);
        Assert.Equal(SigType.Sign, sig.Type);

        Assert.Equal("s001", sig.ID);
        Assert.Equal("/Signs/Sign_0/Signature.xml", sig.BaseLoc!.Value);
    }

    // ── 辅助 ────────────────────────────────────────────────
    private static References BuildRefs()
    {
        var refs = new References { CheckMethod = "SM3" };
        refs.AddReference(new Reference(new STLoc("/OFD.xml"), new byte[] { 1 }))
            .AddReference(new Reference(new STLoc("/Doc_0/Pages/Page_0/Content.xml"), new byte[] { 2 }));
        return refs;
    }
}
