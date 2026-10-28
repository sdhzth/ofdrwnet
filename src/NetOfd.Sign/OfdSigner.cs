using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using NetOfd.Reader;
using NetOfd.Sign.StampPos;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.X509;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// OFD 数字签名引擎（整体保护文档的 SM2 签名）。
/// 参考 ofdrw-sign 的 OFDSigner：计算受保护文件 SM3 杂凑、构建签名描述文件、
/// 使用 SM2withSM3 对 TBS_Sign 签名并封装为 SES_V1（GM/T 0031）签章值。
/// <para>支持在已签名文档上追加签名（续签模式），自动递增签名目录与签名 ID。</para>
/// </summary>
public sealed class OfdSigner
{
    public const string SignId = "s1";
    public const string ProviderName = "NetOfd.Sign";
    public const string Company = "netofd";
    public const string Version = "1.0.0";
    /// <summary>SM2withSM3 算法 OID（1.2.156.10197.1.501）。</summary>
    public const string SignatureMethod = "1.2.156.10197.1.501";

    /// <summary>对已有 OFD 文件进行签名，输出新的已签名 OFD 文件。</summary>
    /// <param name="inputPath">输入 OFD 文件路径</param>
    /// <param name="outputPath">输出已签名 OFD 文件路径</param>
    /// <param name="container">签名容器（SES_V1/V4/V5 或 GB/T 35275 等）</param>
    /// <param name="signatureDateTime">签章时间（格式 "yyyy-MM-dd HH:mm:ss"，默认当前时间）</param>
    /// <param name="appearances">签章外观注释列表（可选）</param>
    /// <param name="signMode">签名模式（默认整体保护）</param>
    /// <param name="protectFileFilter">待保护文件过滤器（可选）</param>
    /// <param name="signIdProvider">签名 ID 提供器（默认标准格式 "sNNN"）</param>
    /// <param name="relativeId">此签名基于的签名标识符（OFD 2.0 可选）</param>
    public static void Sign(
        string inputPath, string outputPath,
        ISignContainer container,
        string? signatureDateTime = null,
        IReadOnlyList<SignatureModels.StampAnnot>? appearances = null,
        SignMode signMode = SignMode.WholeProtected,
        ProtectFileFilter? protectFileFilter = null,
        ISignIdProvider? signIdProvider = null,
        string? relativeId = null)
    {
        var map = ReadZip(inputPath);
        SignContainerCore(map, container, signatureDateTime, appearances, signMode, protectFileFilter, signIdProvider, relativeId);
        WriteZip(outputPath, map);
    }

    /// <summary>对已有 OFD 字节进行签名，返回已签名 OFD 字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="container">签名容器（SES_V1/V4/V5 或 GB/T 35275 等）</param>
    /// <param name="signatureDateTime">签章时间（格式 "yyyy-MM-dd HH:mm:ss"，默认当前时间）</param>
    /// <param name="appearances">签章外观注释列表（可选）</param>
    /// <param name="signMode">签名模式（默认整体保护）</param>
    /// <param name="protectFileFilter">待保护文件过滤器（可选）</param>
    /// <param name="signIdProvider">签名 ID 提供器（默认标准格式 "sNNN"）</param>
    /// <param name="relativeId">此签名基于的签名标识符（OFD 2.0 可选）</param>
    public static byte[] Sign(
        byte[] inputOfd,
        ISignContainer container,
        string? signatureDateTime = null,
        IReadOnlyList<SignatureModels.StampAnnot>? appearances = null,
        SignMode signMode = SignMode.WholeProtected,
        ProtectFileFilter? protectFileFilter = null,
        ISignIdProvider? signIdProvider = null,
        string? relativeId = null)
    {
        var map = ReadZip(inputOfd);
        SignContainerCore(map, container, signatureDateTime, appearances, signMode, protectFileFilter, signIdProvider, relativeId);
        return WriteZipToBytes(map);
    }

