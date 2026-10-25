using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 未加密的明密文映射表文件数据结构（GM/T 0099-2020 附录 C 图 C.3）。
/// 对应 Java: org.ofdrw.core.crypto.encryt.EncryptEntries
/// </summary>
public class EncryptEntries : OFDElement
{
    public EncryptEntries() : base("EncryptEntries") { }
    public EncryptEntries(XElement element) : base(element) { }

    /// <summary>【必选 属性】加密操作标识，应与解密入口描述中的一致</summary>
    public EncryptEntries SetID(string id)
    {
        if (string.IsNullOrEmpty(id)) throw new System.ArgumentException("id 为空");
        SetAttr("ID", id);
        return this;
    }

    public string? GetID() => GetAttr("ID");

    /// <summary>【必选】增加明密文对应关系</summary>
    public EncryptEntries AddEncryptEntry(EncryptEntry encryptEntry)
    {
        if (encryptEntry is not null) AddOFDEntity(encryptEntry);
        return this;
    }

    public EncryptEntries AddEncryptEntry(string path, string ePath, string? decryptSeedLoc = null) =>
        AddEncryptEntry(new EncryptEntry(path, ePath, decryptSeedLoc));

    public System.Collections.Generic.List<EncryptEntry> GetEncryptEntries() =>
        GetChildElements("EncryptEntry").Select(e => new EncryptEntry(e)).ToList();
}
