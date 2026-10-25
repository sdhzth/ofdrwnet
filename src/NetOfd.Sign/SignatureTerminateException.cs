namespace NetOfd.Sign;

/// <summary>
/// 签名终止异常：文档不允许继续追加签名。
/// <para>当签名列表文件（Signatures.xml）已被某个签名的 References 保护时抛出，
/// 此时任何新的签名都会破坏已有签名的完整性。</para>
/// 对应 Java: org.ofdrw.sign.SignatureTerminateException
/// </summary>
public class SignatureTerminateException : SignException
{
    public SignatureTerminateException(string message) : base(message) { }
}