    /// <summary>对已有 OFD 文件进行签名，输出新的已签名 OFD 文件。</summary>
    /// <param name="inputPath">输入 OFD 文件路径</param>
    /// <param name="outputPath">输出已签名 OFD 文件路径</param>
    /// <param name="keyPair">SM2 密钥对</param>
    /// <param name="cert">签章人证书</param>
    /// <param name="signatureDateTime">签章时间（格式 "yyyy-MM-dd HH:mm:ss"，默认当前时间）</param>
    /// <param name="seal">电子印章 DER（可选）</param>
    /// <param name="isSeal">是否电子印章签名</param>
    /// <param name="appearances">签章外观注释列表（可选）</param>
    /// <param name="version">签章版本（1=SES_V1，4=SES_V4，5=SES_V5）。</param>
    /// <param name="timeStampHook">时间戳钩子（可选，仅 SES_V4/V5 附加时间戳）。</param>
    /// <param name="signMode">签名模式（默认整体保护）</param>
    /// <param name="protectFileFilter">待保护文件过滤器（可选）</param>
    /// <param name="signIdProvider">签名 ID 提供器（默认标准格式 "sNNN"）</param>
    /// <param name="relativeId">此签名基于的签名标识符（OFD 2.0 可选）</param>
    public static void Sign(
        string inputPath, string outputPath,
        AsymmetricCipherKeyPair keyPair, X509Certificate cert,
        string? signatureDateTime = null, Asn1Encodable? seal = null, bool isSeal = false,
        IReadOnlyList<SignatureModels.StampAnnot>? appearances = null,
        int version = 1, TimeStampHook? timeStampHook = null,
        SignMode signMode = SignMode.WholeProtected,
        ProtectFileFilter? protectFileFilter = null,
        ISignIdProvider? signIdProvider = null,
        string? relativeId = null)
    {
        var map = ReadZip(inputPath);
        SignKeyPairCore(map, keyPair, cert, signatureDateTime, seal, isSeal, appearances, version, timeStampHook, signMode, protectFileFilter, signIdProvider, relativeId);
        WriteZip(outputPath, map);
    }

    /// <summary>对已有 OFD 字节进行签名，返回已签名 OFD 字节。</summary>
    /// <param name="inputOfd">输入 OFD 字节</param>
    /// <param name="keyPair">SM2 密钥对</param>
    /// <param name="cert">签章人证书</param>
    /// <param name="signatureDateTime">签章时间（格式 "yyyy-MM-dd HH:mm:ss"，默认当前时间）</param>
    /// <param name="seal">电子印章 DER（可选）</param>
    /// <param name="isSeal">是否电子印章签名</param>
    /// <param name="appearances">签章外观注释列表（可选）</param>
    /// <param name="version">签章版本（1=SES_V1，4=SES_V4，5=SES_V5）。</param>
    /// <param name="timeStampHook">时间戳钩子（可选，仅 SES_V4/V5 附加时间戳）。</param>
    /// <param name="signMode">签名模式（默认整体保护）</param>
    /// <param name="protectFileFilter">待保护文件过滤器（可选）</param>
    /// <param name="signIdProvider">签名 ID 提供器（默认标准格式 "sNNN"）</param>
    /// <param name="relativeId">此签名基于的签名标识符（OFD 2.0 可选）</param>
    public static byte[] Sign(byte[] inputOfd,
        AsymmetricCipherKeyPair keyPair, X509Certificate cert,
        string? signatureDateTime = null, Asn1Encodable? seal = null, bool isSeal = false,
        IReadOnlyList<SignatureModels.StampAnnot>? appearances = null,
        int version = 1, TimeStampHook? timeStampHook = null,
        SignMode signMode = SignMode.WholeProtected,
        ProtectFileFilter? protectFileFilter = null,
        ISignIdProvider? signIdProvider = null,
        string? relativeId = null)
    {
        var map = ReadZip(inputOfd);
        SignKeyPairCore(map, keyPair, cert, signatureDateTime, seal, isSeal, appearances, version, timeStampHook, signMode, protectFileFilter, signIdProvider, relativeId);
        return WriteZipToBytes(map);
    }

