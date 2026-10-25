using NetOfd.Sign.Sm2Strut;

namespace NetOfd.Sign.Verify;

/// <summary>
/// 根据 GM/T 0099-2020 §7.2.2 数据格式要求：
/// 签名类型为数字签名且签名算法使用 SM2 时，签名值数据应遵循 GB/T 35275。
/// 对应 Java: org.ofdrw.sign.verify.container.GBT35275ValidateContainer
/// </summary>
public class Gbt35275ValidateContainer : ISignedDataValidateContainer
{
    public Gbt35275ValidateContainer()
    {
    }

    public void Validate(string type, string alg, byte[] tbsContent, byte[] signedValue)
    {
        if (!string.Equals(type, "Sign", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("签名类型(type)必须是 Sign，不支持电子印章验证");

        VerifyInfo verifyInfo = GBT35275Validate.Validate(tbsContent, signedValue);
        if (!verifyInfo.Result)
            throw new InvalidSignedValueException(verifyInfo.Hit);
    }
}