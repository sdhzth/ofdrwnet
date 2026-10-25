namespace NetOfd.Sign.Sm2Strut;

/// <summary>签名验证结果信息。</summary>
public sealed class VerifyInfo
{
    public bool Result { get; set; }
    public string Hit { get; set; } = string.Empty;

    public VerifyInfo(bool result, string hit)
    {
        Result = result;
        Hit = hit;
    }

    public static VerifyInfo Err(string errDesp) => new(false, errDesp);

    public static VerifyInfo Ok() => new(true, "");
}