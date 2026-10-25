namespace NetOfd.Emr;

/// <summary>
/// 电子病历结构化数据模型（对应 WS/T 500.32—2016 住院病案首页共享文档）。
/// <para>
/// 本模型是「版式页面」与「机读数据」之间的桥：<b>同一个字段</b>既参与 OFD-H 版式绘制，
/// 又以标准 DE 码写入结构化标引，保证人读与机读内容严格一致（GB/T 48666—2026 第 7.3 c 条）。
/// </para>
/// </summary>
public sealed class EmrStructuredData
{
    // ═════════════════════════════════════════════════════════════════════
    //  文档头
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>文档流水号（WS/T 500.32：id/@extension）。</summary>
    public string DocumentSerialNo { get; set; } = "";

    /// <summary>文档标题。</summary>
    public string Title { get; set; } = Wst500Codes.InpatientHomePageDocName;

    /// <summary>文档生效/生成时间（yyyyMMddHHmmss）。</summary>
    public string EffectiveTime { get; set; } = "";

    /// <summary>密级代码（HL7 Confidentiality，默认 N = 正常访问）。</summary>
    public string ConfidentialityCode { get; set; } = "N";

    /// <summary>语言代码，默认 zh-CN。</summary>
    public string LanguageCode { get; set; } = "zh-CN";

    /// <summary>文档版本号。</summary>
    public string VersionNumber { get; set; } = "";

    // ═════════════════════════════════════════════════════════════════════
    //  患者信息
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>患者姓名。</summary>
    public string PatientName { get; set; } = "";

    /// <summary>性别代码（GB/T 2261.1：1=男 / 2=女）。</summary>
    public string SexCode { get; set; } = "";

    /// <summary>出生日期（yyyyMMdd）。</summary>
    public string BirthDate { get; set; } = "";

    /// <summary>年龄（岁）。</summary>
    public int? Age { get; set; }

    /// <summary>婚姻状况代码（GB/T 2261.2）。</summary>
    public string MaritalStatusCode { get; set; } = "";

    /// <summary>民族代码（GB 3304）。</summary>
    public string EthnicGroupCode { get; set; } = "";

    /// <summary>国籍代码（ISO 3166-1，156 = 中国）。</summary>
    public string NationalityCode { get; set; } = "156";

    /// <summary>身份证号。</summary>
    public string IdCardNo { get; set; } = "";

    /// <summary>联系电话。</summary>
    public string Phone { get; set; } = "";

    /// <summary>健康卡号。</summary>
    public string HealthCardNo { get; set; } = "";

    /// <summary>病案号。</summary>
    public string MedicalRecordNo { get; set; } = "";

    /// <summary>住院号。</summary>
    public string AdmissionNo { get; set; } = "";

    /// <summary>现住址 - 省。</summary>
    public string AddressProvince { get; set; } = "";

    /// <summary>现住址 - 市。</summary>
    public string AddressCity { get; set; } = "";

    /// <summary>现住址 - 县（区）。</summary>
    public string AddressCounty { get; set; } = "";

    /// <summary>现住址 - 乡镇（街道）。</summary>
    public string AddressTownship { get; set; } = "";

    /// <summary>现住址 - 街路。</summary>
    public string AddressStreet { get; set; } = "";

    /// <summary>现住址 - 门牌号。</summary>
    public string AddressHouseNo { get; set; } = "";

    /// <summary>现住址 - 邮政编码。</summary>
    public string PostalCode { get; set; } = "";

    // ═════════════════════════════════════════════════════════════════════
    //  就诊信息
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>住院次数。</summary>
    public int? AdmissionTimes { get; set; }

    /// <summary>入院日期时间（yyyyMMddHHmmss）。</summary>
    public string AdmissionTime { get; set; } = "";

    /// <summary>出院日期时间（yyyyMMddHHmmss）。</summary>
    public string DischargeTime { get; set; } = "";

    /// <summary>实际住院天数。</summary>
    public int? HospitalDays { get; set; }

    /// <summary>入院途径代码（01=门诊 / 02=急诊 / …）。</summary>
    public string AdmissionRouteCode { get; set; } = "";

    /// <summary>入院科别（科室名称）。</summary>
    public string AdmissionDepartment { get; set; } = "";

    /// <summary>出院科别（科室名称）。</summary>
    public string DischargeDepartment { get; set; } = "";

    /// <summary>入院病房。</summary>
    public string AdmissionWard { get; set; } = "";

    /// <summary>医疗机构名称。</summary>
    public string HospitalName { get; set; } = "";

    /// <summary>医疗机构组织机构代码。</summary>
    public string HospitalOrgCode { get; set; } = "";

    /// <summary>医疗付费方式代码。</summary>
    public string PaymentMethodCode { get; set; } = "";

    /// <summary>主管医师姓名。</summary>
    public string AttendingDoctor { get; set; } = "";

    /// <summary>责任护士姓名。</summary>
    public string ResponsibleNurse { get; set; } = "";

