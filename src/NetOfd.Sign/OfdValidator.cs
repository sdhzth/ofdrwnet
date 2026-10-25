using System.IO.Compression;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.X509;
using NetOfd.Sign.Sm2Strut;
using NetOfd.Sign.Verify;
using X509Certificate = Org.BouncyCastle.X509.X509Certificate;

namespace NetOfd.Sign;

/// <summary>
/// OFD 数字签名验证引擎。
/// 参考 ofdrw-sign 的 OFDValidator：校验每个签名的 References 杂凑、印章匹配（Seal.esl 与签章数据内嵌印章）、
/// 依据签名类型与版本选择验证容器（GB/T 35275 / SES_V1 / SES_V4 / SES_V5），
/// 验证 SM2 签名与原文杂凑，并提取签章人/时间等信息。
/// </summary>
public sealed class OfdValidator
{
    /// <summary>单个签名的验证结果。</summary>
    public sealed class SignatureVerifyResult
    {
        public string SignId { get; init; } = "";
        public string Type { get; init; } = "Sign";
        public bool Valid { get; init; }
        public string? Error { get; init; }
        public string CertSubject { get; init; } = "";
        public DateTime? SignTime { get; init; }
        public int ReferenceCount { get; init; }
        public int ReferenceMismatch { get; init; }
        public string PropertyInfo { get; init; } = "";
    }

    /// <summary>整篇文档的验证结果。</summary>
    public sealed class VerifyResult
    {
        public bool AllValid { get; init; }
        public int SignatureCount { get; init; }
        public IReadOnlyList<SignatureVerifyResult> Signatures { get; init; } = Array.Empty<SignatureVerifyResult>();
        public string? Error { get; init; }
    }

    /// <summary>验证 OFD 文件中的所有签名。</summary>
    public static VerifyResult Verify(string ofdPath)
    {
        Dictionary<string, byte[]> map;
        try
        {
            map = OfdSigner.ReadZip(ofdPath);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            return new VerifyResult { AllValid = false, Error = $"无法读取 OFD 文件：{ex.Message}" };
        }
        return VerifyCore(map);
    }

    /// <summary>验证 OFD 字节中的所有签名。</summary>
    public static VerifyResult Verify(byte[] ofdBytes)
    {
        Dictionary<string, byte[]> map;
        try
        {
            map = OfdSigner.ReadZip(ofdBytes);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            return new VerifyResult { AllValid = false, Error = $"无法读取 OFD 数据：{ex.Message}" };
        }
        return VerifyCore(map);
    }

    private static VerifyResult VerifyCore(Dictionary<string, byte[]> map)
    {
        // 定位签名列表文件：优先 OFD.xml 中的 DocBody/Signatures@Loc，否则扫描
        string? listFile = null;
        if (map.TryGetValue("OFD.xml", out var ofdBytes))
            listFile = SignatureModels.ParseSignaturesLoc(ofdBytes);
        listFile ??= map.Keys.FirstOrDefault(k => k.Replace('\\', '/').EndsWith("/Signs/Signatures.xml"));

        if (listFile == null || !map.TryGetValue(listFile, out var listBytes))
            return new VerifyResult { AllValid = false, Error = "未找到签名列表文件（Signatures.xml）" };

        var records = SignatureModels.ParseSignatureRecords(listBytes);
        if (records.Count == 0)
            return new VerifyResult { AllValid = false, Error = "签名列表为空" };

        var results = new List<SignatureVerifyResult>();
        foreach (var (id, type, baseLoc, _) in records)
        {
            results.Add(VerifyOne(map, id, type, baseLoc));
        }

        return new VerifyResult
        {
            AllValid = results.All(r => r.Valid),
            SignatureCount = results.Count,
            Signatures = results
        };
    }

