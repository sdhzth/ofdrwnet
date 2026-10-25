using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Utilities;

namespace NetOfd.Sign.Sm2Strut;

/// <summary>
/// GB/T 35276-2017 §7.2 SM2 加密数据格式。
/// <code>
/// SM2Cipher ::== SEQUENCE{
///     XCoordinate INTEGER,                 -- X 分量
///     YCoordinate INTEGER,                 -- Y 分量
///     HASH        OCTET STRING SIZE(32),   -- 明文 SM3 杂凑
///     CipherText  OCTET STRING             -- 密文
/// }
/// </code>
/// 对应 Java: org.ofdrw.gm.sm2strut.SM2Cipher
/// </summary>
public sealed class Sm2Cipher : Asn1Encodable
{
    public DerInteger XCoordinate { get; set; } = null!;
    public DerInteger YCoordinate { get; set; } = null!;
    public Asn1OctetString Hash { get; set; } = null!;
    public Asn1OctetString CipherText { get; set; } = null!;

    public Sm2Cipher() { }

    public Sm2Cipher(Asn1Sequence seq)
    {
        var e = seq.GetEnumerator();
        e.MoveNext(); XCoordinate = DerInteger.GetInstance(e.Current);
        e.MoveNext(); YCoordinate = DerInteger.GetInstance(e.Current);
        e.MoveNext(); Hash = DerOctetString.GetInstance(e.Current);
        e.MoveNext(); CipherText = DerOctetString.GetInstance(e.Current);
    }

    public static Sm2Cipher GetInstance(object? o)
    {
        if (o is Sm2Cipher c) return c;
        if (o != null) return new Sm2Cipher(Asn1Sequence.GetInstance(o));
        return null!;
    }

    public override Asn1Object ToAsn1Object() =>
        new DerSequence(XCoordinate, YCoordinate, Hash, CipherText);

    /// <summary>将 ASN.1 结构转换为 C1C3C2 格式（GM/T 0009-2012 加密数据结构）。</summary>
    public byte[] ConvertC1C3C2()
    {
        byte[] x = new byte[32];
        byte[] y = new byte[32];
        byte[] tmp = BigIntegers.AsUnsignedByteArray(XCoordinate.Value);
        Array.Copy(tmp, 0, x, 32 - tmp.Length, tmp.Length);
        tmp = BigIntegers.AsUnsignedByteArray(YCoordinate.Value);
        Array.Copy(tmp, 0, y, 32 - tmp.Length, tmp.Length);

        using var stream = new MemoryStream();
        // C1：非压缩点前缀 0x04 + X + Y
        stream.WriteByte(0x04);
        stream.Write(x, 0, x.Length);
        stream.Write(y, 0, y.Length);
        // C3：杂凑值
        var hash = Hash.GetOctets();
        stream.Write(hash, 0, hash.Length);
        // C2：密文
        var ct = CipherText.GetOctets();
        stream.Write(ct, 0, ct.Length);
        return stream.ToArray();
    }

    /// <summary>将 C1C3C2 格式的 SM2 加密结果转换为 ASN.1 SM2Cipher 结构。</summary>
    public static Sm2Cipher FromC1C3C2(byte[] c1c3c2)
    {
        using var stream = new MemoryStream(c1c3c2);
        // 跳过非压缩点前缀 0x04（1 字节）
        stream.ReadByte();
        var x = new byte[32];
        var y = new byte[32];
        var hash = new byte[32];
        int length = c1c3c2.Length - 1 - 32 - 32 - 32;
        var cipherText = new byte[length];
        stream.ReadExactly(x);
        stream.ReadExactly(y);
        stream.ReadExactly(hash);
        stream.ReadExactly(cipherText);

        return new Sm2Cipher
        {
            XCoordinate = new DerInteger(new BigInteger(1, x)),
            YCoordinate = new DerInteger(new BigInteger(1, y)),
            Hash = new DerOctetString(hash),
            CipherText = new DerOctetString(cipherText)
        };
    }
}