    /// <summary>质控（编码）员姓名。</summary>
    public string CodingDoctor { get; set; } = "";

    // ═════════════════════════════════════════════════════════════════════
    //  诊断
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>诊断记录（入院诊断 / 病理诊断 / 出院诊断等）。</summary>
    public List<EmrDiagnosis> Diagnoses { get; } = new();

    /// <summary>主要健康问题（住院者疾病状态、损伤外因等）。</summary>
    public List<EmrObservation> HealthProblems { get; } = new();

    // ═════════════════════════════════════════════════════════════════════
    //  临床章节
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>生命体征（体温、脉搏、呼吸、血压等）。</summary>
    public List<EmrObservation> VitalSigns { get; } = new();

    /// <summary>过敏史。</summary>
    public EmrAllergy? Allergy { get; set; }

    /// <summary>辅助检查结果（心电图等）。</summary>
    public List<EmrObservation> LabResults { get; } = new();

    /// <summary>手术及操作。</summary>
    public List<EmrSurgery> Surgeries { get; } = new();

    /// <summary>住院史 / 既往史。</summary>
    public string? HospitalizationHistory { get; set; }

    /// <summary>住院过程（诊疗过程描述）。</summary>
    public string? HospitalizationProcess { get; set; }

    /// <summary>治疗计划。</summary>
    public string? TreatmentPlan { get; set; }

    /// <summary>费用信息。</summary>
    public List<EmrObservation> Costs { get; } = new();

    /// <summary>其他医学处置。</summary>
    public string? OtherTreatment { get; set; }

    /// <summary>主诉（就诊原因）。</summary>
    public string? ChiefComplaint { get; set; }

    // ═════════════════════════════════════════════════════════════════════
    //  辅助方法
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>按性别代码推断中文性别。</summary>
    public string SexDisplay => SexCode switch
    {
        Wst500Codes.SexMale => "男",
        Wst500Codes.SexFemale => "女",
        _ => "",
    };

    /// <summary>由出生日期推算年龄（岁）。</summary>
    public int? AgeFromBirthDate()
    {
        if (string.IsNullOrWhiteSpace(BirthDate) || BirthDate.Length < 8) return null;
        if (!DateTime.TryParseExact(BirthDate[..8], "yyyyMMdd",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.None, out var bd)) return null;
        var now = DateTime.Now;
        var age = now.Year - bd.Year;
        if (now.Month < bd.Month || (now.Month == bd.Month && now.Day < bd.Day)) age--;
        return age < 0 ? null : age;
    }
}

/// <summary>诊断记录条目。</summary>
public sealed class EmrDiagnosis
{
    /// <summary>诊断类别：门(急)诊 / 入院 / 出院 / 病理 等。</summary>
    public string Category { get; set; } = "入院诊断";

    /// <summary>诊断名称（DE05.01.025.00）。</summary>
    public string Name { get; set; } = "";

    /// <summary>疾病编码（DE05.01.024.00，ICD-10）。</summary>
    public string Code { get; set; } = "";

    /// <summary>入院病情 / 疾病状态等伴随信息。</summary>
    public string? Status { get; set; }
}

/// <summary>手术及操作条目。</summary>
public sealed class EmrSurgery
{
    /// <summary>手术及操作名称（DE06.00.094.00）。</summary>
    public string Name { get; set; } = "";

    /// <summary>手术及操作编码（DE06.00.093.00，ICD-9-CM-3）。</summary>
    public string Code { get; set; } = "";

    /// <summary>手术日期时间（yyyyMMddHHmmss）。</summary>
    public string? DateTime { get; set; }
}

/// <summary>过敏史。</summary>
public sealed class EmrAllergy
{
    /// <summary>药物过敏标志（DE02.10.023.00，1=有 / 0=无）。</summary>
    public string? Flag { get; set; }

    /// <summary>过敏药物（DE02.10.022.00）。</summary>
    public string? AllergicDrug { get; set; }
}

/// <summary>标准数据元观测值（对应 WS/T 500 的 observation 元素）。</summary>
public sealed class EmrObservation
{
    /// <summary>数据元标识符（DE 码）。</summary>
    public string DeCode { get; set; } = "";

    /// <summary>数据元名称（displayName）。</summary>
    public string DisplayName { get; set; } = "";

    /// <summary>限定名（qualifier），用于区分同名数据元（如「入院体重」/「出生体重」）。</summary>
    public string? Qualifier { get; set; }

    /// <summary>文本值（xsi:type = ST）。</summary>
    public string? TextValue { get; set; }

    /// <summary>数值（xsi:type = PQ / INT 等）。</summary>
    public double? NumericValue { get; set; }

    /// <summary>计量单位。</summary>
    public string? Unit { get; set; }

    /// <summary>编码值（xsi:type = CD），如 ICD-10 诊断编码。</summary>
    public string? CodedValue { get; set; }

    /// <summary>编码值所属体系（xsi:type = CD 时的 codeSystem）。</summary>
    public string? CodedValueSystem { get; set; }
}
