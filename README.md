# NetOfd - 跨平台 OFD 读写库

[![license](https://img.shields.io/badge/license-MIT-blue)](./LICENSE) [![license](https://img.shields.io/badge/ported%20code-Apache--2.0-blue)](./LICENSE-Apache-2.0)

**[简体中文](README.md) | [English](README.en.md)**

## 概述

NetOfd 是 [OFDRW (OFD Reader & Writer)](https://github.com/ofdrw/ofdrw) v2.3.8 的 .NET 跨平台移植版本，
实现中国国家标准 **GB/T 33190-2016** 电子文件存储与交换格式版式文档（OFD）的完整读写功能，
覆盖数字签名（GB/T 35275 / SES_V1-V5）、国密应用（GM/T 0099-2020）、OFD-A 归档（GB/T 42133-2022）等配套标准。

### 超越 Java 版的能力

- **DrawParam 样式级联**：按 ParentID 链解析继承（线宽/颜色/填充/透明度），渲染端与 Java 版一致
- **裁剪链（Clips）**：模型解析、Graphics2D 记录、渲染器 ClipPath 全链路支持
- **批量编辑会话** `OfdEditSession`：注释/版本/水印等多次编辑一次落盘；`OfdDocEditor` 支持并行导出
- **真实语料护栏**：67 个 Java 测试文档全量 打开/渲染/提取 冒烟测试（100% 通过）
- **文字装饰**：下划线/删除线/加粗/斜体/段落对齐完整落盘

## OFD-H 电子病历支持（GB/T 48666—2026）

`NetOfd.Emr` 模块提供 **GB/T 48666—2026《电子病历版式文档技术要求》** 的落地能力：
作为库 API 生成符合 OFD-H 要求的版式文档，并在其中嵌入机读的结构化诊疗数据。
`samples/OfdHWriter` / `samples/OfdHExporter` 两个命令行工具是其封装。

### 覆盖的标准体系

| 标准 | 含义 | 在 NetOfd 中的落地 |
|---|---|---|
| **GB/T 48666—2026** | 电子病历版式文档技术要求 | 主标准：DocType 标识、包结构、元数据、标引、附件、版本、加注、签名、防夹带 |
| **GB/T 33190—2016** | 电子文件存储与交换格式 版式文档 | OFD 基础格式（`Doc_0` 单文档、`ofd:` 命名空间前缀、ZIP 线性化） |
| **WS/T 500.32—2016** | 电子病历共享文档规范 第 32 部分：住院病案首页 | HL7 CDA R2 `ClinicalDocument` 构造与解析（`Wst500Builder`） |
| **WS 445.10—2014** | 电子病历基本数据集 第 10 部分：住院病案首页 | 56 个 DE 码数据元（`Wst500Codes`）与结构化导出 |
| **WS/T 482—2016** | 卫生信息共享文档编制规范 | 共享文档体结构约束 |
| **GB/T 35275** | 信息安全技术 数字签名格式规范 | SM2withSM3 国密签名 / 验签（`OfdSigner`） |
| **GM/T 0099—2020** | 电子文档安全技术 签名存证与防抵赖 | §7.4.3 防夹带 / 完整性保护（`OfdIntegrity`） |

引用的代码表：GB/T 2261.1（生理性别）、GB/T 2261.2（婚姻状况）、GB 3304（民族类别）、
ISO 3166-1（国籍）、ICD-10（疾病分类）、ICD-9-CM-3（手术操作）、LOINC（章节编码）。

### 条款实现对照

`src/NetOfd.Emr/OfdHDocumentGenerator.cs` 顶部注释逐条记录了生成流程与标准条款的对应关系：

| 条款 | 要求 | 实现 |
|---|---|---|
| 6.1.2 | 单文档承载（`Doc_0`，OFD 格式） | `OFDDoc` 单文档 |
| 6.1.3 | ZIP 6.2 + Deflate、线性化（`OFDEntries.xml` 索引） | `RepackLinearized` |
| 6.2 | 包内规范目录结构（表 1） | `NormalizeStructure` |
| 7.1 a) | `DocType="OFD-H"` | `OFRoot.OfdDocType` |
| 7.1 c) | 元数据 DocInfo（患者 / 责任者 / 时间 / 关键词） | `ConfigureDocInfo` |
| 7.2 b) | 字型数据嵌入（仿宋 FontFile） | `EmbedFonts` |
| 7.2 c) | 图像资源嵌入（DocumentRes + `Res/`） | `Img` 元素 |
| 7.3 c) | 结构化数据以自定义标引存储 | `OfdTagger.AddCustomTag` ×2 |
| 7.4 | 附件（`Attachs/Attachments.xml` + `Attach_N.png`） | `OfdAttacher` + 规范化 |
| 7.5 | 机读信息（文件头 / 元数据 / 标识信息，UTF-8） | 机读信息 XML 标引 |
| 7.6 | 版本快照（历史版本全量保留） | `OfdVersioner` |
| 3.12 / 5.3.2 e) | 加注记录（审核批注） | `OfdAnnotator` |
| 6.3 / 8 c) d) e) | SM2 + SM3 国密签名，保护范围除注释列表、签名列表外全部内容 | `OfdSigner` + `GBT35275DSContainer` |
| GM/T 0099 §7.4.3 | 防夹带 / 完整性保护 | `OfdIntegrity` + `GmProtectSigner` |

