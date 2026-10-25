using System.Security.Cryptography;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.GM;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;

namespace NetOfd.Sign;

/// <summary>
/// 国密算法辅助：SM3 摘要、SM2 密钥生成/签名/验签。
/// 参考 ofdrw-gm / ofdrw-crypto，使用 BouncyCastle.Cryptography。
/// </summary>
public static class GmCrypto
{
    /// <summary>SM2 签名默认用户标识（GB/T 35276 / 《GM/T 0009》 推荐的 16 字节 ID）。</summary>
    public static readonly byte[] DefaultUserId = System.Text.Encoding.UTF8.GetBytes("1234567812345678");

    /// <summary>SM2withSM3 算法 OID（1.2.156.10197.1.501）。</summary>
    public static readonly DerObjectIdentifier Sm2SignWithSm3Oid = GMObjectIdentifiers.sm2sign_with_sm3;

    /// <summary>计算 SM3 摘要。</summary>
    public static byte[] Sm3(byte[] data)
    {
        var digest = new SM3Digest();
        digest.BlockUpdate(data, 0, data.Length);
        var outBuf = new byte[digest.GetDigestSize()];
        digest.DoFinal(outBuf, 0);
        return outBuf;
    }

    /// <summary>计算 SM3 摘要（流式）。</summary>
    public static byte[] Sm3(Stream stream)
    {
        var digest = new SM3Digest();
        var buf = new byte[8192];
        int read;
        while ((read = stream.Read(buf, 0, buf.Length)) > 0)
            digest.BlockUpdate(buf, 0, read);
        var outBuf = new byte[digest.GetDigestSize()];
        digest.DoFinal(outBuf, 0);
        return outBuf;
    }

    /// <summary>生成 SM2 密钥对（sm2p256v1 曲线）。</summary>
    public static AsymmetricCipherKeyPair GenerateSm2KeyPair()
    {
        var x9 = GMNamedCurves.GetByName("sm2p256v1");
        var ecSpec = new ECDomainParameters(x9.Curve, x9.G, x9.N, x9.H);
        var gen = new ECKeyPairGenerator();
        gen.Init(new ECKeyGenerationParameters(ecSpec, new SecureRandom()));
        return gen.GenerateKeyPair();
    }

    /// <summary>SM2 签名（SM3withSM2），返回 ASN.1 编码的签名值。</summary>
    public static byte[] Sm2Sign(byte[] data, AsymmetricKeyParameter privateKey, byte[]? userId = null)
    {
        userId ??= DefaultUserId;
        var signer = new SM2Signer();
        signer.Init(true, new ParametersWithID(
            new ParametersWithRandom(privateKey, new SecureRandom()), userId));
        signer.BlockUpdate(data, 0, data.Length);
        return signer.GenerateSignature();
    }

    /// <summary>SM2 验签（SM3withSM2）。</summary>
    public static bool Sm2Verify(byte[] data, byte[] signature, AsymmetricKeyParameter publicKey, byte[]? userId = null)
    {
        userId ??= DefaultUserId;
        var signer = new SM2Signer();
        signer.Init(false, new ParametersWithID(publicKey, userId));
        signer.BlockUpdate(data, 0, data.Length);
        return signer.VerifySignature(signature);
    }
}
