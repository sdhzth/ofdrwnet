using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace NetOfd.Emr;

/// <summary>
/// WS/T 500.32—2016《电子病历共享文档规范 第 32 部分：住院病案首页》共享文档构建器。
/// <para>
/// 产出 <c>ClinicalDocument</c>（HL7 CDA R2 约束，见 WS/T 482—2016），三层结构：
/// <list type="number">
/// <item><b>文档头</b>：文档活动类（realmCode / typeId / templateId / id / code / title /
/// effectiveTime / confidentialityCode / languageCode）、参与者类（recordTarget 患者、
/// author 责任医师、custodian 病案管理者、participant 联系人）、关联活动类（relatedDocument）。</item>
/// <item><b>文档体</b>：<c>structuredBody</c> 下的章节（section），每章节以 LOINC 编码标识，
/// 条目以 <c>observation</c> 承载数据元（DE 码）。</item>
/// </list>
/// </para>
/// <para>
/// 输出文档可作为 OFD-H 自定义标引（CustomTag）的载荷，满足 GB/T 48666—2026 第 7.3 c 条
/// 「结构化数据…应当符合卫生行业现行标准，例如 WS/T 500 系列共享文档规范」的要求。
/// </para>
/// </summary>
public static class Wst500Builder
{
    /// <summary>WS/T 500 共享文档命名空间（HL7 CDA R2）。</summary>
    public static readonly XNamespace Ns = XNamespace.Get("urn:hl7-org:v3");

    /// <summary>WS/T 500 本土化扩展命名空间。</summary>
    public static readonly XNamespace MifNs = XNamespace.Get("urn:hl7-org:v3/mif");

    /// <summary>XML Schema 实例命名空间。</summary>
    public static readonly XNamespace XsiNs = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

    /// <summary>
    /// 构建住院病案首页共享文档。
    /// </summary>
    /// <param name="data">结构化数据模型。</param>
    /// <returns>ClinicalDocument 根元素（未格式化，便于调用方控制输出）。</returns>
    public static XElement Build(EmrStructuredData data)
    {
        ArgumentNullException.ThrowIfNull(data);

        var doc = new XElement(Ns + "ClinicalDocument",
            new XAttribute(XNamespace.Xmlns + "mif", MifNs.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "xsi", XsiNs.NamespaceName),
            new XAttribute(XsiNs + "schemaLocation", "urn:hl7-org:v3 ../sdschemas/SDA.xsd"));

        // ── 5.1 文档活动类 ────────────────────────────────────────────────
        doc.Add(new XElement(Ns + "realmCode", new XAttribute("code", "CN")));
        doc.Add(new XElement(Ns + "typeId",
            new XAttribute("root", Wst500Codes.DocTypeIdRoot),
            new XAttribute("extension", Wst500Codes.DocTypeIdExtension)));
        doc.Add(new XElement(Ns + "templateId",
            new XAttribute("root", Wst500Codes.InpatientHomePageTemplateId)));
        doc.Add(new XElement(Ns + "id",
            new XAttribute("root", Wst500Codes.DocSerialNoRoot),
            new XAttribute("extension", data.DocumentSerialNo)));
        doc.Add(new XElement(Ns + "code",
            new XAttribute("code", Wst500Codes.InpatientHomePageDocCode),
            new XAttribute("codeSystem", Wst500Codes.SharedDocCodeSystem),
            new XAttribute("codeSystemName", "卫生信息共享文档编码体系")));
        doc.Add(new XElement(Ns + "title", data.Title));
        doc.Add(new XElement(Ns + "effectiveTime", new XAttribute("value", data.EffectiveTime)));
        doc.Add(new XElement(Ns + "confidentialityCode",
            new XAttribute("code", data.ConfidentialityCode),
            new XAttribute("codeSystem", Wst500Codes.ConfidentialityCodeSystem),
            new XAttribute("codeSystemName", "Confidentiality"),
            new XAttribute("displayName", "正常访问保密级别")));
        doc.Add(new XElement(Ns + "languageCode", new XAttribute("code", data.LanguageCode)));
        if (!string.IsNullOrWhiteSpace(data.VersionNumber))
            doc.Add(new XElement(Ns + "setId", new XAttribute("id", data.DocumentSerialNo)));
        doc.Add(new XElement(Ns + "versionNumber", new XAttribute("value", data.VersionNumber)));

        // ── 5.2 参与者类：患者 ────────────────────────────────────────────
        doc.Add(BuildRecordTarget(data));

        // ── 5.2 参与者类：创作者 / 责任者 ────────────────────────────────
        if (!string.IsNullOrWhiteSpace(data.AttendingDoctor))
        {
            doc.Add(new XElement(Ns + "author",
                new XAttribute("typeCode", "AUT"),
                new XElement(Ns + "time", new XAttribute("value", data.EffectiveTime)),
                new XElement(Ns + "assignedEntity",
                    new XElement(Ns + "id",
                        new XAttribute("root", Wst500Codes.PractitionerRoot),
                        new XAttribute("extension", data.AttendingDoctor)),
                    new XElement(Ns + "code",
                        new XAttribute("displayName", "住院医师")),
                    new XElement(Ns + "assignedPerson",
                        new XAttribute("classCode", "PSN"),
                        new XAttribute("determinerCode", "INSTANCE"),
                        new XElement(Ns + "name", data.AttendingDoctor)))));
        }

        // ── 5.2 参与者类：文档管理者（病案室） ────────────────────────────
        if (!string.IsNullOrWhiteSpace(data.HospitalName))
        {
            doc.Add(new XElement(Ns + "custodian",
                new XAttribute("typeCode", "CST"),
                new XElement(Ns + "assignedEntity",
                    new XElement(Ns + "id",
                        new XAttribute("root", Wst500Codes.OrganizationRoot),
                        new XAttribute("extension", data.HospitalOrgCode)),
                    new XElement(Ns + "code",
                        new XAttribute("displayName", "病案室")),
                    new XElement(Ns + "representedOrganization",
                        new XAttribute("classCode", "ORG"),
                        new XAttribute("determinerCode", "INSTANCE"),
                        new XElement(Ns + "name", data.HospitalName)))));
        }

        // ── 5.3 关联活动类：相关文档（父文档，追溯版本） ──────────────────
        doc.Add(new XElement(Ns + "relatedDocument",
            new XAttribute("typeCode", "RPLC"),
            new XElement(Ns + "parentDocument",
                new XElement(Ns + "id", new XAttribute("root", Wst500Codes.DocSerialNoRoot)),
                new XElement(Ns + "setId", new XAttribute("id", data.DocumentSerialNo)))));

        // ── 6 文档体 ─────────────────────────────────────────────────────
        doc.Add(BuildStructuredBody(data));

        return doc;
    }