### 生成器：NetOfd.Emr.OfdHDocumentGenerator

```csharp
using NetOfd.Emr;

var result = OfdHDocumentGenerator.Generate(new OfdHOptions
{
    OutputPath = "病历.ofd",
    CaseData   = myCaseData,          // 传入 EmrCaseData；为 null 时使用内置演示数据
    // FontPath = "/path/to/chinese.ttf",   // 不指定则按平台自动探测中文字型
});

if (!result.Success)
{
    Console.Error.WriteLine(result.Error);
    return;
}

Console.WriteLine($"{result.PageCount} 页 / {result.SizeBytes} 字节 / 签名 {result.SignatureCount} 个");
Console.WriteLine($"验签全部通过: {result.AllSignaturesValid}，完整性: {result.IntegrityVerified}");
```

产出一份 **3 页 A4 纵向**（边距 20mm）OFD-H 版式文档：

| 页 | 内容 |
|---|---|
| 1 | 住院病案首页（基本信息 6 行 / 就诊信息 6 行 / 诊断 2 行） |
| 2 | 入院记录（一般项目、主诉、现病史、既往史、个人史 + 家族史、体格检查） |
| 3 | 辅助检查（心电图文字 + 图像 + 图注）、初步诊断与诊疗计划、医师签名、日期 |

文档同时包含：嵌入中文字型、心电图图像资源、内部附件、质控审核加注、版本快照、
机读信息标引 + WS/T 500 共享文档标引、SM2withSM3 国密签名、GM/T 0099 防夹带保护。

`OfdHOptions` 常用参数：

| 参数 | 默认值 | 说明 |
|---|---|---|
| `OutputPath` | 必填 | 输出 .ofd 路径 |
| `CaseData` | 内置演示数据 | 病历数据（`EmrCaseData`） |
| `SkipSignature` | `false` | 跳过签名与防夹带（仅调试结构用） |
| `DocumentId` | 自动生成 GUID | 文档标识 DocID；传入固定值可获得可复现输出 |
| `FontPath` | 按平台自动探测 | 待嵌入的中文字型文件 |
| `SkipFontEmbeddingWhenMissing` | `true` | 找不到字型时记录警告并跳过（设为 `false` 则抛异常） |
| `SigningTime` | 当前时间 | 签名时间 |
| `Log` | `Console.Out` | 日志输出目标 |

### 命令行封装

```bash
dotnet run --project samples/OfdHWriter -- <out.ofd> [--work <工作目录>] [--font <字型路径>] [--no-sign] [--no-preview]
```

退出码：`0` 成功且签名自检通过 / `1` 参数错误 / `2` 生成异常 / `3` 签名自检失败。

