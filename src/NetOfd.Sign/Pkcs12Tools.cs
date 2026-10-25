using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Pkcs;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// PKCS12（PFX/P12）解析工具：从 .p12 文件中读取私钥、用户证书与证书链。
/// 对应 Java: org.ofdrw.gm.cert.PKCS12Tools
/// </summary>
public static class Pkcs12Tools
{
    /// <summary>从 P12 中获取第一个条目的私钥。</summary>
    /// <param name="userP12">PKCS12 文件路径。</param>
    /// <param name="pwd">解密口令。</param>
    /// <returns>私钥。</returns>
    public static AsymmetricKeyParameter ReadPrvKey(string userP12, string pwd)
    {
        using var fs = File.OpenRead(userP12);
        return ReadPrvKey(fs, pwd);
    }

    /// <summary>从 P12 流中获取第一个密钥条目的私钥。</summary>
    public static AsymmetricKeyParameter ReadPrvKey(Stream rootKsIn, string pwd)
    {
        var store = Load(rootKsIn, pwd);
        string? alias = FirstKeyAlias(store) ?? throw new IOException("P12 中没有私钥条目");
        return store.GetKey(alias).Key;
    }

    /// <summary>从 P12 中获取指定别名的私钥。</summary>
    public static AsymmetricKeyParameter ReadPrvKey(string userP12, string alias, string pwd)
    {
        using var fs = File.OpenRead(userP12);
        return ReadPrvKey(fs, alias, pwd);
    }

    /// <summary>从 P12 流中获取指定别名的私钥。</summary>
    public static AsymmetricKeyParameter ReadPrvKey(Stream rootKsIn, string alias, string pwd)
    {
        var store = Load(rootKsIn, pwd);
        if (!store.IsKeyEntry(alias))
            throw new IOException($"别名({alias})不是私钥条目");
        return store.GetKey(alias).Key;
    }

    /// <summary>从 P12 流中获取证书链（第一张为用户证书）。</summary>
    public static X509Certificate[] ReadCertChain(Stream rootKsIn, string pwd)
    {
        var store = Load(rootKsIn, pwd);
        string? alias = FirstKeyAlias(store) ?? throw new IOException("P12 中没有私钥条目");
        var chain = store.GetCertificateChain(alias);
        var arr = new X509Certificate[chain.Length];
        for (int i = 0; i < chain.Length; i++)
            arr[i] = chain[i].Certificate;
        return arr;
    }

    /// <summary>从 P12 中获取用户证书。</summary>
    public static X509Certificate ReadUserCert(string userP12, string pwd)
    {
        using var fs = File.OpenRead(userP12);
        return ReadUserCert(fs, pwd);
    }

    /// <summary>从 P12 流中获取用户证书（证书链第一张）。</summary>
    public static X509Certificate ReadUserCert(Stream rootKsIn, string pwd)
    {
        var chain = ReadCertChain(rootKsIn, pwd);
        return chain[0];
    }

    private static Pkcs12Store Load(Stream input, string pwd)
    {
        var store = new Pkcs12StoreBuilder().Build();
        store.Load(input, pwd?.ToCharArray() ?? Array.Empty<char>());
        return store;
    }

    private static string? FirstKeyAlias(Pkcs12Store store)
    {
        foreach (string alias in store.Aliases)
        {
            if (store.IsKeyEntry(alias)) return alias;
        }
        return null;
    }
}