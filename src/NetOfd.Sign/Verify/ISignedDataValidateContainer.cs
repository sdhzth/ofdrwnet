namespace NetOfd.Sign.Verify;

/// <summary>
/// 签名数据验证容器接口。
/// <para>如果验证不通过请抛出 <see cref="InvalidSignedValueException"/>。</para>
/// 对应 Java: org.ofdrw.sign.verify.SignedDataValidateContainer
/// </summary>
public interface ISignedDataValidateContainer
{
    /// <summary>签名数据验证。</summary>
    /// <param name="type">电子签名类型（Sign/Seal）。</param>
    /// <param name="signAlgName">签名算法名称或 OID。</param>
    /// <param name="tbsContent">待签章内容（Signature.xml 文件内容）。</param>
    /// <param name="signedValue">电子签章数据或签名值（SignedValue.dat 文件内容）。</param>
    /// <exception cref="InvalidSignedValueException">电子签章数据失效。</exception>
    void Validate(string type, string signAlgName, byte[] tbsContent, byte[] signedValue);
}