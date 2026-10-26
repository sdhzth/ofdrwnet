<!-- Release steps (this comment is hidden on the Release page):
     1) Open https://github.com/sdhzth/ofdrwnet/releases/edit/v1.3.2
     2) Attach the 14 .nupkg files (and optionally the 14 .snupkg) from
        E:\电子病历\netofd\github-upload\artifacts\pack-1.3.2
     3) Copy & paste the content of this file into the description box -->

# NetOfd v1.3.2

## 补丁版本：修复编辑模式保存丢失二进制资源，并加入发包门禁

本次为**补丁版本（patch）**，修复一个会导致数据损坏的缺陷，并为发包流程加装自动门禁。

### 🔧 修复：编辑模式保存会丢弃全部二进制资源

**现象**：用 `OFDDoc.OpenEdit(...)` 打开既有 OFD 文档、追加内容后 `Save(...)`，
保存结果中**所有二进制资源整体消失**——嵌入字体（`Res/Font_FangSong.ttf`）、
嵌入图像（`Res/8.png`）、附件（`Attachs/Attach_1.png`）、签名值
（`Signs/Sign_1/SignedValue.dat`）以及版本快照内的同名资源；而 XML 类文件
（Document.xml / Content.xml / CustomTag_N.xml / Signatures.xml 等）全部正常保留。

**根因**：`VirtualContainer.HydrateFromPhysical()` 在把磁盘上的既有文件补进内存缓存时，
对 XML 用 `GetFile()`（**会**写入 `FileCache`），对二进制却用 `GetRaw()`——
而 `GetRaw()` **只从磁盘读取、不写入 `RawFiles` 缓存**；随后 `WriteToZip()` 只遍历
`RawFiles` 写二进制，于是这些资源「读到了却从未进缓存」，写盘时被整体丢弃。

**修复**：改用 `AddRaw(name, File.ReadAllBytes(path))`，确保二进制文件真正进入 `RawFiles`。

**影响面**：该缺陷影响所有「编辑既有 OFD 再保存」的路径（追加页、插入页、编辑会话等），
属于**静默数据损坏**（保存成功、文件可打开，但资源没了）。修复后原包条目一个不少。

### 🛡️ 新增：发包门禁（CI 自动校验）

新增 `tools/verify-packages.py`，并在 `Publish NuGet` 工作流的 **Pack 之后、换取临时 Key 之前**
插入 `Verify package set (gate)` 步骤，门禁校验：

1. 期望的 **14 个包**（Core → Font → Pkg → Reader → Sign → Crypto → Archive → Layout →
   Graphics2D → Converter → Emr → Tool → NetOfd → NetOfd.Full）齐备；
2. 目录中**不存在版本错位**的包（如某个 csproj 硬编码了旧 `<Version>` 而产出 `1.2.0` 的包名）；
3. `.nupkg` 数量正好为 14；
4. **符号包与 nupkg 一一配对**（同名同版本）；
5. **双保险**：逐个读取包内 `.nuspec`，校验其 `<id>` / `<version>` 与文件名一致。

门禁不过即 `exit 1` 中止，**不会推送任何包**，也不会浪费那张一次性 NuGet API Key。
> 背景：v1.3.0 发版时 `NetOfd.Core` 硬编码了 `<Version>1.2.0</Version>`，产出
> `NetOfd.Core.1.2.0.nupkg`，而发布脚本找的是 `1.3.0` → 缺包失败。本门禁正是为拦截此类问题。

### 🧪 新增测试

- `tests/NetOfd.Tests/Emr/OfdHPageInsertTest.cs`：在**已生成（含国密签名）的 OFD-H 电子病历文档**
  中，用 `VirtualPage.SetPageNum(2)` 插入一个固定版式「附页」，验证：
  - 页数 3 → 4，插入页落在第 2 位，原三页内容与位置一一对应；
  - OFD-H 专有要素不受影响：`DocType=OFD-H`、机读信息标引（`urn:emr:machine-readable:2026`）、
    附件（心电图影像）均可正常回读；
  - **原包条目一个不少**（锁住本次二进制资源丢失的回归）；
  - 文档内容被改动后，**原国密签名不再通过**（符合预期）。

### 验证

- 单元测试 **960 个全绿**（959 + 1 新增）。
- 打包核对：14 个 `.nupkg` + 14 个 `.snupkg` 版本统一为 `1.3.2`，门禁脚本本地双路径自测通过
  （正确产物通过 / 复刻 v1.3.0 翻车场景的产物被精确拦下并中止）。

### 安装

```bash
dotnet add package NetOfd --version 1.3.2        # 常用
dotnet add package NetOfd.Full --version 1.3.2   # 含签名/加密/归档/EMR
```

### 许可证

- 整体：MIT（见 `LICENSE`）。
- 移植自 OFDRW 的代码：保留 Apache-2.0 归属（见 `LICENSE-Apache-2.0` / `NOTICE`）。