### 结构化数据：NetOfd.Emr 模块

| 类 | 职责 |
|---|---|
| `OfdHDocumentGenerator` | OFD-H 版式文档生成器（本节上文） |
| `OfdHOptions` / `OfdHResult` | 生成参数与结果对象 |
| `EmrCaseData` | 病历数据模型（患者、就诊、诊断、医嘱等） |
| `EmrStructuredData` | 版式页面与机读数据的桥梁：同一份字段既渲染进 OFD-H 版式，又以标准 DE 码写入结构化标引 |
| `Wst500Builder` | 把 `EmrStructuredData` 序列化为 WS/T 500.32 共享文档（HL7 CDA R2 `ClinicalDocument`） |
| `Wst500Codes` | 编码常量字典：13 个章节 LOINC 码、56 个数据元 DE 码、11 个 OID 根、11 个值域代码 |
| `EmrStructuredExporter` | 从 OFD-H 读回自定义标引并还原为标准结构化数据（JSON / 文本） |

```csharp
using NetOfd.Emr;

var data = new EmrStructuredData
{
    PatientName = "张建国",
    SexCode     = Wst500Codes.SexMale,   // "1"
    BirthDate   = "19651120",
};
data.Diagnoses.Add(new EmrDiagnosis { Name = "不稳定型心绞痛", Code = "I20.0" });

// 构造 WS/T 500.32 共享文档（可作为 OFD-H 自定义标引的载荷）
byte[] sharedDoc = Wst500Builder.BuildBytes(data);

// 从 OFD-H 反向导出结构化数据
var result = EmrStructuredExporter.Export("病历.ofd");
Console.WriteLine(EmrStructuredExporter.ToJson(result));
```

`Wst500Builder` 生成的 `ClinicalDocument` 覆盖 10 个章节（LOINC 编码），
`entry` 内嵌于 `section` 之下，观测值分 `PQ`（数值 + 单位）/ `CD`（编码值）/ `ST`（文本）三类。

### 导出器：samples/OfdHExporter

```bash
dotnet run --project samples/OfdHExporter -- <in.ofd> [--out <输出目录>] [--format json|xml|text|all]
```

| 产物 | 内容 |
|---|---|
| `<base>_结构化数据.json` | WS 445.10 扁平数据元（`DE 码 → 值`）+ 诊断数组 |
| `<base>_WS_T500共享文档.xml` | WS/T 500.32 共享文档原文 |
| `<base>_结构化数据.txt` | 可读文本汇总 |

退出码：`0` 成功 / `1` 参数错误或输入不存在 / `2` 导出异常 / `3` 未找到结构化数据标引。

### 测试覆盖

OFD-H 相关自动化测试位于 `tests/NetOfd.Tests/Emr/`：

| 测试类 | 用例数 | 覆盖内容 |
|---|---|---|
| `OfdHDocumentGeneratorTest` | 18 | 生成全流程端到端：3 页结构、DocType、DocID、包内目录结构（表 1）、线性化顺序、字型嵌入、版本快照、自定义标引 ×2、机读信息三要素、结构化数据反向导出、CDA 结构、附件字节一致、加注、签名验签、防夹带完整性、三页文本提取 |
| `OfdHGeneratorOptionTest` | 5 | 跳过签名、不存在字型路径（跳过 / 抛异常两种策略）、自定义病历数据、自动 DocID |
| `Wst500SharedDocumentTest` | 12 | WS/T 500.32 共享文档常量自洽性、CDA 结构约束、标引写入读回闭环 |

### 已知边界

诚实列出当前实现的局限，便于评估可用范围：

