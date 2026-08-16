# Agent Defaults

## Execution And Files

- 默认不要主动执行编译/构建/测试验证（例如 `dotnet build`、`dotnet test`）。
- Release构建结果已与`DalamudPlugin`的对应目录进行了符号链接，根据要求更新目标仓库时，无需复制构建结果。
- 仅在用户明确要求时，才执行编译或测试相关命令。
- 用 `rg` 搜索内容时，路径参数传目录，不传 `MainWindowV2Window*.cs` 这类 glob 路径；文件名过滤用 `-g 'MainWindowV2Window*.cs'`。
- 需要先枚举文件再读取时，用 `rg --files <目录> -g 'MainWindowV2Window*.cs'`，或 PowerShell `Get-ChildItem -Path <目录> -Filter 'MainWindowV2Window*.cs'`。
- 编辑代码时，默认遵循`.editconfig`配置
- 编辑文件时，`apply_patch` 的上下文必须基于真实文件内容；不要从 PowerShell 折行、`Select-String` 高亮/格式化、带行号前缀的输出中复制长行作为补丁上下文。
- 补丁上下文匹配失败时，先重新读取目标文件的当前内容；优先用短且稳定的相邻源码行或最小 enclosing block 做 patch，不把终端显示层的换行、截断或行号前缀当作文件文本。
- 默认保持文本文件为 `UTF-8` 编码和 `LF` 换行，遵循仓库根目录 `.editorconfig` 与 `.gitattributes`。
- 修改文件时避免引入无关的整文件编码或换行符转换，例如 `LF -> CRLF`。
- 除非文件类型或外部工具明确要求，不要使用 `CRLF` 或 `UTF-8 BOM`。
- 若必须调整文件编码或换行策略，应在变更说明中明确原因。
- `PowerShell` 乱码属于读取编码错误，勿修改文件内容，使用时也必须强制指定 `UTF8` 编码。

## Source Of Truth

### Game Data Source

-查找游戏内纯数据时，例如物品ID、鱼类数据、采集等级、钓场、天气、ExcelSheet字段或RowId，应总是先使用已接入EXDViewer MCP（`exdviewer`）。
-本机解包表格仓库`E:\GitHub\ffxiv-datamining-cn`只是游戏数据的本地克隆快照，可能没有及时拉取；只有EXDViewer MCP不通、未加载、或问题需要检查原始CSV文件结构/表头/本机克隆内容时，才按正确CSV方法查看它。
-不要凭记忆猜测游戏数据口径；若本项目运行时代码、EXDViewer MCP结果与解包表格含义不一致，应在回答或变更说明中明确区分“EXDViewer/运行时字段”和“解包数据字段”。
-该规则只适用于游戏数据查询与口径确认；Dalamud API、ClientStructs API、项目代码结构仍按当前仓库源码和`Dependency / API Reference Confirmation`确认。

### Dependency / API Reference Confirmation

- 目前Dalamud SDK为`Dalamud.CN.NET.Sdk/15.0.0` ,对应.NET 版本为.NET 10.
- 修改依赖库、运行时 API 或 UI 绘制 API 前，先查当前仓库现有用法；项目内已有封装、调用点或约定优先于外部参考。
- 仓库没有现成用法时，先只读查本机克隆仓库源码；用 `rg` 按类型名、字段名、方法名搜索 API、绑定/生成源码、结构注释、绘制语义和提交附近上下文：
  - `E:\GitHub\Dalamud`
  - `E:\GitHub\imgui`
  - `E:\GitHub\FFXIVClientStructs`
  - `E:\Github\FFXIVNetworkOpcodes`
