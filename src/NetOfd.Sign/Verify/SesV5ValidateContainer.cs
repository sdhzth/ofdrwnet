namespace NetOfd.Sign.Verify;

/// <summary>
/// 《GM/T 0031-2025 安全电子签章密码技术规范》电子签章数据验证容器（SES_V5）。
/// <para>签名计算范围：签章签名 Sign(TBS_Sign)。</para>
/// 对应 Java: org.ofdrw.sign.verify.container.SESV5ValidateContainer
/// </summary>
public class SesV5ValidateContainer : ISignedDataValidateContainer
{
    public SesV5ValidateContainer()
    {
    }

    public void Validate(string type, string signAlgName, byte[] tbsContent, byte[] signedValue)
    {
        if (string.Equals(type, "Sign", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("签名类型(type)必须是 Seal，不支持数字签名验证");

        try
        {
            // 与 Java SESV5ValidateContainer 一致：仅做杂凑比对与 SM2 验签，不校验版本号
            SesV4.ParseAndVerify(signedValue, tbsContent);
        }
        catch (SignException ex)
        {
            throw new InvalidSignedValueException(ex.Message, ex);
        }
    }
}