    /// <summary>构建 UTF-8（无 BOM）字节内容。</summary>
    public static byte[] BuildBytes(EmrStructuredData data, bool indent = true)
    {
        var root = Build(data);
        var settings = new XmlWriterSettings
        {
            Encoding = new UTF8Encoding(false),
            Indent = indent,
            IndentChars = "  ",
            NewLineChars = "\n",
            OmitXmlDeclaration = false,
        };
        using var ms = new MemoryStream();
        using (var w = XmlWriter.Create(ms, settings))
        {
            var xdoc = new XDocument(root);
            xdoc.Save(w);
        }
        return ms.ToArray();
    }

    // ── 内部：文档体 ─────────────────────────────────────────────────────

    private static XElement BuildStructuredBody(EmrStructuredData d)
    {
        // 结构：ClinicalDocument > component(structuredBody) 与 component(section)* 平级
        var container = new XElement(Ns + "component",
            new XElement(Ns + "structuredBody"));

        // 6.2 生命体征章节
        if (d.VitalSigns.Count > 0)
            container.Add(Section(Wst500Codes.SecVitalSigns, "VITAL SIGNS", d.VitalSigns));

        // 6.3 诊断记录章节
        if (d.Diagnoses.Count > 0 || !string.IsNullOrWhiteSpace(d.ChiefComplaint))
        {
            var (component, section) = SectionStart(Wst500Codes.SecDiagnosis, "Diagnosis");

            // 就诊原因（主诉，DE05.10.024.00）
            if (!string.IsNullOrWhiteSpace(d.ChiefComplaint))
            {
                section.Add(new XElement(Ns + "entry",
                    Observation(
                        de: Wst500Codes.DeVisitReason,
                        display: "就诊原因",
                        qualifier: "主诉",
                        value: new XAttribute(XsiNs + "type", "ST"),
                        text: d.ChiefComplaint)));
            }

            foreach (var dx in d.Diagnoses)
            {
                var entry = new XElement(Ns + "entry");
                var organizer = new XElement(Ns + "organizer",
                    new XAttribute("classCode", "CLUSTER"),
                    new XAttribute("moodCode", "EVN"),
                    new XElement(Ns + "statusCode"));

                // 疾病名称（DE05.01.025.00）
                organizer.Add(Observation(
                    de: Wst500Codes.DeDiseaseName,
                    display: dx.Name,
                    qualifier: dx.Category,
                    value: new XAttribute(XsiNs + "type", "ST"),
                    text: dx.Name));

                // 疾病编码（DE05.01.024.00，ICD-10）
                if (!string.IsNullOrWhiteSpace(dx.Code))
                {
                    organizer.Add(Observation(
                        de: Wst500Codes.DeDiseaseCode,
                        display: dx.Category + "疾病编码",
                        qualifier: dx.Category,
                        value: new XAttribute(XsiNs + "type", "CD"),
                        code: dx.Code,
                        codeSystem: Wst500Codes.Icd10CodeSystem));
                }

                // 疾病状态（DE05.10.119.00）
                if (!string.IsNullOrWhiteSpace(dx.Status))
                {
                    organizer.Add(Observation(
                        de: Wst500Codes.DeDiseaseStatus,
                        display: "住院者疾病状态代码",
                        qualifier: dx.Category,
                        value: new XAttribute(XsiNs + "type", "CD"),
                        code: dx.Status));
                }

                entry.Add(organizer);
                section.Add(entry);
            }
            container.Add(component);
        }

        // 6.4 主要健康问题章节
        if (d.HealthProblems.Count > 0)
            container.Add(Section(Wst500Codes.SecProblemList, "PROBLEM LIST", d.HealthProblems));

        // 6.7 过敏史章节
        if (d.Allergy is not null)
        {
            var list = new List<EmrObservation>();
            if (!string.IsNullOrWhiteSpace(d.Allergy.Flag))
                list.Add(new EmrObservation
                {
                    DeCode = Wst500Codes.DeAllergyFlag,
                    DisplayName = "药物过敏标志",
                    CodedValue = d.Allergy.Flag,
                });
            if (!string.IsNullOrWhiteSpace(d.Allergy.AllergicDrug))
                list.Add(new EmrObservation
                {
                    DeCode = Wst500Codes.DeAllergicDrug,
                    DisplayName = "过敏药物",
                    TextValue = d.Allergy.AllergicDrug,
                });
            if (list.Count > 0)
                container.Add(Section(Wst500Codes.SecAllergy, "Allergy history", list));
        }

        // 6.8 实验室检查章节
        if (d.LabResults.Count > 0)
            container.Add(Section(Wst500Codes.SecLabExam, "Laboratory studies", d.LabResults));

        // 6.9 手术操作章节
        if (d.Surgeries.Count > 0)
        {
            var list = new List<EmrObservation>();
            foreach (var s in d.Surgeries)
            {
                list.Add(new EmrObservation
                {
                    DeCode = Wst500Codes.DeSurgeryName,
                    DisplayName = "手术及操作名称",
                    TextValue = s.Name,
                });
                if (!string.IsNullOrWhiteSpace(s.Code))
                {
                    list.Add(new EmrObservation
                    {
                        DeCode = Wst500Codes.DeSurgeryCode,
                        DisplayName = "手术及操作编码",
                        CodedValue = s.Code,
                    });
                }
                if (!string.IsNullOrWhiteSpace(s.DateTime))
                {
                    list.Add(new EmrObservation
                    {
                        DeCode = Wst500Codes.DeSurgeryDateTime,
                        DisplayName = "手术及操作日期时间",
                        TextValue = s.DateTime,
                    });
                }
            }
            container.Add(Section(Wst500Codes.SecSurgery, "Surgery procedures", list));
        }

        // 6.10 住院史章节
        if (!string.IsNullOrWhiteSpace(d.HospitalizationHistory))
        {
            container.Add(Section(Wst500Codes.SecHospitalizationHistory, "Hospitalization history",
                new List<EmrObservation>
                {
                    new() { DeCode = Wst500Codes.DeDiseaseHistory, DisplayName = "疾病史（含外伤）", TextValue = d.HospitalizationHistory },
                }));
        }

        // 6.11 住院过程章节
        if (!string.IsNullOrWhiteSpace(d.HospitalizationProcess))
        {
            container.Add(Section(Wst500Codes.SecHospitalizationProcess, "Hospitalization process",
                new List<EmrObservation>
                {
                    new() { DeCode = Wst500Codes.DeAdmissionCondition, DisplayName = "入院情况", TextValue = d.HospitalizationProcess },
                }));
        }

        // 6.13 治疗计划章节
        if (!string.IsNullOrWhiteSpace(d.TreatmentPlan))
        {
            container.Add(Section(Wst500Codes.SecTreatmentPlan, "Treatment plan",
                new List<EmrObservation>
                {
                    new() { DeCode = Wst500Codes.DeOtherTreatment, DisplayName = "其他医学处置", TextValue = d.TreatmentPlan },
                }));
        }

        // 6.14 费用章节
        if (d.Costs.Count > 0)
            container.Add(Section(Wst500Codes.SecCost, "Hospital charges", d.Costs));

        return container;
    }

