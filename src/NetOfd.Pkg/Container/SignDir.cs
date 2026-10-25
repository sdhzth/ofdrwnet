namespace NetOfd.Pkg.Container;

/// <summary>
/// 签名资源容器（Signs/Sign_N 目录）。
/// <para>
/// 一个签名容器内包含：签名描述文件 Signature.xml、签名值文件 SignedValue.dat，
/// 以及可选的安全电子印章文件 Seal.esl。
/// </para>
/// 对应 Java: org.ofdrw.pkg.container.SignDir
/// </summary>
public class SignDir : VirtualContainer
{
    /// <summary>签名容器名称前缀（目录名形如 <c>Sign_0</c>）</summary>
    public const string SignContainerPrefix = "Sign_";

    /// <summary>电子印章文件名</summary>
    public const string SealFileName = "Seal.esl";

    /// <summary>签名/签章 描述文件名</summary>
    public const string SignatureFileName = "Signature.xml";

    /// <summary>签名值文件名</summary>
    public const string SignedValueFileName = "SignedValue.dat";

    public SignDir(string name, string? physicalPath = null) : base(name, physicalPath) { }

    /// <summary>
    /// 该签名容器的索引原始字符串。
    /// <para>用于兼容非规范命名（如 <c>Sign_001</c>）；规范目录下即 <c>N</c> 本身。</para>
    /// <para>对应 Java: <c>SignDir.getIndexStr()</c>。</para>
    /// </summary>
    public string IndexStr =>
        Name.StartsWith(SignContainerPrefix, StringComparison.OrdinalIgnoreCase)
            ? Name[SignContainerPrefix.Length..]
            : Name;

    /// <summary>
    /// 第几个签名（数字索引）。命名非数字时返回 -1 —— 此处不抛异常，
    /// 以便读取第三方生成的非规范签名目录（Java 版会抛 NumberFormatException）。
    /// </summary>
    public int Index => int.TryParse(IndexStr, out int n) ? n : -1;

    // ────────────────────────────────────────────────────────
    //  签名描述文件
    // ────────────────────────────────────────────────────────

    /// <summary>获取签名描述文件（不存在返回 null；Java 版抛 FileNotFoundException）。</summary>
    public NetOfd.Core.Signatures.Signature? GetSignature()
    {
        var el = GetFile(SignatureFileName);
        return el is null ? null : new NetOfd.Core.Signatures.Signature(el);
    }

    /// <summary>设置签名描述文件。</summary>
    public SignDir SetSignature(NetOfd.Core.Signatures.Signature signature)
    {
        PutFile(SignatureFileName, signature.Element);
        return this;
    }

    // ────────────────────────────────────────────────────────
    //  电子印章 / 签名值（二进制）
    // ────────────────────────────────────────────────────────

    /// <summary>获取电子印章文件字节（不存在返回 null）。</summary>
    public byte[]? GetSeal() => GetRaw(SealFileName);

    /// <summary>设置电子印章文件字节。</summary>
    public SignDir SetSeal(byte[] data)
    {
        AddRaw(SealFileName, data);
        return this;
    }

    /// <summary>获取签名值文件字节（不存在返回 null）。</summary>
    public byte[]? GetSignedValue() => GetRaw(SignedValueFileName);

    /// <summary>设置签名值文件字节。</summary>
    public SignDir SetSignedValue(byte[] data)
    {
        AddRaw(SignedValueFileName, data);
        return this;
    }
}
