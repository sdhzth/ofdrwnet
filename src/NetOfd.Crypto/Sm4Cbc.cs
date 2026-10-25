using System;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Paddings;
using Org.BouncyCastle.Crypto.Parameters;

namespace NetOfd.Crypto;

/// <summary>
/// SM4-CBC 对称加解密助手（GM/T 0099-2020 §7.3）。
/// <list type="bullet">
///   <item>对称算法 SM4，CBC 模式</item>
///   <item>填充算法遵循 PKCS#7</item>
///   <item>分块长度 16 字节（128 位），IV 长度 16 字节</item>
/// </list>
/// </summary>
public static class Sm4Cbc
{
    public static byte[] Encrypt(byte[] plaintext, byte[] key, byte[] iv)
    {
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new SM4Engine()), new Pkcs7Padding());
        cipher.Init(true, new ParametersWithIV(new KeyParameter(key), iv));
        return Process(cipher, plaintext);
    }

    public static byte[] Decrypt(byte[] ciphertext, byte[] key, byte[] iv)
    {
        var cipher = new PaddedBufferedBlockCipher(new CbcBlockCipher(new SM4Engine()), new Pkcs7Padding());
        cipher.Init(false, new ParametersWithIV(new KeyParameter(key), iv));
        return Process(cipher, ciphertext);
    }

    private static byte[] Process(PaddedBufferedBlockCipher cipher, byte[] input)
    {
        var outBuf = new byte[input.Length + cipher.GetBlockSize()];
        int len = cipher.ProcessBytes(input, 0, input.Length, outBuf, 0);
        len += cipher.DoFinal(outBuf, len);
        var result = new byte[len];
        Array.Copy(outBuf, result, len);
        return result;
    }
}
