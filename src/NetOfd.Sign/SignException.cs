namespace NetOfd.Sign;

/// <summary>签名/验签相关异常。</summary>
public class SignException : Exception
{
    public SignException(string message) : base(message) { }
    public SignException(string message, Exception inner) : base(message, inner) { }
}