    private static SignatureVerifyResult VerifyOne(
        Dictionary<string, byte[]> map, string id, string type, string baseLoc)
    {
        try
        {
            var normBaseLoc = baseLoc.TrimStart('/').Replace('\\', '/');
            if (!map.TryGetValue(normBaseLoc, out var descBytes))
                return Fail(id, type, $"签名描述文件缺失：{normBaseLoc}");

            // 1. References 杂凑校验
            var refs = SignatureModels.ParseReferences(descBytes);
            int mismatch = 0;
            foreach (var r in refs)
            {
                var entryName = r.FileRef.TrimStart('/').Replace('\\', '/');
                if (!map.TryGetValue(entryName, out var content))
                {
                    mismatch++;
                    continue;
                }
                var hash = GmCrypto.Sm3(content);
                var expected = Convert.FromBase64String(r.CheckValue);
                if (!hash.AsSpan().SequenceEqual(expected))
                    mismatch++;
            }

            // 2. 读取签名值文件
            var signedValueLoc = SignatureModels.ParseSignedValueLoc(descBytes);
            var normSvl = signedValueLoc?.TrimStart('/').Replace('\\', '/');
            if (normSvl == null || !map.TryGetValue(normSvl, out var svBytes))
                return Fail(id, type, "签名值文件缺失");

            // 3. 印章匹配检查（对齐 Java OFDValidator.checkSealMatch）：
            //    电子签章时若 SignedInfo 中声明了 <Seal BaseLoc>，则比对 Seal.esl 与签章数据内嵌印章
            if (!string.Equals(type, "Sign", StringComparison.OrdinalIgnoreCase))
            {
                var sealLoc = SignatureModels.ParseSealBaseLoc(descBytes);
                if (!string.IsNullOrEmpty(sealLoc))
                {
                    var normSeal = sealLoc.TrimStart('/').Replace('\\', '/');
                    if (!map.TryGetValue(normSeal, out var sealBytes))
                        return Fail(id, type, $"电子印章文件缺失：{normSeal}");
                    if (!CheckSealMatch(sealBytes, svBytes))
                        return Fail(id, type, "印章(Seal.esl)与电子签章数据(SignedValue.dat)中的印章不匹配");
                }
            }

            // 4. 解析 SignedValue 并 SM2 验签（自动识别 SES_V1 / SES_V4 / SES_V5 / GB/T 35275）
            var verify = VerifySignedValue(svBytes, descBytes, type);
            var parsed = verify.Parsed;

            // 5. 提取签章信息
            var cert2 = CertUtil.ToDotNet(parsed.Cert);
            var subject = cert2.Subject;
            DateTime? signTime = null;
            var timeStr = verify.TimeInfoText;
            if (TryParseTimeText(timeStr, out var dt2)) signTime = dt2;

            if (mismatch > 0)
                return new SignatureVerifyResult
                {
                    SignId = id,
                    Type = type,
                    Valid = false,
                    Error = $"{mismatch} 个受保护文件杂凑不一致（可能被篡改）",
                    CertSubject = subject,
                    SignTime = signTime,
                    ReferenceCount = refs.Count,
                    ReferenceMismatch = mismatch,
                    PropertyInfo = parsed.PropertyInfo
                };

            return new SignatureVerifyResult
            {
                SignId = id,
                Type = type,
                Valid = true,
                CertSubject = subject,
                SignTime = signTime,
                ReferenceCount = refs.Count,
                ReferenceMismatch = 0,
                PropertyInfo = parsed.PropertyInfo
            };
        }
        catch (Exception ex)
        {
            return Fail(id, type, ex.Message);
        }
    }

    private static SignatureVerifyResult Fail(string id, string type, string error)
        => new SignatureVerifyResult { SignId = id, Type = type, Valid = false, Error = error };

    // 宽容解析签章时间（支持 "yyyy-MM-dd HH:mm:ss" 与 GeneralizedTime 形式）
    private static bool TryParseTimeText(string s, out DateTime dt)
    {
        dt = default;
        if (string.IsNullOrWhiteSpace(s)) return false;
        // 去掉尾部时区（Z / +0800 / +08:00 等），避免误伤日期中的 '-' 分隔符
        var clean = System.Text.RegularExpressions.Regex.Replace(
            s.Trim(), @"(Z|z|[+-]\d{2}:?\d{2})\s*$", "");
        clean = clean.Replace("T", " ").Trim();
        if (DateTime.TryParse(clean, out dt)) return true;
        var compact = clean.Replace(" ", "").Replace("-", "").Replace(":", "").Trim();
        if (compact.Length == 14 &&
            DateTime.TryParseExact(compact, "yyyyMMddHHmmss", System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out dt))
            return true;
        return false;
    }

    // 签章值验证结果（兼容 SES_V1 / SES_V4 / GB/T 35275）
    private sealed record SignedValueVerify
    {
        public required SesV1.ParsedSignedValue Parsed { get; init; }
        public required string TimeInfoText { get; init; }
    }

