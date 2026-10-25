using System;
using NetOfd.Pkg.Container;

namespace NetOfd.Layout.Areaholder;

/// <summary>
/// 区域占位区块文件操作（对应 Java: org.ofdrw.layout.areaholder.AreaHolderBlocksProcess）。
/// <para>纯静态操作 Doc 目录内的 AreaHolderBlocks.xml：obtain/create/get/exist/find/delete。
/// Java 方法名 obtian 为拼写错误，NetOfd 规范为 <see cref="Obtain"/>。</para>
/// </summary>
public static class AreaHolderBlocksProcess
{
    /// <summary>
    /// 获取或创建区域占位区块列表：文件已存在则读取，否则新建并写入 Doc 目录。
    /// </summary>
    /// <exception cref="ArgumentException">docDir 为 null 时抛出</exception>
    public static AreaHolderBlocks Obtain(DocDir? docDir)
    {
        if (docDir is null) throw new ArgumentException("文档目录（docDir）为空");
        return Exists(docDir) ? Get(docDir) : Create(docDir);
    }

    /// <summary>新建区域占位区块列表并写入 Doc 目录。</summary>
    /// <exception cref="ArgumentException">docDir 为 null 时抛出</exception>
    public static AreaHolderBlocks Create(DocDir? docDir)
    {
        if (docDir is null) throw new ArgumentException("文档目录（docDir）为空");
        var areaHolderBlocks = new AreaHolderBlocks();
        docDir.PutFile(AreaHolderBlocks.AreaHolderBlocksFile, areaHolderBlocks.Element);
        return areaHolderBlocks;
    }

    /// <summary>读取 Doc 目录中的区域占位区块列表。</summary>
    /// <exception cref="InvalidOperationException">文件不存在时抛出（应先以 <see cref="Exists"/> 判断）</exception>
    public static AreaHolderBlocks Get(DocDir docDir)
    {
        var el = docDir.GetFile(AreaHolderBlocks.AreaHolderBlocksFile)
                 ?? throw new InvalidOperationException(
                     $"区域占位区块列表文件不存在: {AreaHolderBlocks.AreaHolderBlocksFile}");
        return new AreaHolderBlocks(el);
    }

    /// <summary>检查 Doc 目录中是否存在区域占位区块列表文件。</summary>
    public static bool Exists(DocDir? docDir)
    {
        if (docDir is null) return false;
        return docDir.FileExists(AreaHolderBlocks.AreaHolderBlocksFile);
    }

    /// <summary>按区域名称查找区块记录；未找到返回 null。</summary>
    public static CT_AreaHolderBlock? Find(AreaHolderBlocks? blocks, string? areaName)
    {
        if (blocks is null || areaName is null) return null;
        foreach (CT_AreaHolderBlock block in blocks.GetAreaHolderBlocks())
        {
            if (areaName == block.GetAreaName()) return block;
        }
        return null;
    }

    /// <summary>删除 Doc 目录中的区域占位区块列表文件（不存在则静默返回）。</summary>
    public static void Delete(DocDir? docDir)
    {
        if (docDir is null) return;
        if (Exists(docDir)) docDir.DeleteFile(AreaHolderBlocks.AreaHolderBlocksFile);
    }
}
