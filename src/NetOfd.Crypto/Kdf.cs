using System;
using Org.BouncyCastle.Crypto.Digests;

namespace NetOfd.Crypto;

/// <summary>
/// 密钥派生函数（GB/T 32918.3-2016《SM2 椭圆曲线公钥密码算法 第3部分：密钥交换协议》5.4.3）。
/// <para>密钥派生函数作用：从输入比特串 Z 中派生出长度为 klen 的密钥数据，
/// 杂凑算法采用 SM3（遵循 GB/T 32905）。口令加密方案（1.1.1）使用此函数将用户口令扩展为密钥加密密钥(KEK)。</para>
/// 对应 Java: org.ofdrw.gm.support.KDF
/// </summary>
public static class Kdf
{
    /// <summary>从输入比特串 z 派生出长度为 klen 的密钥。</summary>
    public static byte[] Extend(byte[] z, int klen)
    {
        var md = new SM3Digest();
        int v = md.GetDigestSize(); // SM3 输出长度 = 32
        var k = new byte[klen];
        int ct = 0x00000001;
        int len = klen / v + (klen % v == 0 ? 0 : 1);
        for (int i = 0; i < len; i++)
        {
            md.Reset();
            md.BlockUpdate(z, 0, z.Length);
            md.Update((byte)(ct >> 24));
            md.Update((byte)(ct >> 16));
            md.Update((byte)(ct >> 8));
            md.Update((byte)ct);
            var hai = new byte[v];
            md.DoFinal(hai, 0);
            ct++;
            if (i + 1 == len && klen % v != 0)
                Array.Copy(hai, 0, k, v * (klen / v), klen - (v * (klen / v)));
            else
                Array.Copy(hai, 0, k, v * i, v);
        }
        return k;
    }
}