- **章节覆盖 10/13**：已定义但未实现的 3 个章节为转科记录（`28570-0`）、出院诊断（`56846-4`）、行政管理（`32473-6`）。
- **数据元覆盖 33/56**：56 个 DE 码常量中约 23 个尚未写入共享文档（用药、血型、出生体重、尸检标志、病理号、外因等）。
- **部分字段只入版式、不入机读**：`EmrStructuredData` 中的就诊类字段（年龄、住院次数、入出院时间、入院途径、入出院科室、病区、付费方式、责任护士、编码医师等）目前仅出现在版式页面，未进入结构化标引。
- **仅覆盖 1 个共享文档 part**：当前只实现 WS/T 500.32（住院病案首页），WS/T 500 系列其余 part 未涉及。
- **字型嵌入依赖系统中文字体**：未显式指定 `FontPath` 时按平台探测仿宋/中文字体；探不到时按选项记录警告并跳过（此时不满足第 7.2 b 条），生产环境应显式提供字型文件。

## 项目结构

```
netofd/
├── src/
│   ├── NetOfd.Core/          # 核心：OFD XML 元素体系 + 基础类型（ST_*/CT_*）
│   ├── NetOfd.Emr/           # OFD-H 电子病历（GB/T 48666—2026）：版式文档生成 + WS/T 500 共享文档 + 标准导出
│   ├── NetOfd.Pkg/           # 虚拟容器 + ZIP 打包
│   ├── NetOfd.Font/          # 字体管理与度量
│   ├── NetOfd.Reader/        # OFD 解析器 / 文本提取 / 关键词定位
│   ├── NetOfd.Layout/        # 布局引擎 + 编辑器（注释/附件/自定义标/版本）
│   ├── NetOfd.Graphics2D/    # java.awt 风格绘图接口
│   ├── NetOfd.Converter/     # OFD → PNG/SVG/HTML 渲染
│   ├── NetOfd.Sign/          # 数字签名/电子签章（SES_V1/V4/V5，GB/T 35275）
│   ├── NetOfd.Crypto/        # 国密文档加密解密（SM4/KDF，口令与证书方案）
│   ├── NetOfd.Tool/          # 文档工具（合并/混合/删页）
│   ├── NetOfd.Archive/       # OFD-A 归档合规检查与转换
│   ├── NetOfd.Full/          # 全模块聚合
│   └── NetOfd/               # 统一入口（元包）
├── tests/
│   └── NetOfd.Tests/         # 950+ 单元/集成/性能测试
├── samples/                  # 示例项目（写入/签名/渲染转换/OFD-H 生成/结构化导出）
│   └── OfdHWriter/           # GB/T 48666—2026 电子病历 OFD-H 生成器
└── Directory.Build.props     # 统一构建属性
```

## 快速开始

### 创建 OFD 文件

```csharp
using NetOfd.Layout;
using NetOfd.Layout.Element;

using var doc = new OFDDoc("output.ofd");
doc.AddPage();
doc.Add(new Paragraph().SetWidth(100)
    .Add(new Span("你好，OFD 世界！").SetFontSize(5d).SetBold()));
```

### 读取与文本提取

```csharp
using NetOfd.Reader;
using NetOfd.Converter; // ContentExtractor 所在命名空间以实际为准

using var reader = new OFDReader("input.ofd");
var pages = reader.GetPages().ToList();
foreach (var text in ContentExtractor.ExtractAllPagesText(pages))
    Console.WriteLine(text);
```

### 页面编辑链（注释 → 附件 → 自定义标 → 版本快照）

```csharp
using NetOfd.Layout.Edit;

// 方式一：逐次编辑（每次返回新字节）
var edited = OfdAnnotator.AddAnnotation(ofdBytes, 1,
    new Annotation(10, 20, 40, 16, AnnotType.Path, drawer));
edited = OfdAttacher.AddAttachment(edited, new Attachment("合同扫描件", "scan.pdf"));
edited = OfdTagger.AddCustomTag(edited, "urn:example:keyword", tagBytes);
OfdVersioner.AddVersion(edited, "定稿", out _);      // 留存版本
var old = OfdVersioner.ExtractVersion(edited, "v1"); // 还原历史版本

// 方式二：批量编辑会话（多次操作单次落盘，大批量场景快数倍）
using (var session = OfdEditSession.Open(ofdBytes))
{
    for (int p = 1; p <= session.PageCount; p++)
        session.AddAnnotation(p, annotation);
    session.AddVersion("批注定稿");
    ofdBytes = session.Save();   // 落盘必须显式调用
}
```

