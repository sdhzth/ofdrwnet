namespace NetOfd.Sign.Verify;

/// <summary>
/// OFD 电子签名验证异常基类。
/// 对应 Java: org.ofdrw.sign.verify.exceptions.OFDVerifyException
/// </summary>
public class OfdVerifyException : Exception
{
    public OfdVerifyException(string message) : base(message) { }
    public OfdVerifyException(string message, Exception inner) : base(message, inner) { }
}

/// <summary>
/// 文件未进行电子签章异常。
/// 对应 Java: org.ofdrw.sign.verify.exceptions.DocNotSignException
/// </summary>
public class DocNotSignException : OfdVerifyException
{
    public DocNotSignException(string message) : base(message) { }
}

/// <summary>
/// 文件完整性校验异常（被保护文件杂凑不一致，可能被篡改）。
/// 对应 Java: org.ofdrw.sign.verify.exceptions.FileIntegrityException
/// </summary>
public class FileIntegrityException : OfdVerifyException
{
    /// <summary>被篡改的文件在容器内的位置。</summary>
    public string FileRef { get; }

    /// <summary>预期的文件杂凑值（Base64）。</summary>
    public string ExpectDataHash { get; }

    /// <summary>实际的文件杂凑值（Base64）。</summary>
    public string ActualDataHash { get; }

    public FileIntegrityException(string fileRef, byte[] expectDataHash, byte[] actualDataHash)
        : base($"文件({fileRef})可能被篡改，杂凑值不一致")
    {
        FileRef = fileRef;
        ExpectDataHash = Convert.ToBase64String(expectDataHash);
        ActualDataHash = Convert.ToBase64String(actualDataHash);
    }
}

/// <summary>
/// 电子签章数据失效异常（签名值验证不通过）。
/// 对应 Java: org.ofdrw.sign.verify.exceptions.InvalidSignedValueException
/// </summary>
public class InvalidSignedValueException : OfdVerifyException
{
    public InvalidSignedValueException(string message) : base(message) { }
    public InvalidSignedValueException(string message, Exception inner) : base(message, inner) { }
}