    /// <summary>
    /// 对已有 OFD 文件进行签名并写入签章外观（由位置提供器生成），输出新的已签名 OFD 文件。
    /// <para>签章外观位置提供器基于 OFD 页面信息自动生成签章注释。</para>
    /// </summary>
    /// <param name="inputPath">输入 OFD 文件路径</param>
    /// <param name="outputPath">输出已签名 OFD 文件路径</param>
    /// <param name="keyPair">SM2 密钥对</param>
    /// <param name="cert">签章人证书</param>
    /// <param name="stampAppearances">签章外观位置提供器列表</param>
    /// <param name="signatureDateTime">签章时间（格式 "yyyy-MM-dd HH:mm:ss"，默认当前时间）</param>
    /// <param name="seal">电子印章 DER（可选）</param>
    /// <param name="isSeal">是否电子印章签名</param>
    /// <param name="signIdProvider">签名 ID 提供器（默认标准格式 "sNNN"）</param>
    public static void SignWithAppearance(
        string inputPath, string outputPath,
        AsymmetricCipherKeyPair keyPair, X509Certificate cert,
        IReadOnlyList<IStampAppearance> stampAppearances,
        string? signatureDateTime = null, Asn1Encodable? seal = null, bool isSeal = false,
        ISignIdProvider? signIdProvider = null)
    {
        var appearances = ResolveAppearances(inputPath, stampAppearances, signIdProvider);
        Sign(inputPath, outputPath, keyPair, cert, signatureDateTime, seal, isSeal, appearances);
    }

    /// <summary>
    /// 对已有 OFD 字节进行签名并写入签章外观（由位置提供器生成），返回已签名 OFD 字节。
    /// </summary>
    public static byte[] SignWithAppearance(
        byte[] inputOfd,
        AsymmetricCipherKeyPair keyPair, X509Certificate cert,
        IReadOnlyList<IStampAppearance> stampAppearances,
        string? signatureDateTime = null, Asn1Encodable? seal = null, bool isSeal = false,
        ISignIdProvider? signIdProvider = null)
    {
        var appearances = ResolveAppearances(inputOfd, stampAppearances, signIdProvider);
        return Sign(inputOfd, keyPair, cert, signatureDateTime, seal, isSeal, appearances);
    }

    // 通过读取器打开文档，由各外观位置提供器生成签章注释
    private static List<SignatureModels.StampAnnot> ResolveAppearances(
        string inputPath, IReadOnlyList<IStampAppearance> stampAppearances, ISignIdProvider? signIdProvider)
    {
        using var reader = new OFDReader(inputPath);
        return ResolveAppearances(reader, stampAppearances, signIdProvider);
    }

    private static List<SignatureModels.StampAnnot> ResolveAppearances(
        byte[] inputOfd, IReadOnlyList<IStampAppearance> stampAppearances, ISignIdProvider? signIdProvider)
    {
        using var reader = new OFDReader(new MemoryStream(inputOfd));
        return ResolveAppearances(reader, stampAppearances, signIdProvider);
    }

    private static List<SignatureModels.StampAnnot> ResolveAppearances(
        OFDReader reader, IReadOnlyList<IStampAppearance> stampAppearances, ISignIdProvider? signIdProvider)
    {
        var idProvider = signIdProvider ?? new StandFormatAtomicSignId();
        var result = new List<SignatureModels.StampAnnot>();
        foreach (var provider in stampAppearances)
            result.AddRange(provider.GetAppearance(reader, idProvider));
        return result;
    }

