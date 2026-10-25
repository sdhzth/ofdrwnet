namespace NetOfd.Emr;

/// <summary>
/// WS/T 500《电子病历共享文档规范》与 WS 445《电子病历基本数据集》的标准编码常量。
/// <para>
/// 依据：
/// <list type="bullet">
/// <item>WS/T 500.32—2016《电子病历共享文档规范 第 32 部分：住院病案首页》</item>
/// <item>WS 445.10—2014《电子病历基本数据集 第 10 部分：住院病案首页》</item>
/// <item>WS/T 482—2016《卫生信息共享文档编制规范》</item>
/// </list>
/// </para>
/// </summary>
public static class Wst500Codes
{
    // ═════════════════════════════════════════════════════════════════════
    //  编码体系 OID
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>卫生信息共享文档编码体系（文档类别 code 的 codeSystem）。</summary>
    public const string SharedDocCodeSystem = "2.16.156.10011.2.4";

    /// <summary>卫生信息数据元目录（数据元 code 的 codeSystem）。</summary>
    public const string DataElementCodeSystem = "2.16.156.10011.2.2.1";

    /// <summary>LOINC（章节 code 的 codeSystem）。</summary>
    public const string LoincCodeSystem = "2.16.840.1.113883.6.1";

    /// <summary>HL7 Confidentiality（密级 codeSystem）。</summary>
    public const string ConfidentialityCodeSystem = "2.16.840.1.113883.5.25";

    /// <summary>疾病分类与代码（ICD-10 等，诊断编码 codeSystem）。</summary>
    public const string Icd10CodeSystem = "2.16.156.10011.2.3.11.5";

    /// <summary>生理性别代码表（GB/T 2261.1）。</summary>
    public const string AdministrativeGenderCodeSystem = "2.16.156.10011.2.3.3.4";

    /// <summary>婚姻状况代码表（GB/T 2261.2）。</summary>
    public const string MaritalStatusCodeSystem = "2.16.156.10011.2.3.3.5";

    /// <summary>民族类别代码表（GB 3304）。</summary>
    public const string EthnicGroupCodeSystem = "2.16.156.10011.2.3.3.3";

    /// <summary>入院途径代码表。</summary>
    public const string AdmissionRouteCodeSystem = "2.16.156.10011.2.3.1.270";

    /// <summary>文档注册模型根 OID（CDA R2 POCD_MT000040）。</summary>
    public const string DocTypeIdRoot = "2.16.840.1.113883.1.3";

    /// <summary>CDA 文档模板标识后缀。</summary>
    public const string DocTypeIdExtension = "POCD_MT000040";

    /// <summary>住院病案首页文档模板编号 OID。</summary>
    public const string InpatientHomePageTemplateId = "2.16.156.10011.2.1.1.52";

    /// <summary>文档流水号 OID 根。</summary>
    public const string DocSerialNoRoot = "2.16.156.10011.1.1";

    /// <summary>健康卡号 OID 根。</summary>
    public const string HealthCardNoRoot = "2.16.156.10011.1.19";

    /// <summary>住院号标识 OID 根。</summary>
    public const string AdmissionNoRoot = "2.16.156.10011.1.12";

    /// <summary>病案号标识 OID 根。</summary>
    public const string MedicalRecordNoRoot = "2.16.156.10011.1.13";

    /// <summary>身份证号 OID 根。</summary>
    public const string IdCardNoRoot = "2.16.156.10011.1.2";

    /// <summary>医疗卫生机构（机构标识）OID 根。</summary>
    public const string OrganizationRoot = "2.16.156.10011.1.4";

    /// <summary>科室标识 OID 根。</summary>
    public const string DepartmentRoot = "2.16.156.10011.1.26";

    /// <summary>医护人员标识 OID 根。</summary>
    public const string PractitionerRoot = "2.16.156.10011.1.21";

    /// <summary>就诊标识 OID 根。</summary>
    public const string EncounterRoot = "2.16.156.10011.1.21";

    /// <summary>病案标识（住院次数）OID 根。</summary>
    public const string AdmissionTimesRoot = "2.16.156.10011.1.21";

    /// <summary>文档类别编码：住院病案首页。</summary>
    public const string InpatientHomePageDocCode = "C0032";

    /// <summary>文档类别名称。</summary>
    public const string InpatientHomePageDocName = "住院病案首页";

    // ═════════════════════════════════════════════════════════════════════
    //  文档体章节（LOINC 编码）—— WS/T 500.32 第 6.1 节 表 5
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>生命体征章节（LOINC 8716-3）。</summary>
    public const string SecVitalSigns = "8716-3";

    /// <summary>诊断记录章节（LOINC 29548-5）。</summary>
    public const string SecDiagnosis = "29548-5";

    /// <summary>主要健康问题章节（LOINC 11450-4）。</summary>
    public const string SecProblemList = "11450-4";

    /// <summary>转科记录章节（LOINC 28570-0）。</summary>
    public const string SecTransfer = "28570-0";

    /// <summary>出院诊断章节（LOINC 56846-4）。</summary>
    public const string SecDischargeDiagnosis = "56846-4";