### 数字签名与验签

```csharp
using NetOfd.Sign;

var signed = OfdSigner.Sign(ofdBytes, signerKeyPair, signerCert,
    "2026-08-23 10:00:00", seal: sealAsn1, isSeal: true, version: 4);

var result = OfdValidator.Verify(signed);
Console.WriteLine(result.AllValid ? "全部有效" : result.Error);
```

### 渲染为图像

```csharp
using NetOfd.Converter;

using var exporter = new ImageExporter("input.ofd", outDir, "png", ppm: 15);
var pngPaths = exporter.Export();   // 每页一个 PNG
```

## 命令行工具

```bash
dotnet run --project tests/NetOfd.TestConsole -- info   <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- verify <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- text   <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- render <file.ofd> <outDir> [ppm]
dotnet run --project tests/NetOfd.TestConsole -- merge  <out.ofd> <in1.ofd> <in2.ofd> ...
dotnet run --project tests/NetOfd.TestConsole -- sample <out.ofd>
dotnet run --project tests/NetOfd.TestConsole -- sign <in.ofd> <out.ofd> --p12 <cert.p12> --pwd <密码> [--seal <seal.der>] [--version 1|4|5]
dotnet run --project tests/NetOfd.TestConsole -- protect <in.ofd> <out.ofd> --user <用户名> --pwd <口令>
dotnet run --project tests/NetOfd.TestConsole -- decrypt <in.ofd> <out.ofd> [--user <用户名>] --pwd <口令>
dotnet run --project tests/NetOfd.TestConsole -- ofda-check <file.ofd>
dotnet run --project tests/NetOfd.TestConsole -- ofda-convert <in.ofd> <out.ofd> [--user <u> --pwd <p>]
```

### 生成 OFD-H 电子病历版式文档

`samples/OfdHWriter` 是 `NetOfd.Emr.OfdHDocumentGenerator` 的命令行封装，输出含 3 页版式内容、
嵌入中文字型、心电图附件、质控审核加注、版本快照、自定义标引（机读信息 + 结构化诊疗数据）、
SM2withSM3 国密签名与 GM/T 0099 防夹带保护：

```bash
dotnet run --project samples/OfdHWriter -- <out.ofd> [--work <工作目录>] [--font <字型路径>] [--no-sign] [--no-preview]
```

退出码：`0` 成功且签名自检通过 / `1` 参数错误 / `2` 生成异常 / `3` 签名自检失败。

