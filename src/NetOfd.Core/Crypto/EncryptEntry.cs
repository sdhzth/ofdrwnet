using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 明密文对应关系（GM/T 0099-2020 附录 C 图 C.3）。
/// 对应 Java: org.ofdrw.core.crypto.encryt.EncryptEntry
/// </summary>
public class EncryptEntry : OFDElement
{
    public EncryptEntry() : base("EncryptEntry") { }
    public EncryptEntry(XElement element) : base(element) { }

    public EncryptEntry(string path, string ePath, string? decryptSeedLoc = null) : base("EncryptEntry")
    {
        SetPathA(path);
        SetEPath(ePath);
        if (decryptSeedLoc is not null) SetDecryptSeedLoc(decryptSeedLoc);
    }

    /// <summary>【必选 属性】加密前包内文件的绝对路径</summary>
    public EncryptEntry SetPathA(string path)
    {
        if (string.IsNullOrEmpty(path)) throw new System.ArgumentException("path 为空");
        SetAttr("Path", path);
        return this;
    }

    public string? GetPathA() => GetAttr("Path");

    /// <summary>【必选 属性】加密后形成的包内密文的绝对路径</summary>
    public EncryptEntry SetEPath(string ePath)
    {
        if (string.IsNullOrEmpty(ePath)) throw new System.ArgumentException("ePath 为空");
        SetAttr("EPath", ePath);
        return this;
    }

    public string? GetEPath() => GetAttr("EPath");

    /// <summary>【可选 属性】此项密文独有的密钥数据文件路径</summary>
    public EncryptEntry SetDecryptSeedLoc(string? decryptSeedLoc)
    {
        SetAttr("DecryptSeedLoc", decryptSeedLoc);
        return this;
    }

    public string? GetDecryptSeedLoc() => GetAttr("DecryptSeedLoc");
}
