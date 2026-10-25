using System.Text;
using System.Text.Json;
using System.Xml.Linq;
using NetOfd.Layout.Edit;

namespace NetOfd.Emr;

/// <summary>
/// OFD-H 结构化数据导出器。
/// <para>
/// 从 OFD-H 文档中读取自定义标引（CustomTag）承载的结构化数据，并导出为：
/// <list type="bullet">
/// <item><b>WS/T 500 共享文档 XML</b>（符合 GB/T 48666—2026 第 7.3 c 条要求的标准结构）；</item>
/// <item><b>WS 445 数据元 JSON</b>（扁平化的 DE 码 → 值，便于系统对接与检索）。</item>
/// </list>
/// </para>
/// </summary>
public static class EmrStructuredExporter
{
    /// <summary>GB/T 48666—2026 结构化数据标引 TypeID（WS/T 500 共享文档）。</summary>
    public const string Wst500TypeId = "urn:emr:gb48666:wst500-shared-document";

    /// <summary>兼容旧版的机读信息标引 TypeID。</summary>
    public const string MachineReadableTypeId = "urn:emr:gb48666:machine-readable";

    /// <summary>兼容旧版的临床结构化标引 TypeID。</summary>
    public const string ClinicalDataTypeId = "urn:emr:gb48666:clinical-structured";

    /// <summary>导出结果。</summary>
    public sealed class ExportResult
    {
        /// <summary>是否找到结构化标引。</summary>
        public bool Found { get; set; }

        /// <summary>标引 TypeID 列表。</summary>
        public List<string> TypeIds { get; } = new();

        /// <summary>WS/T 500 共享文档 XML（若存在）。</summary>
        public byte[]? SharedDocumentXml { get; set; }

        /// <summary>扁平化的 DE 码 → 值（若可解析）。</summary>
        public Dictionary<string, string> DataElements { get; } = new();

        /// <summary>诊断条目（若可解析）。</summary>
        public List<EmrDiagnosis> Diagnoses { get; } = new();