    private static void SignContainerCore(
        Dictionary<string, byte[]> map,
        ISignContainer container,
        string? signatureDateTime,
        IReadOnlyList<SignatureModels.StampAnnot>? appearances,
        SignMode signMode, ProtectFileFilter? protectFileFilter,
        ISignIdProvider? signIdProvider, string? relativeId)
    {
        var dt = signatureDateTime ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        SignCore(map, container.Sign, container.SignType, container.Seal, dt,
            appearances, signMode, protectFileFilter, signIdProvider, relativeId,
            signatureMethod: container.GetSignAlgOid());
    }

    private static void SignKeyPairCore(
        Dictionary<string, byte[]> map,
        AsymmetricCipherKeyPair keyPair, X509Certificate cert,
        string? signatureDateTime, Asn1Encodable? seal, bool isSeal,
        IReadOnlyList<SignatureModels.StampAnnot>? appearances,
        int version, TimeStampHook? timeStampHook,
        SignMode signMode, ProtectFileFilter? protectFileFilter,
        ISignIdProvider? signIdProvider, string? relativeId)
    {
        var dt = signatureDateTime ?? DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        byte[] signFunc(byte[] descBytes, string propertyInfo)
        {
            var dataHash = GmCrypto.Sm3(descBytes);
            if (version == 4 || version == 5)
            {
                // SES_V4/V5 的 TBS_Sign 强制包含电子印章
                if (seal is null)
                    throw new SignException($"SES_V{version} 签章需要提供电子印章（seal 参数）");
                string gt = DateTime.TryParse(dt, out var d) ? d.ToUniversalTime().ToString("yyyyMMddHHmmss") + "Z" : dt;
                var tbsSeqV4 = SesV4.BuildTbsSeq(version, seal.GetEncoded(), dataHash, gt, propertyInfo);
                var tbsDerV4 = tbsSeqV4.GetEncoded();
                var sm2SigV4 = GmCrypto.Sm2Sign(tbsDerV4, keyPair.Private);
                byte[]? token = timeStampHook?.Invoke(sm2SigV4);
                return SesV4.BuildSignedValue(tbsSeqV4, cert.GetEncoded(), sm2SigV4, token);
            }
            else
            {
                var tbsSeq = SesV1.BuildTbsSeq(
                    version: 1, dataHash: dataHash,
                    timeInfoBytes: Encoding.UTF8.GetBytes(dt),
                    propertyInfo: propertyInfo,
                    certDer: cert.GetEncoded(),
                    seal: isSeal ? seal : null);
                var tbsDer = tbsSeq.GetEncoded();
                var sm2Sig = GmCrypto.Sm2Sign(tbsDer, keyPair.Private);
                return SesV1.BuildSignedValue(tbsSeq, sm2Sig);
            }
        }

        var sealBytes = seal?.GetEncoded();
        SignCore(map, signFunc, "Seal", sealBytes, dt,
            appearances, signMode, protectFileFilter, signIdProvider, relativeId);
    }