    /// <summary>过敏史章节（LOINC 48765-2）。</summary>
    public const string SecAllergy = "48765-2";

    /// <summary>实验室检查章节（LOINC 26449-9）。</summary>
    public const string SecLabExam = "26449-9";

    /// <summary>手术操作章节（LOINC 29752-6）。</summary>
    public const string SecSurgery = "29752-6";

    /// <summary>住院史章节（LOINC 11348-0）。</summary>
    public const string SecHospitalizationHistory = "11348-0";

    /// <summary>住院过程章节（LOINC 26633-6）。</summary>
    public const string SecHospitalizationProcess = "26633-6";

    /// <summary>行政管理章节（LOINC 32473-6）。</summary>
    public const string SecAdministration = "32473-6";

    /// <summary>治疗计划章节（LOINC 18776-5）。</summary>
    public const string SecTreatmentPlan = "18776-5";

    /// <summary>费用章节（LOINC 32473-6 对应费用类目，本标准按住院费用语义给出）。</summary>
    public const string SecCost = "22562-7";

    // ═════════════════════════════════════════════════════════════════════
    //  数据元标识符（DE 码）—— WS 445.10-2014 住院病案首页子集
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>DE01.00.004.00 病案号。</summary>
    public const string DeMedicalRecordNo = "DE01.00.004.00";

    /// <summary>DE01.00.014.00 住院号。</summary>
    public const string DeAdmissionNo = "DE01.00.014.00";

    /// <summary>DE01.00.019.00 出院病房 / 入院病房。</summary>
    public const string DeWardNo = "DE01.00.019.00";

    /// <summary>DE01.00.021.00 居民健康卡号。</summary>
    public const string DeHealthCardNo = "DE01.00.021.00";

    /// <summary>DE02.01.039.00 姓名（责任者/患者/医师签名通用）。</summary>
    public const string DeName = "DE02.01.039.00";

    /// <summary>DE02.01.040.00 性别代码。</summary>
    public const string DeSex = "DE02.01.040.00";

    /// <summary>DE02.01.005.01 出生日期。</summary>
    public const string DeBirthDate = "DE02.01.005.01";

    /// <summary>DE02.01.026.00 年龄（岁）。</summary>
    public const string DeAgeYears = "DE02.01.026.00";

    /// <summary>DE02.01.018.00 婚姻状况代码。</summary>
    public const string DeMaritalStatus = "DE02.01.018.00";

    /// <summary>DE02.01.025.00 民族。</summary>
    public const string DeEthnicGroup = "DE02.01.025.00";

    /// <summary>DE02.01.015.00 国籍代码。</summary>
    public const string DeNationality = "DE02.01.015.00";

    /// <summary>DE02.01.010.00 电话号码。</summary>
    public const string DePhone = "DE02.01.010.00";

    /// <summary>DE02.01.047.00 邮政编码。</summary>
    public const string DePostalCode = "DE02.01.047.00";

    /// <summary>DE02.01.009.01 省（自治区、直辖市）。</summary>
    public const string DeAddrProvince = "DE02.01.009.01";

    /// <summary>DE02.01.009.02 市（地区、州）。</summary>
    public const string DeAddrCity = "DE02.01.009.02";

    /// <summary>DE02.01.009.03 县（区）。</summary>
    public const string DeAddrCounty = "DE02.01.009.03";

    /// <summary>DE02.01.009.04 乡（镇、街道办事处）。</summary>
    public const string DeAddrTownship = "DE02.01.009.04";

    /// <summary>DE02.01.009.05 村（街、路、弄等）。</summary>
    public const string DeAddrStreet = "DE02.01.009.05";

    /// <summary>DE02.01.009.06 门牌号码。</summary>
    public const string DeAddrHouseNo = "DE02.01.009.06";

    /// <summary>DE02.10.090.00 住院次数。</summary>
    public const string DeAdmissionTimes = "DE02.10.090.00";

    /// <summary>DE02.10.022.00 过敏药物。</summary>
    public const string DeAllergicDrug = "DE02.10.022.00";

    /// <summary>DE02.10.023.00 药物过敏标志。</summary>
    public const string DeAllergyFlag = "DE02.10.023.00";

    /// <summary>DE05.10.024.00 就诊原因（主诉）。</summary>
    public const string DeVisitReason = "DE05.10.024.00";

    /// <summary>DE05.01.024.00 疾病编码（诊断/病理诊断，ICD-10）。</summary>
    public const string DeDiseaseCode = "DE05.01.024.00";

    /// <summary>DE05.01.025.00 疾病名称（诊断/病理诊断）。</summary>
    public const string DeDiseaseName = "DE05.01.025.00";

    /// <summary>DE05.10.119.00 住院者疾病状态代码。</summary>
    public const string DeDiseaseStatus = "DE05.10.119.00";

    /// <summary>DE05.10.148.00 入院情况。</summary>
    public const string DeAdmissionCondition = "DE05.10.148.00";

