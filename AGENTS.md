# AGENTS.md

## 项目概述

NetOfd：OFD（GB/T 33190 版式文档）读写库，从 Java ofdrw 移植至 .NET 10。
13 个子模块（Core/Pkg/Layout/Reader/Sign/Crypto/Converter/Archive 等）+ 聚合包 NetOfd。
测试约 300 个，全部通过；构建要求 0 警告。

## 常用命令

```
dotnet build netofd.slnx -c Debug -v q --nologo        # 构建（须 0 警告 0 错误）
dotnet test tests/NetOfd.Tests/NetOfd.Tests.csproj -c Debug --nologo   # 全量测试
dotnet test ... --filter "FullyQualifiedName~VersionTest"              # 单类测试
dotnet pack netofd.slnx -c Release -o <dir>            # 打包 13 个 nupkg
dotnet run --project tests/NetOfd.TestConsole -- info <file.ofd>       # CLI（info/verify/text/render/merge/sample）
dotnet run --project samples/OfdHWriter -- <out.ofd>                  # 生成 OFD-H 电子病历（GB/T 48666—2026）
dotnet run --project samples/OfdHExporter -- <in.ofd> --out <目录>   # 导出 OFD-H 标准结构化数据（WS/T 500 + WS 445）
```

CI（.github/workflows/ci.yml）以 Release 运行 build+test+pack；提交前先在本地跑 Release 全量测试。

## 架构要点

