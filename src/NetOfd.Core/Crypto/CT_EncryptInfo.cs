using System;
using System.Globalization;
using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 加密描述信息（GM/T 0099-2020 附录 C.3 表 C.2）。
/// 多重加密形成多个加密操作记录（图 C.2 中的 EncryptInfo 节点可出现多次）。
/// 对应 Java: org.ofdrw.core.crypto.encryt.CT_EncryptInfo
/// </summary>
public class CT_EncryptInfo : OFDElement
{
    public CT_EncryptInfo() : base("EncryptInfo") { }
    public CT_EncryptInfo(XElement element) : base(element) { }

    /// <summary>【必选 属性】加密操作标识</summary>
    public CT_EncryptInfo SetID(string id)
    {
        if (string.IsNullOrEmpty(id)) throw new ArgumentException("id 为空");
        SetAttr("ID", id);
        return this;
    }

    public string? GetID() => GetAttr("ID");

    /// <summary>【可选 属性】上一次加密操作标识</summary>
    public CT_EncryptInfo SetRelative(string? id)
    {
        SetAttr("Relative", id);
        return this;
    }

    public string? GetRelative() => GetAttr("Relative");

    /// <summary>【必选】加密组件的相关信息</summary>
    public CT_EncryptInfo SetProvider(Provider provider)
    {
        if (provider is null) throw new ArgumentNullException(nameof(provider));
        SetOFDEntity(provider);
        return this;
    }

    public Provider GetProvider()
    {
        var e = GetChildElement("Provider");
        if (e is null) throw new InvalidOperationException("Provider 为空");
        return new Provider(e);
    }

    /// <summary>【必选】文档加密相关内容的描述（文档加密类型或范围）</summary>
    public CT_EncryptInfo SetEncryptScope(string encryptScope)
    {
        if (encryptScope is null) throw new ArgumentNullException(nameof(encryptScope));
        SetChildText("EncryptScope", encryptScope);
        return this;
    }

    public string GetEncryptScope()
    {
        var e = GetChildElement("EncryptScope");
        if (e is null) throw new InvalidOperationException("EncryptScope 为空");
        return e.Value;
    }

    /// <summary>【可选】加密时间</summary>
    public CT_EncryptInfo SetEncryptDate(DateTime? encryptDate)
    {
        if (encryptDate is null) GetChildElement("EncryptDate")?.Remove();
        else SetChildText("EncryptDate", encryptDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
        return this;
    }

    public string? GetEncryptDateStr() => GetChildText("EncryptDate");

    public DateTime? GetEncryptDate()
    {
        var s = GetEncryptDateStr();
        return string.IsNullOrWhiteSpace(s)
            ? null
            : DateTime.ParseExact(s, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
    }

    /// <summary>【必选】指向包内的二进制密钥描述文件</summary>
    public CT_EncryptInfo SetDecryptSeedLoc(string decryptSeedLoc)
    {
        if (decryptSeedLoc is null) throw new ArgumentNullException(nameof(decryptSeedLoc));
        SetChildText("DecryptSeedLoc", decryptSeedLoc);
        return this;
    }

    public string? GetDecryptSeedLoc() => GetChildText("DecryptSeedLoc");

    /// <summary>【必选】明密文映射表或其加密后的文件存储的路径</summary>
    public CT_EncryptInfo SetEntriesMapLoc(string entriesMapLoc)
    {
        if (entriesMapLoc is null) throw new ArgumentNullException(nameof(entriesMapLoc));
        SetChildText("EntriesMapLoc", entriesMapLoc);
        return this;
    }

    public string? GetEntriesMapLoc() => GetChildText("EntriesMapLoc");
}
