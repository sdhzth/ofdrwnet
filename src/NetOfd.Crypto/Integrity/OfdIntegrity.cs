using NetOfd.Core.Integrity;

namespace NetOfd.Crypto.Integrity;

/// <summary>
/// OFD 完整性保护协议（GM/T 0099-2020 §7.4.3）实现。
/// <para>流程：确认包内所有文件 → 组装完整性保护描述文件（DocEntries/OFDEntries.xml）→
/// 计算完整性保护文件的杂凑值并使用签名私钥数字签名 → 将签名值写入签名值文件（signedvalue.dat）→ 重新打包。</para>
/// 对应 Java: org.ofdrw.crypto.integrity.OFDIntegrity
/// </summary>
public class OfdIntegrity : IDisposable
{
    private readonly string _ofdFile;
    private readonly string _dest;
    private readonly AtomicInteger _idProvider = new();
    private bool _closed;

    /// <summary>创建 OFD 完整性保护器。</summary>
    /// <param name="ofdFile">待保护文件路径。</param>
    /// <param name="dest">完整性保护文件输出位置。</param>
    public OfdIntegrity(string ofdFile, string dest)
    {
        if (string.IsNullOrEmpty(ofdFile) || !File.Exists(ofdFile))
            throw new ArgumentException("待保护文件位置(ofdFile)不正确");
        if (string.IsNullOrEmpty(dest))
            throw new ArgumentException("完整性保护文件输出位置(dest)为空");
        _ofdFile = ofdFile;
        _dest = dest;
    }

    /// <summary>创建 OFD 完整性保护器（字节流形式）。</summary>
    /// <param name="ofd">待保护文件的字节。</param>
    /// <param name="signer">签名实现。</param>
    /// <returns>保护完成后的 OFD 文件字节。</returns>
    public static byte[] Protect(byte[] ofd, IProtectSigner signer)
    {
        if (ofd == null || ofd.Length == 0)
            throw new ArgumentException("待保护文件内容(ofd)为空");
        var map = ZipIO.ReadBytes(ofd);
        return ZipIO.WriteBytes(DoProtect(map, signer));
    }

    /// <summary>对文件实行完整性保护。</summary>
    /// <param name="signer">签名实现。</param>
    public void Protect(IProtectSigner signer)
    {
        if (signer == null)
            throw new ArgumentException("请提供签名实现");
        var map = ZipIO.ReadZip(_ofdFile);
        var protectedMap = DoProtect(map, signer);
        ZipIO.WriteZip(protectedMap, _dest);
    }

    /// <summary>执行保护流程（内存映射层面）。</summary>
    private static Dictionary<string, byte[]> DoProtect(Dictionary<string, byte[]> map, IProtectSigner signer)
    {
        var idProvider = new AtomicInteger();

        var fileList = new FileList();
        var ofdEntries = new OFDEntries()
            .SetID(idProvider.Next().ToString())
            .SetCreatorName("ofdrw-crypto")
            .SetVersion("1.0.0")
            .SetCreationDate(DateTime.Now)
            .SetSignedValueLoc("/signedvalue.dat")
            .SetFileList(fileList);

        // a) 确认文件包内的所有文件
        foreach (var kv in map)
        {
            var id = idProvider.Next().ToString();
            // b) 组装 签名完整性保护文件（容器内绝对路径）
            fileList.AddFile(id, "/" + kv.Key.TrimStart('/'));
        }

        // 把清单写入文件
        var ofdEntriesBytes = ZipIO.ToBytes(ofdEntries.Element);

        // c) d) 根据签名方案，计算完整性保护文件的杂凑值并使用签名私钥数字签名
        var signature = signer.DigestThenSign(ofdEntriesBytes);

        // e) 将数字签名结果写入签名值文件
        map["/OFDEntries.xml"] = ofdEntriesBytes;
        map["/signedvalue.dat"] = signature;
        return map;
    }

    /// <summary>请务必在程序结束时调用该方法释放资源。</summary>
    public void Dispose()
    {
        if (_closed) return;
        _closed = true;
    }

    /// <summary>原子递增标识。</summary>
    private sealed class AtomicInteger
    {
        private int _value;
        public int Next() => Interlocked.Increment(ref _value);
    }
}