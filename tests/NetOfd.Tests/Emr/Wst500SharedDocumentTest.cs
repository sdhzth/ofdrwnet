using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using NetOfd.Emr;
using NetOfd.Layout;
using NetOfd.Layout.Edit;
using NetOfd.Layout.Element;
using Xunit;

namespace NetOfd.Tests.Emr;

/// <summary>
/// WS/T 500.32—2016 共享文档结构化数据测试。
/// <para>
/// 覆盖：标准常量自洽性、共享文档 XML 结构（对照标准附录 A 示例）、
/// CustomTag 写入/读回闭环、OFD-H 文档结构化导出。
/// </para>
/// </summary>
public class Wst500SharedDocumentTest
{
    private static readonly XNamespace Ns = XNamespace.Get("urn:hl7-org:v3");

    private static EmrStructuredData BuildSample()
    {
        var d = new EmrStructuredData
        {
            DocumentSerialNo = "EMR-TEST-0001",
            Title = Wst500Codes.InpatientHomePageDocName,
            EffectiveTime = "20261024120000",
            VersionNumber = "1",
            PatientName = "张建国",
            SexCode = Wst500Codes.SexMale,
            BirthDate = "19580312",
            Age = 68,
            MaritalStatusCode = Wst500Codes.MaritalMarried,
            EthnicGroupCode = Wst500Codes.EthnicHan,
            NationalityCode = "156",
            IdCardNo = "xxxxxxxxxxxxxxxxxx",
            Phone = "xxxxxxxxxxx",
            MedicalRecordNo = "78420931",
            AdmissionNo = "20261018007",
            AddressProvince = "山东省",
            AddressCity = "xx市",
            AddressCounty = "xx区",
            AddressTownship = "通济街道",
            AddressStreet = "新兴路",
            AddressHouseNo = "28 号",
            AdmissionTime = "20261018092400",
            DischargeTime = "20261021114000",
            HospitalDays = 4,
            AdmissionRouteCode = Wst500Codes.AdmissionRouteOutpatient,
            AdmissionDepartment = "心血管内科",
            HospitalName = "xx市第三人民医院",
            HospitalOrgCode = "123456789012345678",
            AttendingDoctor = "李明",
            ResponsibleNurse = "王芳",
        };

        d.Diagnoses.Add(new EmrDiagnosis
        {
            Category = "出院诊断",
            Name = "冠状动脉粥样硬化性心脏病",
            Code = "I20.0",
        });
        d.VitalSigns.Add(new EmrObservation
        {
            DeCode = "DE04.50.007.00",
            DisplayName = "体温",
            Qualifier = "入院体温",
            NumericValue = 36.5,
            Unit = "℃",
        });
        d.Allergy = new EmrAllergy { Flag = "0", AllergicDrug = "否认" };
        return d;
    }

    // ── 常量自洽性 ───────────────────────────────────────────────────────

    [Fact]
    public void 标准编码常量_取值符合标准定义()
    {
        // WS/T 500.32 附录 A
        Assert.Equal("2.16.840.1.113883.1.3", Wst500Codes.DocTypeIdRoot);
        Assert.Equal("POCD_MT000040", Wst500Codes.DocTypeIdExtension);
        Assert.Equal("2.16.156.10011.2.1.1.52", Wst500Codes.InpatientHomePageTemplateId);
        Assert.Equal("C0032", Wst500Codes.InpatientHomePageDocCode);

        // 编码体系 OID
        Assert.Equal("2.16.156.10011.2.4", Wst500Codes.SharedDocCodeSystem);
        Assert.Equal("2.16.156.10011.2.2.1", Wst500Codes.DataElementCodeSystem);
        Assert.Equal("2.16.840.1.113883.6.1", Wst500Codes.LoincCodeSystem);

        // DE 码（WS 445.10）
        Assert.Equal("DE01.00.004.00", Wst500Codes.DeMedicalRecordNo);
        Assert.Equal("DE02.01.039.00", Wst500Codes.DeName);
        Assert.Equal("DE05.01.024.00", Wst500Codes.DeDiseaseCode);
        Assert.Equal("DE06.00.092.00", Wst500Codes.DeAdmissionTime);
    }

