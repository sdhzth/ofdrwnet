using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;

namespace NetOfd.Sign;

/// <summary>
/// 扩展数字签名容器接口。
/// <para>提供摘要算法、签名算法 OID 与流式签名能力，供自定义签名实现扩展。</para>
/// 参考 ofdrw-sign 的 ExtendSignatureContainer。
/// </summary>
public interface IExtendSignatureContainer
{
    /// <summary>提供文件的摘要算法功能（默认 SM3，每次调用返回新的实例）。</summary>
    IDigest GetDigestFnc() => new SM3Digest();

    /// <summary>签名方法 OID（默认 SM2withSM3：1.2.156.10197.1.501）。</summary>
    string GetSignAlgOid() => GMObjectIdentifiers.sm2sign_with_sm3.Id;

    /// <summary>获取签名节点类型："Sign"（数字签名）或 "Seal"（电子签章）。</summary>
    string SignType { get; }

    /// <summary>对待签名数据流计算签名值（实现方不应关闭流）。</summary>
    /// <param name="inData">待签名数据流（签名描述文件内容）。</param>
    /// <param name="propertyInfo">签章属性信息（签名文件包内路径）。</param>
    /// <returns>签名或签章结果。</returns>
    byte[] Sign(Stream inData, string propertyInfo);
}

/// <summary>
/// 签名容器：在扩展签名容器基础上提供内存字节签名与电子印章。
/// <para>内置 SES_V1/V4/V5、GB/T 35275 等容器均实现此接口；也可自行扩展。</para>
/// </summary>
public interface ISignContainer : IExtendSignatureContainer
{
    /// <summary>签名值中携带的电子印章 DER（数字签名无印章时返回 null）。</summary>
    byte[]? Seal { get; }

    /// <summary>对被签内容计算签名值。</summary>
    /// <param name="data">被签内容（签名描述文件字节）。</param>
    /// <param name="propertyInfo">签章属性信息（签名文件包内路径）。</param>
    /// <returns>签名值（SignedValue 文件内容）。</returns>
    byte[] Sign(byte[] data, string propertyInfo);

    /// <summary>默认流式签名实现：读入全部字节后调用字节版本签名。</summary>
    byte[] IExtendSignatureContainer.Sign(Stream inData, string propertyInfo)
    {
        using var ms = new MemoryStream();
        inData.CopyTo(ms);
        return Sign(ms.ToArray(), propertyInfo);
    }
}