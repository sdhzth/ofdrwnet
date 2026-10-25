namespace NetOfd.Pkg.Container;

/// <summary>
/// 签名容器（Doc_N/Signs 目录）。
/// <para>
/// 目录下含签名列表文件 Signatures.xml 与若干签名目录 Sign_N（N 从 0 起算）。
/// </para>
/// 对应 Java: org.ofdrw.pkg.container.SignsDir
/// </summary>
public class SignsDir : VirtualContainer
{
    /// <summary>签名列表文件名称</summary>
    public const string SignaturesFileName = "Signatures.xml";

    /// <summary>签名 Index 最大值 + 1，即下一个新签名目录将使用的序号</summary>
    private int _maxSignIndex;

    public SignsDir(string name = "Signs", string? physicalPath = null) : base(name, physicalPath)
    {
        InitContainer();
    }

    /// <summary>
    /// 扫描容器中已存在的 Sign_N 目录，初始化签名数量计数器。
    /// <para>只有真正存在于磁盘（PhysicalPath）的目录才纳入统计，
    /// 纯内存模式下从 0 开始。</para>
    /// </summary>
    private void InitContainer()
    {
        if (PhysicalPath is null || !Directory.Exists(PhysicalPath)) return;

        foreach (var dir in Directory.EnumerateDirectories(PhysicalPath))
        {
            string dirName = Path.GetFileName(dir);
            if (!dirName.StartsWith(SignDir.SignContainerPrefix, StringComparison.OrdinalIgnoreCase))
                continue;

            string numb = dirName[SignDir.SignContainerPrefix.Length..];
            if (int.TryParse(numb, out int num) && _maxSignIndex <= num)
                _maxSignIndex = num + 1;
        }
    }

    /// <summary>
    /// 子容器工厂：Sign_N 目录一律构造为 <see cref="SignDir"/>。
    /// <para>netofd 用「重写工厂」替代 Java 的 <c>obtainContainer(name, SignDir::new)</c> 方法引用。</para>
    /// </summary>
    protected override VirtualContainer CreateChildContainer(string name, string? physicalPath)
    {
        if (name.StartsWith(SignDir.SignContainerPrefix, StringComparison.OrdinalIgnoreCase))
            return new SignDir(name, physicalPath);
        return base.CreateChildContainer(name, physicalPath);
    }

    // ────────────────────────────────────────────────────────
    //  签名列表文件
    // ────────────────────────────────────────────────────────

    /// <summary>获取签名列表文件（不存在返回 null）。</summary>
    public NetOfd.Core.Signatures.Signatures? GetSignatures()
    {
        var el = GetFile(SignaturesFileName);
        return el is null ? null : new NetOfd.Core.Signatures.Signatures(el);
    }

    /// <summary>
    /// 设置签名列表文件（不存在时自动创建）。
    /// 对应 Java: <c>SignsDir.setSignatures</c> / <c>getSignatures</c>（后者不存在即抛异常）。
    /// </summary>
    public NetOfd.Core.Signatures.Signatures ObtainSignatures()
    {
        var sig = GetSignatures();
        if (sig is not null) return sig;
        sig = new NetOfd.Core.Signatures.Signatures();
        SetSignatures(sig);
        return sig;
    }

    /// <summary>设置签名列表文件。</summary>
    public SignsDir SetSignatures(NetOfd.Core.Signatures.Signatures signatures)
    {
        PutFile(SignaturesFileName, signatures.Element);
        return this;
    }

    // ────────────────────────────────────────────────────────
    //  签名目录
    // ────────────────────────────────────────────────────────

    /// <summary>创建一个新的签名目录（序号自增，形如 Sign_0、Sign_1 …）。</summary>
    public SignDir NewSignDir()
    {
        string name = SignDir.SignContainerPrefix + _maxSignIndex;
        _maxSignIndex++;
        return (SignDir)ObtainContainer(name);
    }

    /// <summary>
    /// 按序号获取签名目录（不存在返回 null）。
    /// <para>
    /// <b>与 Java 的差异（有意修正）</b>：Java 版要求 <c>index &gt; 0</c>，
    /// 但 <c>newSignDir()</c> 生成的第一个目录是 <c>Sign_0</c> —— 两者自相矛盾。
    /// GB/T 33190-2016 中签名目录序号自 0 起算，故此处允许 <c>index &gt;= 0</c>。
    /// </para>
    /// </summary>
    public SignDir? GetByIndex(int index)
    {
        if (index < 0) return null;
        return GetSignDir(SignDir.SignContainerPrefix + index);
    }

    /// <summary>按容器名获取签名目录（不存在返回 null）。</summary>
    public SignDir? GetSignDir(string containerName)
        => GetContainer(containerName) as SignDir;
}
