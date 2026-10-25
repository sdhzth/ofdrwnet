namespace OfdHWriter;

/// <summary>演示病历数据（虚构患者，仅用于合规演示）。</summary>
internal sealed class EmrCase
{
    public string Hospital = "xx市第三人民医院";
    public string PatientName = "张建国";
    public string Sex = "男";
    public int Age = 68;
    public string BirthDate = "1958-03-12";
    public string Nation = "汉";
    public string Marriage = "已婚";
    public string Occupation = "退休职工";
    /// <summary>身份证号（已脱敏：整串以 x 占位，保留 18 位长度与格式可读性）。</summary>
    /// <remarks>
    /// 采用全字符替换而非局部掩码：病历中「出生日期」为必填项且明文可见，
    /// 若仅掩码身份证内的出生日期段，可用文件内已知出生日期回填还原，脱敏形同虚设；
    /// 全串占位后无法反推，地区码亦不外泄。
    /// </remarks>
    public string IdCard = "xxxxxxxxxxxxxxxxxx";
    /// <summary>联系电话（已脱敏：整串以 x 占位，保留 11 位长度）。</summary>
    public string Phone = "xxxxxxxxxxx";
    public string ContactPerson = "张卫东（之子）";
    public string Address = "山东省xx市通济街道新兴路 28 号 3 号楼";
    public string MedicalRecordNo = "78420931";
    public string AdmissionNo = "20261018007";
    public string AdmissionTime = "2026-10-18 09:24";
    public string Dept = "心血管内科";
    public string WardBed = "3 病室 15 床";
    public string Doctor = "李明";
    public string ChiefDoctor = "赵鸿";
    public string Nurse = "王芳";
    public string ChiefComplaint = "发作性胸闷、气短 3 天，加重 1 天。";
    public string PresentIllness = "患者 3 天前于活动后出现胸闷、气短，伴左肩背部酸胀不适，持续约 5～10 分钟，"
        + "休息后可缓解，未予特殊处理。1 天前症状加重，静息状态下亦有发作，含服硝酸甘油后约 3 分钟缓解，"
        + "为求进一步诊治来院，门诊以“冠状动脉粥样硬化性心脏病”收入院。自发病以来，患者神志清楚，精神尚可，"
        + "食欲、睡眠可，大、小便正常，体重无明显变化。";
    public string PastHistory = "高血压病史 10 年，最高 180/100 mmHg，规律服用苯磺酸氨氯地平片 5 mg qd，"
        + "血压控制在 140/90 mmHg 左右；2 型糖尿病病史 5 年，口服二甲双胍 0.5 g bid。"
        + "否认肝炎、结核等传染病史，否认手术、外伤及输血史，否认食物、药物过敏史。预防接种史随当地。";
    public string PersonalHistory = "生于原籍，久居本地。吸烟 30 年，平均约 10 支/日，已戒烟 2 年；偶有饮酒。"
        + "否认疫区旅居史及毒物、放射性物质接触史。";
    public string FamilyHistory = "父亲有高血压病史，否认冠心病、糖尿病家族史。";
    public string PhysicalExam = "T 36.5 ℃，P 78 次/分，R 18 次/分，BP 152/94 mmHg。神志清楚，自主体位，查体合作。"
        + "口唇无发绀，颈静脉无怒张，肝颈静脉回流征阴性。双肺呼吸音清，未闻及干、湿性啰音。心界不大，"
        + "心率 78 次/分，律齐，各瓣膜听诊区未闻及病理性杂音。腹软，无压痛、反跳痛，肝脾肋下未触及，双下肢无水肿。";
    public string EcgText = "窦性心律，HR 72 bpm，PR 间期 160 ms，QRS 时限 92 ms，QT 间期 400 ms，QTc 435 ms；"
        + "V1～V6 导联 ST 段压低 0.05～0.15 mV，T 波低平、倒置。";
    public string EcgConclusion = "窦性心律；心肌缺血性改变（建议结合临床）。";
    public string PrimaryDiagnosis = "冠状动脉粥样硬化性心脏病　不稳定型心绞痛（I20.0）";
    public string OtherDiagnoses = "高血压病 3 级（极高危）（I10）；2 型糖尿病（E11.9）";
    public string TreatmentPlan = "① 完善血常规、生化、凝血、心肌损伤标志物及冠脉 CTA 等检查；"
        + "② 予抗血小板（阿司匹林肠溶片 100 mg qd + 硫酸氢氯吡格雷片 75 mg qd）、调脂稳定斑块"
        + "（阿托伐他汀钙片 20 mg qn）、扩冠（单硝酸异山梨酯片 40 mg qd）治疗；③ 控制血压、血糖；"
        + "④ 若冠脉 CTA 提示重度狭窄，建议行冠脉造影检查，必要时行 PCI 治疗。";
    public string[] Keywords = { "电子病历", "住院病案首页", "入院记录", "不稳定型心绞痛", "心电图" };
}