    /// <summary>
    /// 签名核心流程（容器无关）：
    /// 解析已有签名列表 → 签名预检查 → 选择新的签名目录 → 更新 OFD.xml → 写入印章 →
    /// 计算受保护文件杂凑 → 构建签名描述文件 → 计算签名值 → 更新签名列表。
    /// </summary>
    private static void SignCore(
        Dictionary<string, byte[]> map,
        Func<byte[], string, byte[]> signFunc,
        string signType,
        byte[]? sealBytes,
        string signatureDateTime,
        IReadOnlyList<SignatureModels.StampAnnot>? appearances = null,
        SignMode signMode = SignMode.WholeProtected,
        ProtectFileFilter? protectFileFilter = null,
        ISignIdProvider? signIdProvider = null,
        string? relativeId = null,
        string? signatureMethod = null)
    {
        const string signsDir = "Doc_0/Signs";
        const string signaturesListFile = signsDir + "/Signatures.xml";
        signIdProvider ??= new StandFormatAtomicSignId();

        // 1. 解析已有签名列表（追加签名场景保留已有记录）
        var existingRecords = new List<(string Id, string Type, string BaseLoc, string? Relative)>();
        string? existingMaxId = null;
        string? resolvedListFile = null;
        if (map.TryGetValue(signaturesListFile, out var listBytes))
        {
            resolvedListFile = signaturesListFile;
            existingRecords.AddRange(SignatureModels.ParseSignatureRecords(listBytes));
            existingMaxId = SignatureModels.ParseMaxSignId(listBytes);
        }
        else if (map.TryGetValue("OFD.xml", out var ofdXmlBytes))
        {
            var loc = SignatureModels.ParseSignaturesLoc(ofdXmlBytes);
            var norm = loc?.TrimStart('/').Replace('\\', '/');
            if (norm != null && map.TryGetValue(norm, out listBytes))
            {
                resolvedListFile = norm;
                existingRecords.AddRange(SignatureModels.ParseSignatureRecords(listBytes));
                existingMaxId = SignatureModels.ParseMaxSignId(listBytes);
            }
        }

        // 2. 签名预检查（对齐 Java OFDSigner.preChecker）：
        //    若已有签名将签名列表文件纳入 References 保护，则文档不允许继续追加签名
        if (resolvedListFile != null)
            PreChecker(map, existingRecords, resolvedListFile);

        // 2. 选择新的签名目录（Sign_1、Sign_2、...）
        int nextIndex = 1;
        foreach (var (_, _, baseLoc, _) in existingRecords)
        {
            if (TryParseSignIndex(baseLoc, out var idx) && idx >= nextIndex)
                nextIndex = idx + 1;
        }
        var signDir = $"{signsDir}/Sign_{nextIndex}";
        var signatureFile = $"{signDir}/Signature.xml";
        var signedValueFile = $"{signDir}/SignedValue.dat";
        var sealFile = $"{signDir}/Seal.esl";

        // 3. 设置最大签名 ID 并分配新签名 ID
        if (existingMaxId != null)
            signIdProvider.SetCurrentMaxSignId(existingMaxId);
        else if (existingRecords.Count > 0)
            signIdProvider.SetCurrentMaxSignId(existingRecords.Max(r => SignIdParser.ParseIndex(r.Id)).ToString());
        var signId = signIdProvider.IncrementAndGet();

        // 4. 更新 OFD.xml 入口（幂等）
        if (map.TryGetValue("OFD.xml", out var ofdBytes))
        {
            map["OFD.xml"] = SignatureModels.UpdateOfdXmlBytes(ofdBytes, signaturesListFile);
            // GB/T 33190-2016 §7.1 / GB/T 48666-2026 §6.2：根节点应声明 defaults:ofd。
            // 必须在杂凑计算之前注入（OFD.xml 通常纳入签名保护范围）。
            // 置于 UpdateOfdXmlBytes 之后：LINQ-to-XML 重写会重分配命名空间前缀，
            // 若先注入 defaults 声明，重写会把元素前缀改为 defaults:，违反「标识符宜为 ofd」。
            map["OFD.xml"] = NetOfd.Core.OFDElement.InjectDefaultsDeclarationBytes(
                map["OFD.xml"], SignatureModels.Ns.NamespaceName);
        }

        // 5. 更新签名列表文件（保留已有记录 + 新记录，更新最大 ID）。
        //    对齐 Java OFDSigner.buildSignature：先写入列表文件再计算杂凑，
        //    使整体保护模式下 Signatures.xml 自身也纳入 References 保护。
        existingRecords.Add((signId, signType, signatureFile, relativeId));
        map[signaturesListFile] = SignatureModels.BuildSignaturesListBytes(existingRecords, signIdProvider.Get());
        // GB/T 33190-2016 §7.1 / GB/T 48666-2026 §6.2：根节点应声明 defaults:ofd。
        // 必须在杂凑计算之前注入，否则 Signatures.xml 纳入保护范围时签名会失效。
        map[signaturesListFile] = NetOfd.Core.OFDElement.InjectDefaultsDeclarationBytes(
            map[signaturesListFile], SignatureModels.Ns.NamespaceName);

        // 6. 印章写入包内（先于杂凑计算，使其参与保护）
        if (sealBytes != null)
            map[sealFile] = sealBytes;

        // 7. 计算受保护文件杂凑（此时 OFD.xml、Seal.esl 与 Signatures.xml 已是最终内容）
        var refs = BuildRefs(map, signMode, protectFileFilter, signatureFile, signedValueFile);

        // 8. 构建签名描述文件（含可选外观注释与印章引用；签名方法取自容器）
        var descBytes = SignatureModels.BuildSignatureDescriptionBytes(
            providerName: ProviderName, company: Company, version: Version,
            signatureMethod: signatureMethod ?? SignatureMethod, signatureDateTime: signatureDateTime,
            references: refs, signedValueLoc: signedValueFile,
            stampAnnots: appearances,
            sealLoc: sealBytes != null ? "/" + sealFile.Replace('\\', '/') : null);

        // GB/T 33190-2016 §7.1 / GB/T 48666-2026 §6.2：签名描述文件根节点亦应声明 defaults:ofd。
        // 必须在计算签名值之前注入，否则签名值与文件实际字节不符。
        descBytes = NetOfd.Core.OFDElement.InjectDefaultsDeclarationBytes(
            descBytes, SignatureModels.Ns.NamespaceName);

        // 9. 容器计算签名值
        var signedValue = signFunc(descBytes, signatureFile);

        // 10. 写入包内（签名描述、签名值）
        map[signatureFile] = descBytes;
        map[signedValueFile] = signedValue;
    }

