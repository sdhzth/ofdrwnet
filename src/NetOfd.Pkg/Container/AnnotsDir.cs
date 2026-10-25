using NetOfd.Core.Annotation;

namespace NetOfd.Pkg.Container;

/// <summary>
/// 注释容器（Doc_N/Annots 目录）。
/// <para>
/// 目录下含注释列表文件 Annotations.xml 与若干页面注释目录 Page_N。
/// 注释是板式文档形成后附加的图文信息，页面内容与注释内容分文件描述。
/// </para>
/// <para>GMT 0099 / OFD 2.0 扩展结构</para>
/// 对应 Java: org.ofdrw.pkg.container.AnnotsDir
/// </summary>
public class AnnotsDir : VirtualContainer
{
    public AnnotsDir(string name = "Annots", string? physicalPath = null) : base(name, physicalPath) { }

    /// <summary>
    /// 子容器工厂：Page_N 目录一律构造为 <see cref="PageDir"/>。
    /// </summary>
    protected override VirtualContainer CreateChildContainer(string name, string? physicalPath)
    {
        if (name.StartsWith("Page_", StringComparison.OrdinalIgnoreCase))
            return new PageDir(name, physicalPath);
        return base.CreateChildContainer(name, physicalPath);
    }

    // ────────────────────────────────────────────────────────
    //  注释列表文件
    // ────────────────────────────────────────────────────────

    /// <summary>设置注释列表对象。</summary>
    public AnnotsDir SetAnnotations(Annotations annotations)
    {
        PutFile(DocDir.ANNOTATIONS_FILE, annotations.Element);
        return this;
    }

    /// <summary>获取注释列表对象（不存在返回 null）。</summary>
    public Annotations? GetAnnotations()
    {
        var el = GetFile(DocDir.ANNOTATIONS_FILE);
        return el is null ? null : new Annotations(el);
    }

    /// <summary>获取注释列表对象（不存在时自动创建）。</summary>
    public Annotations ObtainAnnotations()
    {
        var ann = GetAnnotations();
        if (ann is not null) return ann;
        ann = new Annotations();
        SetAnnotations(ann);
        return ann;
    }

    // ────────────────────────────────────────────────────────
    //  页面注释目录
    // ────────────────────────────────────────────────────────

    /// <summary>按序号获取（不存在则创建）页面注释目录。</summary>
    public PageDir ObtainByIndex(int index)
        => (PageDir)ObtainContainer(PageDir.PageContainerPrefix + index);

    /// <summary>按容器名获取页面注释目录（不存在返回 null）。</summary>
    public PageDir? GetPageDir(string containerName)
        => GetContainer(containerName) as PageDir;
}