    [Fact]
    public void 章节LOINC码_全部为合法LOINC格式()
    {
        var loincCodes = new[]
        {
            Wst500Codes.SecVitalSigns, Wst500Codes.SecDiagnosis, Wst500Codes.SecProblemList,
            Wst500Codes.SecTransfer, Wst500Codes.SecDischargeDiagnosis, Wst500Codes.SecAllergy,
            Wst500Codes.SecLabExam, Wst500Codes.SecSurgery, Wst500Codes.SecHospitalizationHistory,
            Wst500Codes.SecHospitalizationProcess, Wst500Codes.SecAdministration,
            Wst500Codes.SecTreatmentPlan, Wst500Codes.SecCost,
        };
        foreach (var c in loincCodes)
        {
            // LOINC 形如 8716-3：数字-数字
            Assert.Matches(@"^\d+-\d+$", c);
        }
        // 不得重复
        Assert.Equal(loincCodes.Length, loincCodes.Distinct().Count());
    }

    [Fact]
    public void 数据元DE码_全部符合DE编码格式()
    {
        // 仅取以 "De" 开头且值为 DE 码形态的常量（排除 DepartmentRoot 等 OID 常量）
        var deCodes = typeof(Wst500Codes)
            .GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Where(f => f.Name.StartsWith("De") && f.FieldType == typeof(string))
            .Select(f => (string)f.GetValue(null)!)
            .Where(v => v.StartsWith("DE"))
            .ToList();

        Assert.NotEmpty(deCodes);
        foreach (var de in deCodes)
            Assert.Matches(@"^DE\d{2}\.\d{2}\.\d{2,3}\.\d{2}$", de);
    }

    // ── 共享文档结构 ─────────────────────────────────────────────────────

    [Fact]
    public void 共享文档_根元素与命名空间符合CDA约束()
    {
        var doc = Wst500Builder.Build(BuildSample());
        Assert.Equal(Ns + "ClinicalDocument", doc.Name);
        Assert.Equal("urn:hl7-org:v3", doc.Name.NamespaceName);
    }

    [Fact]
    public void 共享文档_文档活动类要素齐备()
    {
        var doc = Wst500Builder.Build(BuildSample());

        Assert.Equal("CN", doc.Element(Ns + "realmCode")?.Attribute("code")?.Value);
        Assert.Equal("2.16.840.1.113883.1.3", doc.Element(Ns + "typeId")?.Attribute("root")?.Value);
        Assert.Equal("POCD_MT000040", doc.Element(Ns + "typeId")?.Attribute("extension")?.Value);
        Assert.Equal("2.16.156.10011.2.1.1.52", doc.Element(Ns + "templateId")?.Attribute("root")?.Value);
        Assert.Equal("EMR-TEST-0001", doc.Element(Ns + "id")?.Attribute("extension")?.Value);
        Assert.Equal("C0032", doc.Element(Ns + "code")?.Attribute("code")?.Value);
        Assert.Equal("2.16.156.10011.2.4", doc.Element(Ns + "code")?.Attribute("codeSystem")?.Value);
        Assert.Equal("住院病案首页", doc.Element(Ns + "title")?.Value);
        Assert.Equal("20261024120000", doc.Element(Ns + "effectiveTime")?.Attribute("value")?.Value);
        Assert.Equal("N", doc.Element(Ns + "confidentialityCode")?.Attribute("code")?.Value);
        Assert.Equal("zh-CN", doc.Element(Ns + "languageCode")?.Attribute("code")?.Value);
        Assert.NotNull(doc.Element(Ns + "versionNumber"));
    }