    /// <summary>
    /// OFD 文档签名预检查（对齐 Java OFDSigner.preChecker）。
    /// <para>遍历文档中已有的全部签名，若某个签名的 References 将签名列表文件
    /// （Signatures.xml）纳入保护范围，则说明文档已整体锁定，不允许继续追加签名。</para>
    /// </summary>
    /// <exception cref="SignatureTerminateException">签名列表文件已被保护，不允许继续签章。</exception>
    private static void PreChecker(
        Dictionary<string, byte[]> map,
        List<(string Id, string Type, string BaseLoc, string? Relative)> existingRecords,
        string signaturesListFile)
    {
        if (existingRecords.Count == 0) return;
        var normListFile = signaturesListFile.Replace('\\', '/').TrimStart('/');
        foreach (var (_, _, baseLoc, _) in existingRecords)
        {
            var normBase = baseLoc?.Replace('\\', '/').TrimStart('/');
            if (string.IsNullOrEmpty(normBase) || !map.TryGetValue(normBase, out var descBytes))
                continue;
            foreach (var r in SignatureModels.ParseReferences(descBytes))
            {
                var fileRef = r.FileRef.Replace('\\', '/').TrimStart('/');
                if (string.Equals(fileRef, normListFile, StringComparison.OrdinalIgnoreCase))
                    throw new SignatureTerminateException("签名列表文件（Signatures.xml）已经被保护，文档不允许继续追加签名");
            }
        }
    }

    private static List<SignatureModels.ReferenceItem> BuildRefs(
        Dictionary<string, byte[]> map,
        SignMode signMode,
        ProtectFileFilter? protectFileFilter,
        string newSignatureFile,
        string newSignedValueFile)
    {
        var refs = new List<SignatureModels.ReferenceItem>();
        foreach (var kv in map)
        {
            if (!IsProtectedFile(kv.Key, signMode, newSignatureFile, newSignedValueFile)) continue;
            if (protectFileFilter != null)
            {
                var absPath = "/" + kv.Key.Replace('\\', '/').TrimStart('/');
                if (!protectFileFilter(absPath)) continue;
            }
            var hash = GmCrypto.Sm3(kv.Value);
            var fileRef = "/" + kv.Key.Replace('\\', '/').TrimStart('/');
            refs.Add(new SignatureModels.ReferenceItem
            {
                FileRef = fileRef,
                CheckValue = Convert.ToBase64String(hash)
            });
        }
        return refs;
    }