> 完整条款对照、库 API、结构化数据与测试覆盖见 [OFD-H 电子病历支持](#ofd-h-电子病历支持gbt-486662026)。

## 技术特性

- **跨平台**: 支持 Windows、Linux、macOS（.NET 10；Linux 渲染需安装 CJK 字体）
- **编辑器架构**: zip 展开为内存映射 + XML 直改，链式调用安全（对象 ID 单调递增）
- **版本管理**: 文档级快照（Doc_0/V_N）与历史版本提取
- **国密**: SM2/SM3/SM4 全覆盖，兼容 BouncyCastle 与系统密码库互操作验证
- **健壮性**: 损坏输入有语义明确的异常或结构化错误结果（950+ 测试保障）

## 技术栈映射

| Java (OFDRW) | .NET (NetOfd) |
|---|---|
| dom4j `Element` | `System.Xml.Linq.XElement` |
| zip4j | `System.IO.Compression.ZipArchive` |
| BouncyCastle SM2/SM3/SM4 | `BouncyCastle.Cryptography` |
| `java.awt.Graphics2D` | SkiaSharp（渲染）/ `NetOfd.Graphics2D` 接口层 |
| Maven multi-module | .NET Solution + csproj |

## NuGet 依赖

| 包 | 用途 |
|---|---|
| `BouncyCastle.Cryptography` | SM2/SM3/SM4 国密算法 |
| `SkiaSharp` | 跨平台渲染（Converter/Graphics2D） |

## 开发与发布工作流

维护时本项目分为两份工作区，内容由脚本自动同步，无需手工拷贝：

| 目录 | 角色 |
|---|---|
| `<仓库根>/` | **开发态**：日常改动、构建、测试都在这里 |
| `<仓库根>/github-upload/` | **发布态**：git 仓库，最终推送到 GitHub |

### 更新到 GitHub

```bash
# 1. 在开发态改代码
# 2. 一条命令完成「同步 + 提交 + 推送」
python tools/sync-publish.py --apply --commit "feat: 你的提交信息" --push
```

| 参数 | 作用 |
|---|---|
| （无参数） | 预览：报告将新增 / 修改 / 删除哪些文件，不做任何改动 |
| `--apply` | 实际执行同步 |
| `--check` | 仅检查是否已同步（有差异时退出码 1，可用于 CI） |
| `--commit "msg"` | 同步后自动 `git add -A` + `git commit` |
| `--push` | 同步后提交并推送到 `origin/master` |

同步规则：

- **白名单**：`src/` `tests/` `samples/` `tools/` `.github/` + `netofd.slnx` `Directory.Build.props` `Directory.Packages.props` `README.md` `README.en.md`
- **排除规则直接读取发布态的 `.gitignore`**（唯一来源，不重复维护），因此构建产物与 AI 会话临时产物（`_probe/`、`render_dump/`、`*.log` 等）不会被上传
- **删除同步**：文件从开发态删除后，发布态的对应文件会被自动删除并提交
- **发布态专属文件**（`.gitignore`、`LICENSE`、`LICENSE-Apache-2.0`、`NOTICE`）不会被覆盖，缺失时在报告中提示

### 发布 NuGet 包

```bash
# 打包（14 个 nupkg + 14 个 snupkg）
dotnet pack netofd.slnx -c Release -o artifacts/nupkg

# 发布（先在 https://www.nuget.org/account/apikeys 生成 API Key）
export NUGET_API_KEY=你的密钥
./tools/publish-nuget.sh              # 按依赖顺序推送
./tools/publish-nuget.sh --dry-run    # 仅做前置检查
```

`publish-nuget.sh` 按依赖拓扑顺序推送（`NetOfd.Core` → … → `NetOfd.Full`）——
nuget.org 会拒绝依赖尚未发布的包，因此顺序不能乱。脚本还会检查版本号一致性、
包齐备性、README 是否已打包，并对「版本已存在」自动跳过。

## 许可证

本项目采用**双协议**：

| 范围 | 协议 | 文件 |
|---|---|---|
| NetOfd 自有代码（如 `NetOfd.Emr` 等非移植模块） | MIT License | [LICENSE](LICENSE) |
| 自 OFDRW 移植而来的代码 | Apache License 2.0 | [LICENSE-Apache-2.0](LICENSE-Apache-2.0) |

各 NuGet 包的 `PackageLicenseExpression` 与此保持一致：

| 包 | 声明 |
|---|---|
| `NetOfd.Emr` | `MIT` |
| `NetOfd.Core` / `Pkg` / `Font` / `Reader` / `Layout` / `Graphics2D` / `Converter` / `Sign` / `Crypto` / `Tool` / `Archive` | `Apache-2.0` |
| `NetOfd` / `NetOfd.Full`（聚合包） | `Apache-2.0 OR MIT` |

NetOfd 是 [OFDRW](https://github.com/ofdrw/ofdrw)（Copyright (c) 2020 Quan guanyu，Apache-2.0）
的 .NET 移植版本。根据 Apache-2.0 §4，移植而来的部分保留原协议、声明修改并保留版权声明，
详见 [NOTICE](NOTICE)。

> 根目录 `LICENSE` 为 MIT 协议标准原文（便于 GitHub 自动识别协议标签），
> 上述协议适用范围划分以 `NOTICE` 与本表为准。

本项目是独立实现，与 OFDRW 项目无隶属或背书关系。