    /// <summary>DE06.00.017.00 出院日期时间。</summary>
    public const string DeDischargeTime = "DE06.00.017.00";

    /// <summary>DE06.00.092.00 入院日期时间。</summary>
    public const string DeAdmissionTime = "DE06.00.092.00";

    /// <summary>DE06.00.093.00 手术及操作编码。</summary>
    public const string DeSurgeryCode = "DE06.00.093.00";

    /// <summary>DE06.00.094.00 手术及操作名称。</summary>
    public const string DeSurgeryName = "DE06.00.094.00";

    /// <summary>DE06.00.310.00 实际住院天数。</summary>
    public const string DeHospitalDays = "DE06.00.310.00";

    /// <summary>DE06.00.339.00 入院途径代码。</summary>
    public const string DeAdmissionRoute = "DE06.00.339.00";

    /// <summary>DE07.00.007.00 医疗付费方式代码。</summary>
    public const string DePaymentMethod = "DE07.00.007.00";

    /// <summary>DE08.10.013.00 医疗机构名称。</summary>
    public const string DeHospitalName = "DE08.10.013.00";

    /// <summary>DE08.10.026.00 入院科别 / 出院科别 / 科室名称。</summary>
    public const string DeDepartment = "DE08.10.026.00";

    /// <summary>DE08.10.052.00 医疗机构组织机构代码。</summary>
    public const string DeHospitalOrgCode = "DE08.10.052.00";

    /// <summary>DE08.50.022.00 药物名称（关键药物）。</summary>
    public const string DeDrugName = "DE08.50.022.00";

    /// <summary>DE08.50.011.00 药物剂型代码。</summary>
    public const string DeDrugForm = "DE08.50.011.00";

    /// <summary>DE06.00.133.00 药物使用频率。</summary>
    public const string DeDrugFrequency = "DE06.00.133.00";

    /// <summary>DE06.00.134.00 药物使用途径代码。</summary>
    public const string DeDrugRoute = "DE06.00.134.00";

    /// <summary>DE08.50.023.00 药物使用次剂量。</summary>
    public const string DeDrugDose = "DE08.50.023.00";

    /// <summary>DE08.50.024.00 药物使用剂量单位。</summary>
    public const string DeDrugDoseUnit = "DE08.50.024.00";

    /// <summary>DE04.50.001.00 ABO 血型代码。</summary>
    public const string DeAboBloodType = "DE04.50.001.00";

    /// <summary>DE04.10.019.00 新生儿出生体重 / 入院体重（g）。</summary>
    public const string DeBirthWeight = "DE04.10.019.00";

    /// <summary>DE04.30.009.00 辅助检查结果。</summary>
    public const string DeAuxiliaryExam = "DE04.30.009.00";

    /// <summary>DE09.00.104.00 入院病情代码（出院诊断的入院病情评估）。</summary>
    public const string DeAdmissionConditionCode = "DE09.00.104.00";

    /// <summary>DE09.00.108.00 死亡患者尸检标志。</summary>
    public const string DeAutopsyFlag = "DE09.00.108.00";

    /// <summary>DE09.00.103.00 病案质量代码。</summary>
    public const string DeRecordQuality = "DE09.00.103.00";

    /// <summary>DE01.00.005.00 病理号。</summary>
    public const string DePathologyNo = "DE01.00.005.00";

    /// <summary>DE05.10.152.00 损伤中毒的外部原因。</summary>
    public const string DeExternalCause = "DE05.10.152.00";

    /// <summary>DE05.01.078.00 损伤中毒的外部原因疾病编码。</summary>
    public const string DeExternalCauseCode = "DE05.01.078.00";

    /// <summary>DE06.00.095.00 手术及操作日期时间。</summary>
    public const string DeSurgeryDateTime = "DE06.00.095.00";

    /// <summary>DE02.10.026.00 疾病史（含外伤）。</summary>
    public const string DeDiseaseHistory = "DE02.10.026.00";

    /// <summary>DE06.00.174.00 其他医学处置。</summary>
    public const string DeOtherTreatment = "DE06.00.174.00";

    // ═════════════════════════════════════════════════════════════════════
    //  值域代码
    // ═════════════════════════════════════════════════════════════════════

    /// <summary>性别代码：男（GB/T 2261.1）。</summary>
    public const string SexMale = "1";

    /// <summary>性别代码：女（GB/T 2261.1）。</summary>
    public const string SexFemale = "2";

    /// <summary>婚姻状况：未婚。</summary>
    public const string MaritalUnmarried = "10";

    /// <summary>婚姻状况：已婚。</summary>
    public const string MaritalMarried = "20";

    /// <summary>民族代码：汉族（GB 3304）。</summary>
    public const string EthnicHan = "01";

    /// <summary>入院途径：门诊（WS/T 500.32 示例取值域）。</summary>
    public const string AdmissionRouteOutpatient = "01";

    /// <summary>入院途径：急诊。</summary>
    public const string AdmissionRouteEmergency = "02";

    /// <summary>住院者疾病状态：未治愈（示例取值域）。</summary>
    public const string DiseaseStatusUncured = "1";
}
