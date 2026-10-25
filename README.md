# NetOfd - 跨平台 OFD 读写库

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

## 项目结构

```
netofd/
├── src/
│   ├── NetOfd.Core/          # 核心：OFD XML 元素体系 + 基础类型（ST_*/CT_*）
│   ├── NetOfd.Emr/           # 电子病历结构化数据（WS/T 500 共享文档生成 + 标准导出）
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
│   └── NetOfd.Tests/         # 300+ 单元/集成/性能测试
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

`samples/OfdHWriter` 是独立的 OFD-H 生成器（GB/T 48666—2026），输出含 3 页版式内容、
嵌入仿宋字型、心电图附件、质控审核加注、版本快照、自定义标引（机读信息 + 结构化诊疗数据）、
SM2withSM3 国密签名与 GM/T 0099 防夹带保护：

```bash
dotnet run --project samples/OfdHWriter -- <out.ofd> [--work <工作目录>] [--no-sign]
```

退出码：`0` 成功且签名自检通过 / `1` 参数错误 / `2` 生成异常 / `3` 签名自检失败。

## 技术特性

- **跨平台**: 支持 Windows、Linux、macOS（.NET 10；Linux 渲染需安装 CJK 字体）
- **编辑器架构**: zip 展开为内存映射 + XML 直改，链式调用安全（对象 ID 单调递增）
- **版本管理**: 文档级快照（Doc_0/V_N）与历史版本提取
- **国密**: SM2/SM3/SM4 全覆盖，兼容 BouncyCastle 与系统密码库互操作验证
- **健壮性**: 损坏输入有语义明确的异常或结构化错误结果（308 测试保障）

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

## 许可证

Apache License 2.0（与 OFDRW 保持一致）