    [Fact]
    public void 共享文档_患者信息含DE对应数据元()
    {
        var doc = Wst500Builder.Build(BuildSample());
        var patient = doc.Descendants(Ns + "patient").Single();

        Assert.Equal("张建国", patient.Element(Ns + "name")?.Value);
        Assert.Equal("1", patient.Element(Ns + "administrativeGenderCode")?.Attribute("code")?.Value);
        Assert.Equal("19580312", patient.Element(Ns + "birthTime")?.Attribute("value")?.Value);
        Assert.Equal("20", patient.Element(Ns + "maritalStatusCode")?.Attribute("code")?.Value);
        Assert.Equal("01", patient.Element(Ns + "ethnicGroupCode")?.Attribute("code")?.Value);

        // 病案号 / 住院号以 OID 根区分
        var ids = doc.Descendants(Ns + "patientRole").Single().Elements(Ns + "id").ToList();
        Assert.Contains(ids, i => i.Attribute("root")?.Value == Wst500Codes.MedicalRecordNoRoot
                                && i.Attribute("extension")?.Value == "78420931");
        Assert.Contains(ids, i => i.Attribute("root")?.Value == Wst500Codes.AdmissionNoRoot
                                && i.Attribute("extension")?.Value == "20261018007");
    }

    [Fact]
    public void 共享文档_章节使用LOINC编码且entry嵌在section内()
    {
        var doc = Wst500Builder.Build(BuildSample());

        // 生命体征章节
        var vsSection = doc.Descendants(Ns + "section")
            .First(s => s.Element(Ns + "code")?.Attribute("code")?.Value == Wst500Codes.SecVitalSigns);
        Assert.Equal("2.16.840.1.113883.6.1",
            vsSection.Element(Ns + "code")?.Attribute("codeSystem")?.Value);

        // entry 必须是 section 的子元素（不是 component 的兄弟）
        var entries = vsSection.Elements(Ns + "entry").ToList();
        Assert.Single(entries);
        Assert.Empty(vsSection.Parent!.Elements(Ns + "entry"));

        // observation 的 code 为 DE 码
        var obsCode = entries[0].Element(Ns + "observation")?.Element(Ns + "code");
        Assert.Equal("DE04.50.007.00", obsCode?.Attribute("code")?.Value);
        Assert.Equal(Wst500Codes.DataElementCodeSystem, obsCode?.Attribute("codeSystem")?.Value);

        // 数值与单位
        var value = entries[0].Element(Ns + "observation")?.Element(Ns + "value");
        Assert.Equal("36.5", value?.Attribute("value")?.Value);
        Assert.Equal("℃", value?.Attribute("unit")?.Value);
    }

    [Fact]
    public void 共享文档_诊断章节采用entry组织organizer且含ICD10编码()
    {
        var doc = Wst500Builder.Build(BuildSample());
        var dxSection = doc.Descendants(Ns + "section")
            .First(s => s.Element(Ns + "code")?.Attribute("code")?.Value == Wst500Codes.SecDiagnosis);

        var entry = dxSection.Elements(Ns + "entry").Single();
        var organizer = entry.Element(Ns + "organizer");
        Assert.Equal("CLUSTER", organizer?.Attribute("classCode")?.Value);

        var obs = organizer!.Elements(Ns + "observation").ToList();
        Assert.Equal(2, obs.Count);

        // 名称（ST）
        var nameObs = obs[0];
        Assert.Equal("DE05.01.025.00", nameObs.Element(Ns + "code")?.Attribute("code")?.Value);
        Assert.Equal("冠状动脉粥样硬化性心脏病",
            nameObs.Element(Ns + "value")?.Value);

        // 编码（CD + ICD-10 codeSystem）
        var codeObs = obs[1];
        Assert.Equal("DE05.01.024.00", codeObs.Element(Ns + "code")?.Attribute("code")?.Value);
        Assert.Equal("I20.0", codeObs.Element(Ns + "value")?.Attribute("code")?.Value);
        Assert.Equal(Wst500Codes.Icd10CodeSystem, codeObs.Element(Ns + "value")?.Attribute("codeSystem")?.Value);
    }

