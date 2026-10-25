using System;

namespace NetOfd.Crypto;

/// <summary>文档密码应用异常（GM/T 0099-2020）。</summary>
public class CryptoException : Exception
{
    public CryptoException(string message) : base(message) { }
    public CryptoException(string message, Exception inner) : base(message, inner) { }
}