        /// <summary>原始标引文件（全部）。</summary>
        public Dictionary<string, byte[]> RawTags { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>从 OFD 文件读取并解析结构化数据。</summary>
    /// <param name="ofdPath">OFD 文件路径。</param>
    public static ExportResult Export(string ofdPath)
    {
        if (!File.Exists(ofdPath))
            throw new FileNotFoundException("OFD 文件不存在", ofdPath);
        return Export(File.ReadAllBytes(ofdPath));
    }

    /// <summary>从 OFD 字节读取并解析结构化数据。</summary>
    public static ExportResult Export(byte[] ofdBytes)
    {
        var result = new ExportResult();
        var tags = OfdTagger.GetCustomTags(ofdBytes);
        if (tags.Count == 0) return result;

        result.Found = true;
        foreach (var (tag, content) in tags)
        {
            result.TypeIds.Add(tag.TypeID);
            var loc = tag.FileLoc.Value;
            result.RawTags[loc] = content;

            if (string.Equals(tag.TypeID, Wst500TypeId, StringComparison.OrdinalIgnoreCase))
            {
                result.SharedDocumentXml = content;
                ParseSharedDocument(content, result);
            }
            else if (string.Equals(tag.TypeID, ClinicalDataTypeId, StringComparison.OrdinalIgnoreCase))
            {
                ParseLegacyClinical(content, result);
            }
            else if (string.Equals(tag.TypeID, MachineReadableTypeId, StringComparison.OrdinalIgnoreCase))
            {
                ParseLegacyMachineReadable(content, result);
            }
        }
        return result;
    }

    /// <summary>导出为 WS 445 扁平数据元 JSON。</summary>
    public static byte[] ToJson(ExportResult result, bool indent = true)
    {
        var obj = new Dictionary<string, object?>
        {
            ["standard"] = "WS 445.10-2014 / WS/T 500.32-2016",
            ["docType"] = "OFD-H",
            ["dataElements"] = result.DataElements,
        };
        if (result.Diagnoses.Count > 0)
        {
            obj["diagnoses"] = result.Diagnoses.Select(d => new Dictionary<string, object?>
            {
                ["category"] = d.Category,
                ["name"] = d.Name,
                ["code"] = d.Code,
                ["status"] = d.Status,
            }).ToList();
        }
        var options = new JsonSerializerOptions
        {
            WriteIndented = indent,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };
        return JsonSerializer.SerializeToUtf8Bytes(obj, options);
    }

    /// <summary>导出为标准文本（人可读，便于质控与核对）。</summary>
    public static string ToText(ExportResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("═══ OFD-H 结构化数据导出（WS 445.10 / WS/T 500.32）═══");
        sb.AppendLine($"标引数量: {result.TypeIds.Count}  [{string.Join(", ", result.TypeIds)}]");
        if (result.SharedDocumentXml is not null)
            sb.AppendLine($"WS/T 500 共享文档: {result.SharedDocumentXml.Length} 字节");
        sb.AppendLine($"数据元条目: {result.DataElements.Count}");
        foreach (var (de, v) in result.DataElements)
            sb.AppendLine($"  {de,-22} {v}");
        if (result.Diagnoses.Count > 0)
        {
            sb.AppendLine("诊断:");
            foreach (var d in result.Diagnoses)
                sb.AppendLine($"  [{d.Category}] {d.Name} ({d.Code})");
        }
        return sb.ToString();
    }

    // ── 内部：解析 ───────────────────────────────────────────────────────

    private static void ParseSharedDocument(byte[] content, ExportResult result)
    {
        try
        {
            var xdoc = XDocument.Parse(Encoding.UTF8.GetString(content));
            var ns = Wst500Builder.Ns;

            // 文档头：患者基本信息
            var patient = xdoc.Descendants(ns + "patient").FirstOrDefault();
            if (patient is not null)
            {
                SetDe(result, Wst500Codes.DeName, patient.Element(ns + "name")?.Value);
                SetDe(result, Wst500Codes.DeSex, patient.Element(ns + "administrativeGenderCode")?.Attribute("code")?.Value);
                SetDe(result, Wst500Codes.DeBirthDate, patient.Element(ns + "birthTime")?.Attribute("value")?.Value);
                SetDe(result, Wst500Codes.DeMaritalStatus, patient.Element(ns + "maritalStatusCode")?.Attribute("code")?.Value);
                SetDe(result, Wst500Codes.DeEthnicGroup, patient.Element(ns + "ethnicGroupCode")?.Attribute("code")?.Value);
                SetDe(result, Wst500Codes.DeNationality, patient.Element(ns + "nationality")?.Attribute("code")?.Value);
            }

            var patientRole = xdoc.Descendants(ns + "patientRole").FirstOrDefault();
            if (patientRole is not null)
            {
                var ids = patientRole.Elements(ns + "id").ToList();
                foreach (var id in ids)
                {
                    var root = id.Attribute("root")?.Value;
                    var ext = id.Attribute("extension")?.Value;
                    if (root == Wst500Codes.HealthCardNoRoot) SetDe(result, Wst500Codes.DeHealthCardNo, ext);
                    else if (root == Wst500Codes.AdmissionNoRoot) SetDe(result, Wst500Codes.DeAdmissionNo, ext);
                    else if (root == Wst500Codes.MedicalRecordNoRoot) SetDe(result, Wst500Codes.DeMedicalRecordNo, ext);
                    else if (root == Wst500Codes.IdCardNoRoot) SetDe(result, "DE02.01.001.00", ext);
                }
                var telecom = patientRole.Element(ns + "telecom")?.Attribute("value")?.Value;
                SetDe(result, Wst500Codes.DePhone, telecom);

                var addr = patientRole.Element(ns + "addr");
                if (addr is not null)
                {
                    SetDe(result, Wst500Codes.DeAddrProvince, addr.Element(ns + "state")?.Value);
                    SetDe(result, Wst500Codes.DeAddrCity, addr.Element(ns + "city")?.Value);
                    SetDe(result, Wst500Codes.DeAddrCounty, addr.Element(ns + "county")?.Value);
                    SetDe(result, Wst500Codes.DeAddrTownship, addr.Element(ns + "township")?.Value);
                    SetDe(result, Wst500Codes.DeAddrStreet, addr.Element(ns + "streetName")?.Value);
                    SetDe(result, Wst500Codes.DeAddrHouseNo, addr.Element(ns + "houseNumber")?.Value);
                    SetDe(result, Wst500Codes.DePostalCode, addr.Element(ns + "postalCode")?.Value);
                }
            }

            // 文档头：责任者 / 管理者
            var authorName = xdoc.Descendants(ns + "author").FirstOrDefault()?
                .Descendants(ns + "name").FirstOrDefault()?.Value;
            SetDe(result, "DE02.01.039.00#attending", authorName);

            var custodianName = xdoc.Descendants(ns + "custodian").FirstOrDefault()?
                .Descendants(ns + "name").FirstOrDefault()?.Value;
            SetDe(result, Wst500Codes.DeHospitalName, custodianName);

            // 文档体：所有 observation → DE 码
            foreach (var obs in xdoc.Descendants(ns + "observation"))
            {
                var code = obs.Element(ns + "code");
                if (code is null) continue;
                var de = code.Attribute("code")?.Value;
                if (string.IsNullOrWhiteSpace(de)) continue;

                var display = code.Attribute("displayName")?.Value ?? "";
                var qualifier = code.Element(ns + "qualifier")?.Element(ns + "name")
                    ?.Attribute("displayName")?.Value;

                var value = obs.Element(ns + "value");
                string? val = null;
                if (value is not null)
                {
                    val = value.Attribute("value")?.Value
                          ?? value.Attribute("code")?.Value
                          ?? value.Value;
                }

                var key = string.IsNullOrWhiteSpace(qualifier) ? de : $"{de}#{qualifier}";
                if (!result.DataElements.ContainsKey(key))
                    result.DataElements[key] = val ?? "";
            }

            // 诊断条目：按 organizer（entry）分组解析，保证 name/code/status 正确配对
            foreach (var organizer in xdoc.Descendants(ns + "organizer"))
            {
                var dx = new EmrDiagnosis { Category = "诊断" };
                var hasName = false;

                foreach (var obs in organizer.Elements(ns + "observation"))
                {
                    var de = obs.Element(ns + "code")?.Attribute("code")?.Value;
                    if (de is null) continue;

                    var qualifier = obs.Element(ns + "code")?.Element(ns + "qualifier")
                        ?.Element(ns + "name")?.Attribute("displayName")?.Value;
                    if (!string.IsNullOrWhiteSpace(qualifier)) dx.Category = qualifier;

                    var v = obs.Element(ns + "value");
                    var val = v?.Attribute("value")?.Value ?? v?.Attribute("code")?.Value ?? v?.Value;

                    if (de == Wst500Codes.DeDiseaseName)
                    {
                        dx.Name = val ?? obs.Element(ns + "code")?.Attribute("displayName")?.Value ?? "";
                        hasName = true;
                    }
                    else if (de == Wst500Codes.DeDiseaseCode)
                    {
                        dx.Code = val ?? "";
                    }
                    else if (de == Wst500Codes.DeDiseaseStatus
                             || de == Wst500Codes.DeAdmissionConditionCode)
                    {
                        dx.Status = val;
                    }
                }

                if (hasName) result.Diagnoses.Add(dx);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("解析 WS/T 500 共享文档失败：" + ex.Message, ex);
        }
    }

    private static void ParseLegacyClinical(byte[] content, ExportResult result)
    {
        try
        {
            var xdoc = XDocument.Parse(Encoding.UTF8.GetString(content));
            var ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;

            foreach (var dx in xdoc.Descendants(ns + "Diagnosis"))
            {
                result.Diagnoses.Add(new EmrDiagnosis
                {
                    Category = dx.Attribute("Type")?.Value ?? "诊断",
                    Name = dx.Value,
                    Code = dx.Attribute("Icd10")?.Value ?? "",
                });
                result.DataElements[$"DIAG:{dx.Attribute("Type")?.Value}"] = dx.Value;
            }

            void Scalar(string local, string de)
            {
                var e = xdoc.Descendants(ns + local).FirstOrDefault();
                if (e is not null) SetDe(result, de, e.Value);
            }

            Scalar("ChiefComplaint", Wst500Codes.DeVisitReason);
            Scalar("Allergies", Wst500Codes.DeAllergicDrug);

            foreach (var vs in xdoc.Descendants(ns + "VitalSigns").FirstOrDefault()?.Elements() ?? Enumerable.Empty<XElement>())
            {
                var de = vs.Name.LocalName switch
                {
                    "Temperature" => "DE04.50.007.00",
                    "Pulse" => "DE04.50.008.00",
                    "Respiration" => "DE04.50.009.00",
                    "Sbp" => "DE04.50.010.00",
                    "Dbp" => "DE04.50.011.00",
                    _ => null,
                };
                if (de is not null) SetDe(result, de, vs.Value);
            }
        }
        catch
        {
            // 旧版标引解析失败不阻断导出
        }
    }

    private static void ParseLegacyMachineReadable(byte[] content, ExportResult result)
    {
        try
        {
            var xdoc = XDocument.Parse(Encoding.UTF8.GetString(content));
            var ns = xdoc.Root?.Name.Namespace ?? XNamespace.None;

            string? Get(string local) => xdoc.Descendants(ns + local).FirstOrDefault()?.Value;

            SetDe(result, Wst500Codes.DeName, Get("Name"));
            SetDe(result, Wst500Codes.DeSex, Get("Sex"));
            SetDe(result, Wst500Codes.DeBirthDate, NormalizeDate(Get("BirthDate")));
            SetDe(result, Wst500Codes.DeMedicalRecordNo, Get("MedicalRecordNo"));
            SetDe(result, Wst500Codes.DeAdmissionNo, Get("AdmissionNo"));
            SetDe(result, Wst500Codes.DeAdmissionTime, Get("AdmissionTime"));
            SetDe(result, Wst500Codes.DeDepartment, Get("Department"));
            SetDe(result, Wst500Codes.DeHospitalName, Get("Organization"));
        }
        catch
        {
            // 忽略
        }
    }

    private static void SetDe(ExportResult result, string de, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        // 标准共享文档（WS/T 500）的值优先；旧版标引不覆盖已存在的标准值
        if (!result.DataElements.ContainsKey(de))
            result.DataElements[de] = value;
    }

    /// <summary>日期归一化为 WS/T 500 的 yyyyMMdd 形式（兼容 yyyy-MM-dd / yyyy-MM-dd HH:mm:ss）。</summary>
    private static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return value;
        var v = value.Trim();
        if (DateTime.TryParse(v, System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var dt))
        {
            return dt.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);
        }
        return v;
    }
}
