using System.IO.Compression;

namespace NetOfd.Sign;

/// <summary>
/// 签章清理器：移除已签名 OFD 文档中的全部签名（Signs 目录与 OFD.xml 入口）。
/// 参考 ofdrw-sign 的 SignCleaner。
/// </summary>
public sealed class SignCleaner
{
    /// <summary>清理文档中的全部签名。</summary>
    /// <param name="inputPath">已签名 OFD 文件路径。</param>
    /// <param name="outputPath">清理后的 OFD 输出路径。</param>
    public static void Clean(string inputPath, string outputPath)
    {
        var map = OfdSigner.ReadZip(inputPath);
        CleanCore(map);
        OfdSigner.WriteZip(outputPath, map);
    }

    /// <summary>清理文档中的全部签名，返回清理后的 OFD 字节。</summary>
    public static byte[] Clean(byte[] inputOfd)
    {
        var map = OfdSigner.ReadZip(inputOfd);
        CleanCore(map);
        return OfdSigner.WriteZipToBytes(map);
    }

    private static void CleanCore(Dictionary<string, byte[]> map)
    {
        // 1. 获取并移除 OFD.xml 中的 Signatures 入口
        string? listFile = null;
        if (map.TryGetValue("OFD.xml", out var ofdBytes))
        {
            listFile = SignatureModels.ParseSignaturesLoc(ofdBytes);
            map["OFD.xml"] = SignatureModels.RemoveSignaturesOfdXmlBytes(ofdBytes);
        }
        listFile ??= map.Keys.FirstOrDefault(k => k.Replace('\\', '/').EndsWith("/Signs/Signatures.xml"));

        // 2. 计算 Signs 目录前缀（Signatures.xml 的父目录，如 "Doc_0/Signs"）
        string? signsDir = null;
        if (listFile != null)
        {
            var norm = listFile.Replace('\\', '/').TrimStart('/');
            var idx = norm.LastIndexOf('/');
            if (idx > 0) signsDir = norm[..idx];
        }

        // 3. 删除 Signs 目录下的所有条目（签名描述、签名值、印章等）
        if (signsDir != null)
        {
            var keys = map.Keys
                .Where(k => k.Replace('\\', '/').StartsWith(signsDir + "/", StringComparison.OrdinalIgnoreCase))
                .ToList();
            foreach (var k in keys)
            {
                map.Remove(k);
            }
        }

        // 4. 兜底：删除已知的签名列表文件（某些规范将 Signatures.xml 直接放在 Signs 目录下）
        if (listFile != null)
        {
            map.Remove(listFile);
            // 兼容带前导 "/" 或反斜杠的写法
            map.Remove(listFile.TrimStart('/'));
            map.Remove(listFile.Replace('\\', '/').TrimStart('/'));
        }
    }
}