    [Fact]
    public void 共享文档_为合法XML且UTF8无BOM()
    {
        var bytes = Wst500Builder.BuildBytes(BuildSample());
        Assert.NotEqual(0xEF, bytes[0]); // 无 BOM

        var text = Encoding.UTF8.GetString(bytes);
        var doc = XDocument.Parse(text);
        Assert.Equal(Ns + "ClinicalDocument", doc.Root!.Name);
        Assert.Contains("urn:hl7-org:v3", text);
    }

    // ── CustomTag 写入 / 读回闭环 ───────────────────────────────────────

    [Fact]
    public void 自定义标引_写入后可读回并解析出数据元()
    {
        var ofd = BuildMinimalOfd();
        var payload = Wst500Builder.BuildBytes(BuildSample());
        var tagged = OfdTagger.AddCustomTag(ofd, EmrStructuredExporter.Wst500TypeId, payload);

        // 标引索引存在，且登记了标准 TypeID
        using (var ms = new MemoryStream(tagged))
        using (var zip = new ZipArchive(ms))
        {
            var idxEntry = zip.Entries.FirstOrDefault(e =>
                e.FullName.EndsWith("CustomTags.xml", StringComparison.OrdinalIgnoreCase));
            Assert.NotNull(idxEntry);
            var idx = XDocument.Load(idxEntry!.Open());
            var typeIds = idx.Descendants()
                .Where(e => e.Name.LocalName == "CustomTag")
                .Select(e => e.Attribute("TypeID")?.Value ?? "")
                .ToList();
            Assert.Contains(EmrStructuredExporter.Wst500TypeId, typeIds);
        }

        // 导出器能读回
        var result = EmrStructuredExporter.Export(tagged);
        Assert.True(result.Found);
        Assert.NotNull(result.SharedDocumentXml);
        Assert.Equal("张建国", result.DataElements[Wst500Codes.DeName]);
        Assert.Equal("78420931", result.DataElements[Wst500Codes.DeMedicalRecordNo]);
        Assert.Equal("20261018007", result.DataElements[Wst500Codes.DeAdmissionNo]);
        Assert.Single(result.Diagnoses);
        Assert.Equal("I20.0", result.Diagnoses[0].Code);
    }

    [Fact]
    public void 结构化导出_无标引的文档返回未找到()
    {
        var result = EmrStructuredExporter.Export(BuildMinimalOfd());
        Assert.False(result.Found);
        Assert.Empty(result.TypeIds);
    }

    [Fact]
    public void 结构化导出_JSON含标准声明与数据元()
    {
        var ofd = BuildMinimalOfd();
        var tagged = OfdTagger.AddCustomTag(ofd, EmrStructuredExporter.Wst500TypeId,
            Wst500Builder.BuildBytes(BuildSample()));
        var result = EmrStructuredExporter.Export(tagged);
        var json = Encoding.UTF8.GetString(EmrStructuredExporter.ToJson(result));

        Assert.Contains("WS 445.10-2014", json);
        Assert.Contains("DE01.00.004.00", json);
        Assert.Contains("78420931", json);
        Assert.Contains("张建国", json);
    }

    // ── 辅助 ─────────────────────────────────────────────────────────────

    private static byte[] BuildMinimalOfd()
    {
        string path = Path.Combine(Path.GetTempPath(), $"emr_struct_{Guid.NewGuid():N}.ofd");
        using (var doc = new OFDDoc(path))
        {
            doc.AddPage();
            doc.Add(new Paragraph("测试内容"));
            doc.Close();
        }
        return File.ReadAllBytes(path);
    }
}
