using System;
using System.Collections.Generic;
using System.IO;
using NetOfd.Core.Crypto;

namespace NetOfd.Crypto;

/// <summary>
/// OFD 解密器（GM/T 0099-2020 §7.3，OFDEncryptor 的逆操作）。
/// <para>流程：读取 Encryptions.xml → 对每个加密信息：加载 decryptseed.dat 匹配解密器恢复 FEK →
/// SM4-CBC 解密 entriesmap.dat → 逐一解密文件写回明文并删除密文 →
/// 清理 decryptseed/entriesmap/Encryptions.xml → 重新打包为明文 OFD。</para>
/// 对应 Java: org.ofdrw.crypto.OFDDecryptor
/// </summary>
public class OfdDecryptor : IDisposable
{
    private readonly string _src;
    private readonly string _dest;
    private readonly List<IUserFekDecryptor> _decryptors = new();
    private bool _closed;

    public OfdDecryptor(string src, string dest)
    {
        _src = src ?? throw new ArgumentNullException(nameof(src));
        _dest = dest ?? throw new ArgumentNullException(nameof(dest));
    }

    /// <summary>添加解密用户（口令或证书解密器）。</summary>
    public OfdDecryptor AddUser(IUserFekDecryptor decryptor)
    {
        if (decryptor is not null) _decryptors.Add(decryptor);
        return this;
    }

    public void Decrypt()
    {
        var map = ZipIO.ReadZip(_src);

        if (!map.TryGetValue("/Encryptions.xml", out var encBytes))
        {
            // 文件未加密，直接打包输出
            ZipIO.WriteZip(map, _dest);
            return;
        }

        var encryptions = new Encryptions(ZipIO.ToXElement(encBytes));
        foreach (var info in encryptions.GetEncryptInfos())
        {
            var result = RecoverFek(info, map);
            var entries = DecryptEntriesMap(info, map, result);
            foreach (var entry in entries.GetEncryptEntries())
            {
                var cipherKey = entry.GetEPath()!;
                var plainKey = entry.GetPathA()!;
                if (!map.TryGetValue(cipherKey, out var cb)) continue;
                map[plainKey] = Sm4Cbc.Decrypt(cb, result.Fek, result.Iv);
                map.Remove(cipherKey);
            }
            map.Remove(info.GetDecryptSeedLoc()!);
            map.Remove(info.GetEntriesMapLoc()!);
        }

        map.Remove("/Encryptions.xml");
        ZipIO.WriteZip(map, _dest);
    }

    /// <summary>恢复文件加密密钥(FEK)和初始化向量(IV)。</summary>
    private DecryptResult RecoverFek(CT_EncryptInfo info, Dictionary<string, byte[]> map)
    {
        var seedLoc = info.GetDecryptSeedLoc()
            ?? throw new CryptoException("加密信息缺少 DecryptSeedLoc");
        if (!map.TryGetValue(seedLoc, out var sb))
            throw new CryptoException("密钥描述文件不存在: " + seedLoc);

        var seed = new DecyptSeed(ZipIO.ToXElement(sb));
        var caseId = seed.GetEncryptCaseId();
        var userInfos = seed.GetUserInfos();
        if (userInfos.Count == 0)
            throw new CryptoException("密钥描述文件中没有用户信息(UserInfo)");

        foreach (var d in _decryptors)
        {
            if (caseId is not null && caseId != d.EncryptCaseId()) continue;
            foreach (var ui in userInfos)
            {
                if (d.GetUsername() is not null && d.GetUsername() != ui.GetUserName()) continue;
                try { return d.Decrypt(ui); }
                catch (Exception) { /* 尝试下一个 UserInfo */ }
            }
        }

        throw new CryptoException($"没有匹配的解密器。文件加密方案: {caseId}");
    }

    /// <summary>解密明密文映射表（先尝试 SM4 解密，失败则按明文解析）。</summary>
    private EncryptEntries DecryptEntriesMap(CT_EncryptInfo info, Dictionary<string, byte[]> map, DecryptResult result)
    {
        var loc = info.GetEntriesMapLoc()
            ?? throw new CryptoException("加密信息缺少 EntriesMapLoc");
        if (!map.TryGetValue(loc, out var eb))
            throw new CryptoException("明密文映射表文件不存在: " + loc);

        byte[] plain;
        try { plain = Sm4Cbc.Decrypt(eb, result.Fek, result.Iv); }
        catch (Exception) { plain = eb; }
        return new EncryptEntries(ZipIO.ToXElement(plain));
    }

    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
    }
}