    // ── 内部：患者 ───────────────────────────────────────────────────────

    private static XElement BuildRecordTarget(EmrStructuredData d)
    {
        var patientRole = new XElement(Ns + "patientRole", new XAttribute("classCode", "PAT"));

        if (!string.IsNullOrWhiteSpace(d.HealthCardNo))
            patientRole.Add(Id(Wst500Codes.HealthCardNoRoot, d.HealthCardNo));
        if (!string.IsNullOrWhiteSpace(d.AdmissionNo))
            patientRole.Add(Id(Wst500Codes.AdmissionNoRoot, d.AdmissionNo));
        if (!string.IsNullOrWhiteSpace(d.MedicalRecordNo))
            patientRole.Add(Id(Wst500Codes.MedicalRecordNoRoot, d.MedicalRecordNo));
        if (!string.IsNullOrWhiteSpace(d.IdCardNo))
            patientRole.Add(Id(Wst500Codes.IdCardNoRoot, d.IdCardNo));

        if (HasAddress(d))
        {
            var addr = new XElement(Ns + "addr", new XAttribute("use", "H"));
            if (!string.IsNullOrWhiteSpace(d.AddressHouseNo)) addr.Add(new XElement(Ns + "houseNumber", d.AddressHouseNo));
            if (!string.IsNullOrWhiteSpace(d.AddressStreet)) addr.Add(new XElement(Ns + "streetName", d.AddressStreet));
            if (!string.IsNullOrWhiteSpace(d.AddressTownship)) addr.Add(new XElement(Ns + "township", d.AddressTownship));
            if (!string.IsNullOrWhiteSpace(d.AddressCounty)) addr.Add(new XElement(Ns + "county", d.AddressCounty));
            if (!string.IsNullOrWhiteSpace(d.AddressCity)) addr.Add(new XElement(Ns + "city", d.AddressCity));
            if (!string.IsNullOrWhiteSpace(d.AddressProvince)) addr.Add(new XElement(Ns + "state", d.AddressProvince));
            if (!string.IsNullOrWhiteSpace(d.PostalCode)) addr.Add(new XElement(Ns + "postalCode", d.PostalCode));
            patientRole.Add(addr);
        }

        if (!string.IsNullOrWhiteSpace(d.Phone))
            patientRole.Add(new XElement(Ns + "telecom", new XAttribute("value", d.Phone)));

        var patient = new XElement(Ns + "patient",
            new XAttribute("classCode", "PSN"),
            new XAttribute("determinerCode", "INSTANCE"),
            new XElement(Ns + "name", d.PatientName));

        if (!string.IsNullOrWhiteSpace(d.SexCode))
        {
            patient.Add(new XElement(Ns + "administrativeGenderCode",
                new XAttribute("code", d.SexCode),
                new XAttribute("codeSystem", Wst500Codes.AdministrativeGenderCodeSystem),
                new XAttribute("codeSystemName", "生理性别代码表(GB/T 2261.1)")));
        }
        if (!string.IsNullOrWhiteSpace(d.BirthDate))
            patient.Add(new XElement(Ns + "birthTime", new XAttribute("value", d.BirthDate)));
        if (!string.IsNullOrWhiteSpace(d.MaritalStatusCode))
        {
            patient.Add(new XElement(Ns + "maritalStatusCode",
                new XAttribute("code", d.MaritalStatusCode),
                new XAttribute("codeSystem", Wst500Codes.MaritalStatusCodeSystem),
                new XAttribute("codeSystemName", "婚姻状况代码表(GB/T 2261.2)")));
        }
        if (!string.IsNullOrWhiteSpace(d.EthnicGroupCode))
        {
            patient.Add(new XElement(Ns + "ethnicGroupCode",
                new XAttribute("code", d.EthnicGroupCode),
                new XAttribute("codeSystem", Wst500Codes.EthnicGroupCodeSystem),
                new XAttribute("codeSystemName", "民族类别代码表(GB 3304)")));
        }
        if (!string.IsNullOrWhiteSpace(d.NationalityCode))
        {
            patient.Add(new XElement(Ns + "nationality",
                new XAttribute("code", d.NationalityCode),
                new XAttribute("codeSystem", "2.16.156.10011.2.3.3.1"),
                new XAttribute("codeSystemName", "国籍代码表")));
        }
        patientRole.Add(patient);

        return new XElement(Ns + "recordTarget",
            new XAttribute("typeCode", "RCT"),
            new XAttribute("contextControlCode", "OP"),
            patientRole);
    }

