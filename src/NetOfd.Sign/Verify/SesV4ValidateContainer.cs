namespace NetOfd.Sign.Verify;

/// <summary>
/// 《GB/T 38540-2020 信息安全技术 安全电子签章密码技术规范》电子签章数据验证容器（SES_V4）。
/// <para>注意：仅用于测试，电子签章验证请使用符合国家规范的流程进行！</para>
/// 对应 Java: org.ofdrw.sign.verify.container.SESV4ValidateContainer
/// </summary>
public class SesV4ValidateContainer : ISignedDataValidateContainer
{
    public SesV4ValidateContainer()
    {
    }

    public void Validate(string type, string signAlgName, byte[] tbsContent, byte[] signedValue)
    {
        if (string.Equals(type, "Sign", StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("签名类型(type)必须是 Seal，不支持数字签名验证");

        try
        {
            // 与 Java SESV4ValidateContainer 一致：仅做杂凑比对与 SM2 验签，不校验版本号
            SesV4.ParseAndVerify(signedValue, tbsContent);
        }
        catch (SignException ex)
        {
            throw new InvalidSignedValueException(ex.Message, ex);
        }
    }
}