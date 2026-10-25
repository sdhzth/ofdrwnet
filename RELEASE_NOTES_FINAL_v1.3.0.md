<!-- 发布操作提示（本注释不会显示在 Release 页面）：
     1) 打开 https://github.com/sdhzth/ofdrwnet/releases/edit/v1.3.0
     2) 先把 package-dependency-graph.png 拖到页面下方的「Attach binaries… / 附加二进制文件」区上传
     3) 再把本文件全部内容复制粘贴进描述框
     4) 下方图片链接指向 Release 附件，上传后才会显示 -->

# NetOfd v1.3.0

## 首次发布到 NuGet.org

本版本**首次将 NetOfd 正式发布到 NuGet.org**，共 **14 个包**（含 14 个符号包 snupkg），
采用 NuGet Trusted Publishing（OIDC 无密钥）发布，包所有者 `sdhzsmart`。

NetOfd 是 [OFDRW (OFD Reader & Writer)](https://github.com/ofdrw/ofdrw) v2.3.8 的
**.NET 跨平台移植版本**，实现中国国家标准 **GB/T 33190-2016** 电子文件存储与交换格式
版式文档（OFD）的完整读写，并覆盖数字签名（GB/T 35275 / SES_V1-V5）、国密应用
（GM/T 0099-2020）、OFD-A 归档（GB/T 42133-2022）等配套标准。

---

## 包清单（14 个，按依赖顺序）

| 包 ID | 说明 |
|---|---|
| `NetOfd.Core` | OFD 基础类型、XML 元素模型、文档结构 |
| `NetOfd.Font` | 字体解析、度量与嵌入 |
| `NetOfd.Pkg` | 包结构（ZIP 线性化、`OFDEntries.xml` 索引、`RepackLinearized`） |
| `NetOfd.Reader` | OFD 读取与解析（含注释原样/剥离两级容错） |
| `NetOfd.Layout` | 版式布局与坐标变换 |
| `NetOfd.Graphics2D` | 2D 图形绘制记录与渲染抽象 |
| `NetOfd.Converter` | 转换器（渲染 / 导出，基于 SkiaSharp） |
| `NetOfd.Crypto` | 国密算法与加密（GM/T 0099-2020） |
| `NetOfd.Sign` | 数字签名（GB/T 35275，SM2withSM3） |
| `NetOfd.Tool` | 命令行工具集 |
| `NetOfd.Archive` | OFD-A 长期归档（GB/T 42133-2022） |
| `NetOfd.Emr` | **OFD-H 电子病历**（GB/T 48666—2026，详见下文） |
| `NetOfd` | 聚合包 —— 引用全部 12 个子模块包 |
| `NetOfd.Full` | 聚合包 —— 引用全部 12 个子模块包（与 `NetOfd` 集合一致） |

> `NetOfd` 与 `NetOfd.Full` 均聚合全部 12 个子模块，按需选择其一即可。

```bash
# 聚合包（含全部 12 个子模块）
dotnet add package NetOfd

# 聚合包（含全部 12 个子模块，集合与 NetOfd 一致）
dotnet add package NetOfd.Full
```

### 包依赖关系图

![NetOfd 包依赖关系图](https://github.com/sdhzth/ofdrwnet/releases/download/v1.3.0/package-dependency-graph.png)

---

## 相对 v1.2.0 的变更

代码功能层面 v1.2.0 已齐备；v1.3.0 主要完成**发布闭环**与一处**打包修复**：

- **首次发布到 NuGet.org**：配置 NuGet Trusted Publishing（OIDC 无密钥交换），
  14 个包 + 14 个符号包按依赖拓扑顺序推送，含 `--skip-duplicate` 防重。
- **修复 `NetOfd.Core` 包版本错位**：移除 csproj 内硬编码的 `<Version>1.2.0</Version>`，
  改为继承 `Directory.Build.props` 的 `VersionPrefix`，避免升级后 Core 包名仍停留在旧版本。
- **发布自动化**：新增 `tools/sync-publish.py`（开发态→发布态白名单同步）、
  `tools/publish-nuget.sh`（依赖顺序发布），发布工作流绑定受保护的 `release` 环境并支持
  `dry_run` 演练模式与版本一致性门禁。
- **CI 加固**：修复长期为红的 CI（Java 语料冒烟测试改为多来源解析、CI 拉取真实语料），
  Windows / Ubuntu 双平台全绿。
- **文档**：发布流程与 NuGet 配置写入中英文 README。

---

## 核心能力

### 超越 Java 版的能力
- **DrawParam 样式级联**：按 ParentID 链解析继承（线宽 / 颜色 / 填充 / 透明度），与 Java 版渲染一致。
- **裁剪链（Clips）**：模型解析、Graphics2D 记录、渲染器 ClipPath 全链路支持。
- **批量编辑会话** `OfdEditSession`：注释 / 版本 / 水印等多次编辑一次落盘；`OfdDocEditor` 支持并行导出。
- **真实语料护栏**：67 个 Java 测试文档全量 打开 / 渲染 / 提取 冒烟测试（100% 通过）。
- **文字装饰**：下划线 / 删除线 / 加粗 / 斜体 / 段落对齐完整落盘。

### OFD-H 电子病历支持（GB/T 48666—2026）
`NetOfd.Emr` 模块作为**库 API** 生成符合 OFD-H 要求的版式文档，并嵌入机读的结构化诊疗数据
（`samples/OfdHWriter` / `samples/OfdHExporter` 为命令行封装）：

- 覆盖 GB/T 48666、WS/T 500.32、WS 445.10、WS/T 482、GB/T 35275、GM/T 0099 等标准体系；
- 条款实现对照（DocType 标识、包结构、元数据、字体/图像嵌入、自定义标引、附件、机读信息、
  版本快照、质控加注、SM2+SM3 国密签名、防夹带完整性保护）；
- 内置 `EmrCaseData` 病历模型、`Wst500Builder`（HL7 CDA R2 `ClinicalDocument`）、
  `Wst500Codes`（56 个 DE 码 + 13 章节 LOINC + 11 OID 根）、`EmrStructuredExporter`（读回标引还原结构化数据）。

---

## 验证

- 单元测试：**954 个**全绿（含 23 个 OFD-H 端到端测试）。
- 真实语料护栏：67 个 Java 测试文档全量 打开 / 渲染 / 提取 冒烟测试 100% 通过。

## 许可证

- 整体：MIT（见 `LICENSE`）。
- 移植自 OFDRW 的代码：保留 Apache-2.0 归属（见 `LICENSE-Apache-2.0` / `NOTICE`）。