- `E:\GitHub\imgui` 是 Dalamud 引用的 Dear ImGui 源码，优先用于确认原生 Dear ImGui API 与绘制语义，例如 draw list path、rounding flags、clip、primitive、`AddRectFilled`、`AddRectFilledMultiColor`、`PathArcToFast` 等行为。
- 需要确认 Dalamud ImGui / `Dalamud.Bindings.ImGui` 是否暴露某个 C# API、类型、枚举或方法签名时，也先从本机克隆里的绑定/生成源码找；找不到时再查当前项目实际引用或绑定 DLL 元数据。不要假设 Hooks dev 目录存在 `imgui.xml`，也不要把缺失的 `imgui.xml` 当成必要确认步骤。
- 本机克隆找不到、或需要核对当前项目实际引用/运行时二进制时，再查看项目文件、lock 文件、SDK props/targets、引用 DLL、绑定 DLL，以及 Hooks dev 目录里的 XML/DLL 元数据。当前 Windows / CN 默认 Dalamud dev 目录是 `C:\Users\zc979\AppData\Roaming\XIVLauncher\addon\Hooks\dev\`（即 `%APPDATA%\XIVLauncher\addon\Hooks\dev\`）。
- Hooks dev XML 主要用于确认 Dalamud、Lumina、Lumina.Excel 或 FFXIVClientStructs 的 API、类型、字段、方法、offset、继承关系或枚举；必要时用 DLL 元数据/反射确认枚举、方法或签名。
- 这些本机克隆库默认只读参考，不要改动源码、分支或子模块状态；不要尝试反编译 DLL 或凭记忆猜测。
- 若当前项目实际引用、Hooks dev 文档/绑定 DLL 与本机源码参考不一致，应以当前项目实际引用/运行时绑定为准，并在回答或变更说明中明确区分“运行时 API / 绑定”和“本机源码参考”。

## Change Constraints

### Signature Format

- 向项目源码、资源或文档写入 native byte signature / sig / AOB pattern 时，通配字节统一写作 `??`，不要写成单个 `?`。
- 对 `CompSig.GetHook` 这类直接 hook 签名，仍需确认匹配地址是目标函数入口，并在当前二进制中唯一匹配。

### Code Structure & Refactoring

- 新增功能时优先融入现有结构，不要为了新功能主动引入新的局部架构或多层包装。
- 触及代码结构、状态字段、工具方法、数据结构、文件拆分或调用链时，按 `code-structure-maintenance` 的口径判断和收敛；详细规则放在该 skill，不在本规范重复展开。
- 完成上述类型改动后，交付前按 `code-structure-maintenance` 轻量复查触碰区域，能局部收敛且低风险的应直接收敛。
- 允许顺手改善触碰区域的可维护性，但应服务于当前任务，不做无关的大规模重构、搬文件或批量重命名。
- 如果用户只是询问可维护性或重构建议，先给判断和建议范围；除非用户明确要求实现，否则不要直接大规模修改。

### UI Copy

- 当任务涉及窄按钮、tab bar、列表行等紧凑 UI 文案，或用户明确提到文案长度、拥挤、放不下时，将显示宽度作为检查项；普通正文、说明文、宽布局文案不需要额外测宽。
- 粗略估算时，可按未缩放的原始 UI 宽度 `10px ≈ 1` 个全宽字符判断。若文案接近控件可用宽度，应代入典型数字检查。
- 空间允许时，文字/词 与 数字、半角符号、ASCII 片段之间优先用半角空格隔开，以提升扫读性；但数字与计量单位、后缀、语法助词之间是否加空格，应遵循对应语言和项目既有习惯，例如英文短单位 `5m` 通常不拆成 `5 m`。
- 若 `<1`、倒计时、状态变化等特殊文本让通用模板变得别扭或过长，优先单独拆短文案，而不是继续拼接。

### Versioning

- 版本号格式为 `x.y.z.0`（四段式），第四位始终为 `0`，除非用户手动修改。
- **x（主版本号）**：重大功能更新或架构性变更时递增。
- **y（次版本号）**：新增较小功能或较大优化时递增。
- **z（修订号）**：修复 Bug 或进行小幅优化时递增。
- 递增高位版本号时，低位版本号归零；例如 `1.2.3.0` 发布重大更新后下一个版本为 `2.0.0.0`。
- 未经用户明确要求，不要自行修改版本号。

### 通用 Playbook 输出要求

- 命中任一 playbook 的需求时，先完成范围判定；除非用户明确要求立即实现，否则不要跳过判定直接修改代码。
- 需要先给方案或说明时，默认按以下结构输出：
  1. `需求重述`（1-3 行）
  2. `改动范围判定`（引用对应 playbook 的领域分类）
  3. `实施清单`（按文件列出）
  4. `兼容性说明`（旧表达式、旧配置、旧文案、运行时默认行为等按领域说明）
  5. `验收标准`（解析、序列化、行为、显示、保存、语言切换等按领域说明）
  6. `非目标`（明确不做什么）
- 若用户明确要求立即实现，最终变更说明仍应覆盖实际改动范围、兼容性/验收要点，以及未执行的验证。
- 具体 playbook 只保留领域决策树、文件级 checklist、领域模板和常见漏改点；不要在每个 playbook 重复本节通用输出结构。