    /// <summary>判断是否为“受保护文件”（参与 References 杂凑）。</summary>
    /// <param name="name">包内条目名。</param>
    /// <param name="signMode">签名模式（续签模式下不保护 Signatures.xml）。</param>
    /// <param name="newSignatureFile">本次新签名描述文件路径（不参与保护）。</param>
    /// <param name="newSignedValueFile">本次新签名值文件路径（不参与保护）。</param>
    internal static bool IsProtectedFile(string name, SignMode signMode, string newSignatureFile, string newSignedValueFile)
    {
        var n = name.Replace('\\', '/');
        if (n.EndsWith("/")) return false; // 目录项
        // 本次新签名的描述/签名值文件不参与本次保护（尚未写入或由自身引用）
        if (n == newSignatureFile.Replace('\\', '/') || n == newSignedValueFile.Replace('\\', '/'))
            return false;
        // 续签模式下，签名列表文件（Signatures.xml）不参与保护（允许追加签名）
        if (signMode == SignMode.ContinueSign && n.EndsWith("/Signs/Signatures.xml"))
            return false;
        return true;
    }

    /// <summary>从签名文件 BaseLoc 中解析 "Sign_N" 的序号（N）。</summary>
    private static bool TryParseSignIndex(string baseLoc, out int index)
    {
        index = 0;
        if (string.IsNullOrEmpty(baseLoc)) return false;
        var m = Regex.Match(baseLoc.Replace('\\', '/'), @"Sign_(\d+)/", RegexOptions.IgnoreCase);
        if (m.Success && int.TryParse(m.Groups[1].Value, out index))
            return true;
        var m2 = Regex.Match(baseLoc.Replace('\\', '/'), @"Sign_(\d+)$", RegexOptions.IgnoreCase);
        return m2.Success && int.TryParse(m2.Groups[1].Value, out index);
    }

    // ---- zip 读写辅助 ----

    internal static Dictionary<string, byte[]> ReadZip(string path)
    {
        try
        {
            var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using var za = ZipFile.OpenRead(path);
            foreach (var e in za.Entries)
            {
                using var s = e.Open();
                map[e.FullName] = ReadAll(s);
            }
            return map;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException or FileNotFoundException)
        {
            throw new InvalidOperationException($"无法读取 OFD 文件「{path}」：不是有效的 ZIP/OFD 数据", ex);
        }
    }

    internal static Dictionary<string, byte[]> ReadZip(byte[] bytes)
    {
        try
        {
            var map = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            using var ms = new MemoryStream(bytes ?? throw new ArgumentException("输入字节为空（null）"));
            if (bytes.Length == 0) throw new InvalidDataException("输入为空字节数组");
            using var za = new ZipArchive(ms, ZipArchiveMode.Read);
            foreach (var e in za.Entries)
            {
                using var s = e.Open();
                map[e.FullName] = ReadAll(s);
            }
            return map;
        }
        catch (Exception ex) when (ex is InvalidDataException or ArgumentException)
        {
            throw new InvalidOperationException("无法读取 OFD 数据：不是有效的 ZIP/OFD 字节流", ex);
        }
    }

    internal static void WriteZip(string path, Dictionary<string, byte[]> map)
    {
        if (File.Exists(path)) File.Delete(path);
        using var za = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var kv in map)
            WriteEntry(za, kv.Key, kv.Value);
    }

    internal static byte[] WriteZipToBytes(Dictionary<string, byte[]> map)
    {
        using var ms = new MemoryStream();
        using (var za = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            foreach (var kv in map)
                WriteEntry(za, kv.Key, kv.Value);
        }
        return ms.ToArray();
    }

    private static void WriteEntry(ZipArchive za, string name, byte[] content)
    {
        var e = za.CreateEntry(name);
        using var s = e.Open();
        s.Write(content, 0, content.Length);
    }

    private static byte[] ReadAll(Stream s)
    {
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }
}