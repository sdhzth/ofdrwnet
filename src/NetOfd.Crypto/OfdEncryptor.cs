using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using NetOfd.Core.Crypto;
using Org.BouncyCastle.Security;

namespace NetOfd.Crypto;

/// <summary>
/// OFD 加密器（GM/T 0099-2020 §7.3 文档加密）。
/// <para>流程：生成 FEK+IV → 用 SM4-CBC 加密包内全部文件（生成 .dat 密文，删除明文）→
/// 将明密文映射表用 SM4-CBC 加密存于 entriesmap.dat →
/// 用各用户加密器包装 FEK 生成 decryptseed.dat → 写入解密入口 Encryptions.xml。</para>
/// 对应 Java: org.ofdrw.crypto.OFDEncryptor
/// </summary>
public class OfdEncryptor : IDisposable
{
    private readonly string _src;
    private readonly string _dest;
    private readonly List<IUserFekEncryptor> _users = new();
    private SecureRandom? _random;
    private bool _closed;

    public OfdEncryptor(string src, string dest)
    {
        _src = src ?? throw new ArgumentNullException(nameof(src));
        _dest = dest ?? throw new ArgumentNullException(nameof(dest));
    }

    /// <summary>添加加密用户（口令或证书加密器）。</summary>
    public OfdEncryptor AddUser(IUserFekEncryptor encryptor)
    {
        if (encryptor is not null) _users.Add(encryptor);
        return this;
    }

    /// <summary>设置随机源（默认软件随机源）。</summary>
    public OfdEncryptor SetRandom(SecureRandom random)
    {
        _random = random;
        return this;
    }

    public OfdEncryptor Encrypt()
    {
        if (_users.Count == 0)
            throw new ArgumentException("没有可用加密用户(UserFEKEncryptor)");

        var map = ZipIO.ReadZip(_src);

        Encryptions encryptions = map.TryGetValue("/Encryptions.xml", out var exb)
            ? new Encryptions(ZipIO.ToXElement(exb))
            : new Encryptions();
        string id = (encryptions.MaxID() + 1).ToString();

        const int blockSize = 16;
        var fek = new byte[blockSize];
        var iv = new byte[blockSize];
        (_random ??= new SecureRandom()).NextBytes(fek);
        (_random ??= new SecureRandom()).NextBytes(iv);

        var entries = new EncryptEntries().SetID(id);
        foreach (var kv in map.ToList())
        {
            if (IsMetadata(kv.Key)) continue;
            var encPath = EncPathFor(kv.Key);
            map[encPath] = Sm4Cbc.Encrypt(kv.Value, fek, iv);
            map.Remove(kv.Key);
            entries.AddEncryptEntry(kv.Key, encPath);
        }

        var entriesBytes = Sm4Cbc.Encrypt(ZipIO.ToBytes(entries.Element), fek, iv);
        const string entriesMapLoc = "/entriesmap.dat";
        map[entriesMapLoc] = entriesBytes;

        // 注意（对齐 Java ofdrw）：DecyptSeed 的 EncryptCaseId 保持为 null。
        // 标准 GM/T 0099 虽定义了该属性，但 ofdrw 当前版本并不写入，
        // 解密时改为以 UserInfo.UserName 路由各解密器，从而支持口令/证书混合多用户。
        var seed = new DecyptSeed().SetID(id).SetExtendParams(new ExtendParams());
        foreach (var u in _users)
        {
            var ui = u.Encrypt(fek, iv);
            if (ProtectionCaseIDExt.FromId(u.EncryptCaseId()) == ProtectionCaseID.EncryptGMCert)
            {
                var cert = u.UserCert();
                if (cert is null || cert.Length == 0)
                    throw new CryptoException("无法获取加密证书");
                ui.SetUserCert(cert);
            }
            seed.AddUserInfo(ui);
        }
        const string decryptSeedLoc = "/decryptseed.dat";
        map[decryptSeedLoc] = ZipIO.ToBytes(seed.Element);

        var info = new CT_EncryptInfo().SetID(id);
        info.SetProvider(new Provider()
            .SetCompany("netofd").SetProviderName("NetOfd.Crypto").SetVersion("1.0.0"));
        info.SetEncryptScope("All");
        info.SetEncryptDate(DateTime.Now);
        info.SetDecryptSeedLoc(decryptSeedLoc);
        info.SetEntriesMapLoc(entriesMapLoc);
        encryptions.AddEncryptInfo(info);
        map["/Encryptions.xml"] = ZipIO.ToBytes(encryptions.Element);

        ZipIO.WriteZip(map, _dest);
        return this;
    }

    /// <summary>计算密文在容器内的绝对路径：原目录 + 原文件名(去扩展名,转小写) + .dat。</summary>
    private static string EncPathFor(string originalPath)
    {
        int idx = originalPath.LastIndexOf('/');
        string dir = originalPath.Substring(0, idx);
        string fname = originalPath.Substring(idx + 1);
        int dot = fname.LastIndexOf('.');
        string name = dot != -1 ? fname.Substring(0, dot) : fname;
        return dir + "/" + name.ToLowerInvariant() + ".dat";
    }

    /// <summary>判断是否为加密元数据文件（不应被加密）。</summary>
    private static bool IsMetadata(string key)
    {
        var seg = key.TrimStart('/');
        int i = seg.LastIndexOf('/');
        var fname = i >= 0 ? seg.Substring(i + 1) : seg;
        var l = fname.ToLowerInvariant();
        return l == "encryptions.xml" || l.StartsWith("decryptseed") || l.StartsWith("entriesmap");
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
    }
}
