namespace NetOfd.Crypto;

/// <summary>解密结果：恢复出的文件加密密钥(FEK)与初始化向量(IV)。</summary>
public class DecryptResult
{
    public byte[] Fek { get; }
    public byte[] Iv { get; }

    public DecryptResult(byte[] fek, byte[] iv)
    {
        Fek = fek;
        Iv = iv;
    }
}