    /// <summary>解析签章值并做 SM2 验签，依据签名类型自动识别 SES_V1 / SES_V4 / SES_V5 / GB/T 35275 结构。</summary>
    /// <remarks>
    /// 与 Java OFDValidator 一致：按 Signatures.xml 中的 Type 选择验证容器，
    /// "Sign"（数字签名）→ <see cref="Gbt35275ValidateContainer"/>；
    /// "Seal"（电子签章）→ <see cref="SesV1ValidateContainer"/> / <see cref="SesV4ValidateContainer"/> /
    /// <see cref="SesV5ValidateContainer"/>（版本由 <see cref="SesVersionParser"/> 自动识别）。
    /// </remarks>
    private static SignedValueVerify VerifySignedValue(byte[] svBytes, byte[] descBytes, string type)
    {
        try
        {
            var seq = Asn1Sequence.GetInstance(Asn1Object.FromByteArray(svBytes));

            bool isGbt = seq.Count > 0 &&
                seq[0] is DerObjectIdentifier oid &&
                OIDs.SignedData.Equals(oid);

            if (string.Equals(type, "Sign", StringComparison.OrdinalIgnoreCase))
            {
                if (!isGbt)
                    throw new InvalidSignedValueException("数字签名值不符合 GB/T 35275 格式（旧版裸 SM2 签名需外部公钥验证，已废弃）");
                new Gbt35275ValidateContainer().Validate("Sign", "", descBytes, svBytes);
                return VerifyGbt35275(seq, descBytes);
            }

            // Seal（电子签章）：GB/T 35275 结构（少见）或 SES_V1/V4/V5
            if (isGbt)
            {
                new Gbt35275ValidateContainer().Validate("Sign", "", descBytes, svBytes);
                return VerifyGbt35275(seq, descBytes);
            }

            // 依据版本解析结果选择验证容器
            var holder = SesVersionParser.ParseSesSignatureVersion(svBytes);
            switch (holder.Version)
            {
                case SesVersion.V1:
                    new SesV1ValidateContainer().Validate(type, "", descBytes, svBytes);
                    break;
                case SesVersion.V4:
                    new SesV4ValidateContainer().Validate(type, "", descBytes, svBytes);
                    break;
                case SesVersion.V5:
                    new SesV5ValidateContainer().Validate(type, "", descBytes, svBytes);
                    break;
                default:
                    throw new InvalidSignedValueException($"不支持的电子签章数据版本：{holder.Version}");
            }

            if (holder.Version == SesVersion.V1)
            {
                var v1 = SesV1.ParseAndVerify(svBytes, descBytes);
                return new SignedValueVerify
                {
                    Parsed = v1,
                    TimeInfoText = System.Text.Encoding.UTF8.GetString(v1.TimeInfo)
                };
            }

            var v4 = SesV4.ParseAndVerify(svBytes, descBytes);
            return new SignedValueVerify
            {
                Parsed = new SesV1.ParsedSignedValue
                {
                    Version = v4.Version,
                    DataHash = v4.DataHash,
                    TimeInfo = System.Text.Encoding.UTF8.GetBytes(v4.TimeInfo),
                    PropertyInfo = v4.PropertyInfo,
                    Cert = v4.Cert,
                    TbsDer = v4.TbsDer,
                    Signature = v4.Signature
                },
                TimeInfoText = v4.TimeInfo
            };
        }
        catch (OfdVerifyException)
        {
            throw;
        }
        catch (ArgumentException ex)
        {
            throw new InvalidSignedValueException(ex.Message, ex);
        }
        catch (SignException ex)
        {
            throw new InvalidSignedValueException(ex.Message, ex);
        }
        catch (Exception ex)
        {
            throw new SignException($"SES_Signature 结构解析失败：{ex.Message}");
        }
    }

    /// <summary>
    /// 印章匹配检查：比对 Seal.esl 文件内容与电子签章数据（SES_Signature）中内嵌的印章是否一致。
    /// 对应 Java OFDValidator.checkSealMatch。
    /// </summary>
    private static bool CheckSealMatch(byte[] sealBytes, byte[] signedValueBytes)
    {
        Asn1Encodable esealObj;
        try
        {
            var holder = SesVersionParser.ParseSesSignatureVersion(signedValueBytes);
            var tbs = Asn1Sequence.GetInstance(holder.ObjSeq[0]);
            // TBS_Sign[1] 为电子印章（SES_V1 中可能为 [0] EXPLICIT 标记或 NULL）
            esealObj = tbs[1] is DerTaggedObject tagged ? tagged.GetBaseObject() : tbs[1];
        }
        catch (Exception)
        {
            return false;
        }

        if (esealObj is DerNull) return false;

        // DER 编码后与 Seal.esl 文件内容比对
        byte[] expect = esealObj.GetEncoded();
        return expect.AsSpan().SequenceEqual(sealBytes);
    }

    /// <summary>提取 GB/T 35275 SignedData 结构中的签章信息（验证已由容器完成）。</summary>
    private static SignedValueVerify VerifyGbt35275(Asn1Sequence seq, byte[] descBytes)
    {
        var contentInfo = ContentInfo.GetInstance(seq);
        var signedData = SignedData.GetInstance(contentInfo.Content);
        var cert = FindFirstSignCert(signedData);
        if (cert == null)
            throw new SignException("GBT35275 签名值中没有找到签名证书");
        return new SignedValueVerify
        {
            Parsed = new SesV1.ParsedSignedValue
            {
                Version = 0,
                DataHash = Array.Empty<byte>(),
                TimeInfo = Array.Empty<byte>(),
                PropertyInfo = string.Empty,
                Cert = cert,
                TbsDer = descBytes,
                Signature = Array.Empty<byte>()
            },
            TimeInfoText = string.Empty
        };
    }

    /// <summary>在 SignedData 中查找第一个签名者对应的证书。</summary>
    private static Org.BouncyCastle.X509.X509Certificate? FindFirstSignCert(SignedData signedData)
    {
        if (signedData.SignerInfos == null) return null;
        foreach (var item in signedData.SignerInfos)
        {
            var si = SignerInfo.GetInstance(item);
            var asn1Cert = signedData.GetSignCert(si.IssuerAndSerialNumber);
            if (asn1Cert != null)
                return new Org.BouncyCastle.X509.X509Certificate(asn1Cert.GetEncoded());
        }
        return null;
    }
}