    private static bool HasAddress(EmrStructuredData d)
        => !string.IsNullOrWhiteSpace(d.AddressProvince)
        || !string.IsNullOrWhiteSpace(d.AddressCity)
        || !string.IsNullOrWhiteSpace(d.AddressCounty)
        || !string.IsNullOrWhiteSpace(d.AddressTownship)
        || !string.IsNullOrWhiteSpace(d.AddressStreet)
        || !string.IsNullOrWhiteSpace(d.AddressHouseNo)
        || !string.IsNullOrWhiteSpace(d.PostalCode);

    // ── 内部：元素工厂 ───────────────────────────────────────────────────

    private static XElement Id(string root, string extension)
        => new(Ns + "id", new XAttribute("root", root), new XAttribute("extension", extension));

    private static (XElement Component, XElement Section) SectionStart(string loinc, string displayName)
    {
        var section = new XElement(Ns + "section",
            new XElement(Ns + "code",
                new XAttribute("code", loinc),
                new XAttribute("displayName", displayName),
                new XAttribute("codeSystem", Wst500Codes.LoincCodeSystem),
                new XAttribute("codeSystemName", "LOINC")),
            new XElement(Ns + "text"));
        return (new XElement(Ns + "component", section), section);
    }

    private static XElement Section(string loinc, string displayName, List<EmrObservation> observations)
    {
        var (component, comp) = SectionStart(loinc, displayName);

        foreach (var o in observations)
        {
            var obs = new XElement(Ns + "observation",
                new XAttribute("classCode", "OBS"),
                new XAttribute("moodCode", "EVN"));

            var code = new XElement(Ns + "code",
                new XAttribute("code", o.DeCode),
                new XAttribute("codeSystem", Wst500Codes.DataElementCodeSystem),
                new XAttribute("codeSystemName", "卫生信息数据元目录"),
                new XAttribute("displayName", o.DisplayName));

            if (!string.IsNullOrWhiteSpace(o.Qualifier))
                code.Add(new XElement(Ns + "qualifier",
                    new XElement(Ns + "name", new XAttribute("displayName", o.Qualifier))));

            obs.Add(code);

            if (o.NumericValue.HasValue)
            {
                var v = new XElement(Ns + "value", new XAttribute(XsiNs + "type", "PQ"));
                if (!string.IsNullOrWhiteSpace(o.Unit)) v.Add(new XAttribute("unit", o.Unit));
                v.Add(new XAttribute("value", o.NumericValue.Value.ToString("0.####",
                    System.Globalization.CultureInfo.InvariantCulture)));
                obs.Add(v);
            }
            else if (!string.IsNullOrWhiteSpace(o.CodedValue))
            {
                var v = new XElement(Ns + "value", new XAttribute(XsiNs + "type", "CD"));
                v.Add(new XAttribute("code", o.CodedValue));
                if (!string.IsNullOrWhiteSpace(o.CodedValueSystem))
                    v.Add(new XAttribute("codeSystem", o.CodedValueSystem));
                obs.Add(v);
            }
            else if (!string.IsNullOrWhiteSpace(o.TextValue))
            {
                obs.Add(new XElement(Ns + "value",
                    new XAttribute(XsiNs + "type", "ST"),
                    o.TextValue));
            }

            comp.Add(new XElement(Ns + "entry", obs));
        }

        return component;
    }

    private static XElement Observation(string de, string display, string? qualifier,
        XAttribute value, string? text = null, string? code = null, string? codeSystem = null)
    {
        var obs = new XElement(Ns + "observation",
            new XAttribute("classCode", "OBS"),
            new XAttribute("moodCode", "EVN"));

        var c = new XElement(Ns + "code",
            new XAttribute("code", de),
            new XAttribute("codeSystem", Wst500Codes.DataElementCodeSystem),
            new XAttribute("codeSystemName", "卫生信息数据元目录"),
            new XAttribute("displayName", display));

        if (!string.IsNullOrWhiteSpace(qualifier))
            c.Add(new XElement(Ns + "qualifier",
                new XElement(Ns + "name", new XAttribute("displayName", qualifier))));

        obs.Add(c);

        if (code is not null)
        {
            var v = new XElement(Ns + "value", value, new XAttribute("code", code));
            if (codeSystem is not null) v.Add(new XAttribute("codeSystem", codeSystem));
            obs.Add(v);
        }
        else if (text is not null)
        {
            obs.Add(new XElement(Ns + "value", value, text));
        }
        else
        {
            obs.Add(new XElement(Ns + "value", value));
        }

        return obs;
    }
}
