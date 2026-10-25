namespace NetOfd.Sign.Verify;

/// <summary>
/// 《GM/T 0031-2014 安全电子签章密码技术规范》电子签章数据验证容器（SES_V1）。
/// <para>注意：仅用于测试，电子签章验证请使用符合国家规范的流程进行！</para>
/// 对应 Java: org.ofdrw.sign.verify.container.SESV1ValidateContainer
/// </summary>
public class SesV1ValidateContainer : ISignedDataValidateContainer
{
    public SesV1ValidateContainer()
    {
    }

    public void Validate(string type, string signAlgName, byte[] tbsContent, byte[] signedValue)
    {
        if (string.Equals(type, "Sign", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("签名类型(type)必须是 Seal，不支持数字签名验证");

        try
        {
            SesV1.ParseAndVerify(signedValue, tbsContent);
        }
        catch (SignException ex)
        {
            throw new InvalidSignedValueException(ex.Message, ex);
        }
    }
}