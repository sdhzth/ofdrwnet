<!-- Release steps (this comment is hidden on the Release page):
     1) Open https://github.com/sdhzth/ofdrwnet/releases/edit/v1.3.1
     2) Attach the 14 .nupkg files (and optionally the 14 .snupkg) from
        C:\Users\Administrator\AppData\Local\Temp\netofd-pack-all-131
     3) Copy & paste the content of this file into the description box -->

# NetOfd v1.3.1

## 补丁版本：板式布局支持插入到指定页码

本次为**补丁版本（patch）**，在 v1.3.0 基础上补齐一项能力、加固一处实现并补充文档。

### 新增：VirtualPage 插入到指定页码

`VirtualPage.SetPageNum(n)` 此前只有属性定义、没有任何消费点——虚拟页面只能追加到文档末尾。
本次补齐后，与 Java `VPageParseEngine.addNewPage` 语义完全对齐：

- `PageNum` 为空 → 追加到末尾，并**回填实际页码**（调用方可在 `Close()` 之后查询该页的最终位置）；
- `PageNum` 有值 → 插入到第 `n` 页（1-based）的位置，越界自动钳制（小于 1 插到最前，大于总页数则追加末尾）；
- 物理目录 `Page_N` 仍按追加顺序分配，**文档页序由 `Document.xml` 的 `<Page>` 列表决定**——
  与 Java 一致；且 `OFDReader` 本就按页序读取，所以按页码取内容始终正确。

用法：

```csharp
var vPage = new VirtualPage(PageLayout.A4());
vPage.Add(new Div().SetPosition(Position.Absolute).SetXY(20, 30).SetWidth(170));
vPage.SetPageNum(2);      // 插入到第 2 页
doc.AddVPage(vPage);
doc.Close();
// vPage.PageNum 已回填为该页的最终页码
```

### 加固

- `OFDDoc.AddWatermark` 原按**物理目录索引**遍历页面，插入后会错位加水印 → 改为按 `Document.xml` 的页面树顺序遍历。
- 新增 `PagesDir.ObtainPageDirByName(dirName)`：按 `BaseLoc` 解析出的目录名定位物理页目录（物理目录号 ≠ 页序时必需）。

### 文档

- 中英文 README 新增「板式（固定）布局：虚拟页面」章节：绝对定位约束、`SetPageNum` 插入语义、
  母版复用（`AddTemplate`）、图层归属，以及「流式 `Add` 与 `AddPage` 交错会留下空白页」的避坑提示。

### 验证

- 单元测试 **959 个全绿**（新增 5 例：指定页码插入 / 插到最前且与流式页混合 / 越界钳制 / 追加并回填页码 / Reader 回读页序与内容）。
- 打包核对：14 个 nupkg + 14 个 snupkg 版本全部为 `1.3.1`。

### 安装

```bash
dotnet add package NetOfd --version 1.3.1        # 常用
dotnet add package NetOfd.Full --version 1.3.1   # 含签名/加密/归档/EMR
```

### 许可证

- 整体：MIT（见 `LICENSE`）。
- 移植自 OFDRW 的代码：保留 Apache-2.0 归属（见 `LICENSE-Apache-2.0` / `NOTICE`）。