- **编辑器统一策略**：Annotator/Attacher/Editor/Tagger/Versioner 均为 `byte[] → Dictionary<string,byte[]>（zip 展开为内存 map）→ 改 XML → WriteZipToBytes`，不依赖 Pkg 半成品容器层。共享辅助在 `NetOfd.Layout\Edit\ZipMapIO.cs`。
- **编辑器不可变约定**：所有编辑 API 返回**新字节**，输入不变。丢弃返回值 = 操作未生效（StressTest 曾因此误报库缺陷）。链式调用必须 `doc = Editor.Xxx(doc, ...)`。
- **对象 ID**：各编辑器从 DocInfo.MaxUnitID 分配新 ID，写回时 DocInfo=实际最大值、CommonData=值+1（生态兼容惯例），链式调用安全。
- **版本快照**：`Doc_0/V_N/` 存快照，`Versions/V_N/DocVersion.xml` 为描述文件；ExtractVersion 输出必须剥离 V_N 路径段才是合法独立文档。
- **布尔序列化**：OFD 规范要求小写 `"true"/"false"`（勿用 bool.ToString()）。
- **OFDDoc 页面模型**：文档初始 0 页，页面仅由 `AddPage()` 显式创建；`Add(段落)` 写入当前第 1 页，不会隐式建页。
- **渲染性能归因**：光栅化约 20–40ms/页（A4@15ppm），瓶颈在 PNG 编码（约 30 倍于光栅化）。追求速度用 `imageType:"jpg"`（快约 4 倍）或 `ParallelExport=true`（跨页并行，实测 3.5x）。
- **DrawParam 继承**：`ParentID` 链解析经 `DrawParamResolver.Resolve`（Core 纯函数）+ `ResourceManage.GetDrawParamResolved`；渲染器消费 Visible/Alpha 已走继承链。样式（LineWidth/Join/Cap/描边色/填充色）级联已接入 OfdPageRenderer 的 DrawPath/DrawText。
- **Graphics2D 颜色记录**：AddPath 现将画刷颜色写入 PathObject 的 FillColor/StrokeColor（此前填充一律不可见）；渐变画刷以中点插值纯色近似（Shading 规范未支持，类注释已声明）。描边线宽/Dash 与 AlphaComposite 透明度也已落盘。
- **裁剪（Clips）**：G2D SetClip/Clip 在设置时冻结 CTM 变换并写入图元 Clips；渲染器烘焙 PPM 后 ClipPath。注意本渲染器**无全局缩放**——各图元矩阵自带 PPM，新增页坐标空间的绘制逻辑必须自行乘 PPM。
- **模型惰性解析惯例**：CT_Clips/CT_Clip 从 XElement 构造后子列表为空，getter 内惰性加载；Add* 前先触发 getter 防止丢子元素。
- **资源列表形态**：CommonData 的公共/文档资源用**直子元素** `<PublicRes>`（无 List 包裹）——与 Java ofdrw 实际产物一致，勿按规范包裹形态构造测试夹具。
- **多文档编号**：DocRoot 可能指向 `Doc_1` 等非 0 目录（真实语料存在），读取端一律从 DocRoot 推导文档目录，禁止硬编码 `Doc_0`。
- **语料库冒烟**：`CorpusSmokeTest` 把 ofdrw-master 全部 67 个测试 OFD 走 打开/渲染/提取（阈值 80%，当前 100%）；改动 Reader/Converter 后必跑。SkiaSharp 对畸形图像可能抛异常而非返回 null——图像解码处需 try/catch 跳过。
- **文字装饰**：Span 下划线/删除线以描边 PathObject 实现（基线 +0.15×字号 / −0.30×字号，随折行逐段绘制），在 OFDDoc.RenderParagraph 的 FlushRun 内。Bold/Italic 落盘为 TextObject 属性；TextAlign Center/Right 按行宽平移运行段；Justify 折行整行设 TextCode.Width（渲染端按行宽均匀分布字形），末行不拉伸。
- **线程安全边界**：VirtualContainer 懒加载磁盘文件并写普通 Dictionary 缓存——共享实例跨线程渲染不安全。并行导出通过每页独立打开文档实例规避。
- **Java 对照**：源码在 `E:\电子病历\ofdrw-master\`；SealOFDReader/DLOFDReader 已废弃未移植。
- **命名约定映射**（机械审计时勿按字面比对）：Java `ST_Loc`↔.NET `STLoc`、`CT_Text`↔`CTText`；Java 的 XML 属性包装类（FillColor、LineWidth 等 OFDSimpleTypeElement 子类）在 .NET 中是元素上的类型化属性；接口加 I 前缀（SignIDProvider↔ISignIdProvider）。
- **覆盖率**：`dotnet test --collect:"XPlat Code Coverage"`（coverlet.collector）。低覆盖区曾连续挖出静默缺陷——Graphics2D 形状类漏实现 IShape 致空路径、CurrentSegment 不填控制点、AffineTransform Scale/Rotate 语义与 AWT 相反。修改 AWT 仿真层务必跑 `Graphics2DFunctionalTest` + `Graphics2DStateTest`。
- **MaxUnitID 双形态**：DocInfo 中为属性，CommonData 中为子元素，读取时按位置区分。
- **切缝章语义**：N 页产 2N−2 个片段（首页右条/中间页左右条/末页左条），CuttingRatio 取 0–1 分数。
- **规范低频类未移植**（Movie/Sound/GotoA 之外的 URI 动作、CT_Permission、CT_VPreferences、Shading 渐变、CT_Region、CompositeObject、Extensions）：务实裁剪，如需请参照 ofdrw-core 对应类补充。

## 关键教训（务必遵守）

1. **禁止用 PowerShell 内容类 cmdlet（Set-Content/Out-File/-replace 管道）修改源码文件**——已三次造成 UTF-8 编码损坏（乱码、引号被吞）。所有文件修改一律使用 Edit/Write 工具。
2. 控制台输出乱码 ≠ 文件损坏：PowerShell 控制台按 GBK 渲染 UTF-8 会显示假乱码。判断文件是否真损坏需用 Read 工具读取或字节级校验（EF BB BF + 合法 UTF-8 解码）。
3. 测试中构造中文样例文本没问题，但断言路径/条目名一律用 ASCII 与规范命名（如 `Doc_0/Annots/Page_1/Annot_1.xml`）。
4. `OFDDoc` 只把 MaxUnitID 写入 Document.xml 的 CommonData，不写 OFD.xml 的 DocInfo；读取时两处都查取最大值。
