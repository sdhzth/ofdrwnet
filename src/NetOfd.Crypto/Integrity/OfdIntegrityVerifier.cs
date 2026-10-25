using NetOfd.Core.Integrity;

namespace NetOfd.Crypto.Integrity;

/// <summary>
/// OFD 完整性协议校验（GM/T 0099-2020 §7.4.6 校验流程）实现。
/// <para>流程：解压文档 → 读取完整性保护描述文件（OFDEntries.xml）→ 读取签名值文件并进行签名验证 →
/// 根据文件表与包内文件比对，检查是否存在夹带。</para>
/// 对应 Java: org.ofdrw.crypto.integrity.OFDIntegrityVerifier
/// </summary>
public class OfdIntegrityVerifier
{
    /// <summary>创建 OFD 完整性协议校验器。</summary>
    public OfdIntegrityVerifier()
    {
    }

    /// <summary>执行 OFD 的完整性校验流程。</summary>
    /// <param name="inPath">待校验的 OFD 文件路径。</param>
    /// <param name="verifier">签名值验证器。</param>
    /// <returns>true - 文件完整且没有夹带；false - 文件存在夹带或校验失败。</returns>
    public bool Integrity(string inPath, IProtectVerifier verifier)
    {
        if (string.IsNullOrEmpty(inPath) || !File.Exists(inPath))
            throw new ArgumentException("待校验的OFD文件路径(in)为空或不存在");
        if (verifier == null)
            throw new ArgumentException("签名值验证器(verifier)为空");
        var map = ZipIO.ReadZip(inPath);
        return DoVerify(map, verifier);
    }

    /// <summary>执行 OFD 的完整性校验流程（字节流形式）。</summary>
    /// <param name="ofd">待校验的 OFD 文件字节。</param>
    /// <param name="verifier">签名值验证器。</param>
    /// <returns>true - 文件完整且没有夹带；false - 文件存在夹带或校验失败。</returns>
    public bool Integrity(byte[] ofd, IProtectVerifier verifier)
    {
        if (ofd == null || ofd.Length == 0)
            throw new ArgumentException("待校验的OFD文件内容(ofd)为空");
        if (verifier == null)
            throw new ArgumentException("签名值验证器(verifier)为空");
        var map = ZipIO.ReadBytes(ofd);
        return DoVerify(map, verifier);
    }

    /// <summary>执行校验流程（内存映射层面）。</summary>
    private static bool DoVerify(Dictionary<string, byte[]> map, IProtectVerifier verifier)
    {
        // a) 读取完整性保护描述文件（此处如果文件不存在则抛出异常）
        if (!map.TryGetValue("/" + OFDEntries.FileName, out var ofdEntriesBytes))
            throw new FileNotFoundException($"完整性保护描述文件({OFDEntries.FileName})不存在");
        var ofdEntries = new OFDEntries(ZipIO.ToXElement(ofdEntriesBytes));

        // b) c) 读取签名值文件，进行签名验证
        var signedValueLoc = ofdEntries.GetSignedValueLoc();
        string signedValuePath = NormalizeLoc(signedValueLoc.ToString());
        if (!map.TryGetValue(signedValuePath, out var signedValue))
            throw new FileNotFoundException($"签名值文件({signedValueLoc})不存在");
        bool integrity = verifier.DigestThenVerify(ofdEntriesBytes, signedValue);

        // 为了保证没有额外夹带的文件，还需根据文件表和包内文件进行比对
        bool hasExtraFile = CheckNoExtraFile(map, ofdEntries);
        return integrity && hasExtraFile;
    }

    /// <summary>检查是否存在 防止夹带文件列表 之外的夹带文件。</summary>
    /// <returns>true - 没有夹带；false - 存在夹带。</returns>
    private static bool CheckNoExtraFile(Dictionary<string, byte[]> map, OFDEntries ofdEntries)
    {
        var fileList = ofdEntries.GetFileList();

        // 建立文件 SET 用于检查映射情况
        var pkgFileSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in fileList.GetFiles())
            pkgFileSet.Add(NormalizeLoc(f.GetFileLoc().ToString()));

        // 签名值文件也加入 SET 中用于检查
        pkgFileSet.Add(NormalizeLoc(ofdEntries.GetSignedValueLoc().ToString()));

        // 遍历包内出现的所有文件，忽略完整性保护描述文件本身
        foreach (var key in map.Keys)
        {
            if (key == "/" + OFDEntries.FileName) continue;
            if (!pkgFileSet.Contains(key))
                return false; // 文件不在完整性保护文件列表中，认为发生夹带
        }
        return true;
    }

    /// <summary>确保路径以 / 开头。</summary>
    private static string NormalizeLoc(string loc)
    {
        if (string.IsNullOrEmpty(loc)) return "/";
        return loc.StartsWith('/') ? loc : "/" + loc;
    }
}