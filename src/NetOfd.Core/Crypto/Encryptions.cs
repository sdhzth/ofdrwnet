using System.Xml.Linq;

namespace NetOfd.Core.Crypto;

/// <summary>
/// 解密入口文件（GM/T 0099-2020）。
/// 文件以 Encryptions 为根节点，放置于 OFD 文件根目录（与 OFD.xml 同级），
/// 可包含多个加密信息（EncryptInfo）节点以支持多重加密。
/// 对应 Java: org.ofdrw.core.crypto.encryt.Encryptions
/// </summary>
public class Encryptions : OFDElement
{
    public Encryptions() : base("Encryptions") { }
    public Encryptions(XElement element) : base(element) { }

    /// <summary>【必选】增加加密描述信息</summary>
    public Encryptions AddEncryptInfo(CT_EncryptInfo encryptInfo)
    {
        if (encryptInfo is not null) AddOFDEntity(encryptInfo);
        return this;
    }

    public System.Collections.Generic.List<CT_EncryptInfo> GetEncryptInfos() =>
        GetChildElements("EncryptInfo").Select(e => new CT_EncryptInfo(e)).ToList();

    /// <summary>返回当前容器内最大的加密操作标识数字（无法解析则返回 0）</summary>
    public int MaxID()
    {
        int res = 0;
        foreach (var info in GetEncryptInfos())
        {
            if (int.TryParse(info.GetID(), out int i) && i > res) res = i;
        }
        return res;
    }
}
