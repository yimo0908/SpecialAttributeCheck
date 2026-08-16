# 新月岛补正检测（SpecialAttributeCheck）

一个基于 [卫月 (Dalamud)](https://github.com/goatcorp/Dalamud) 的插件，仅在**蜃景幻界新月岛**相关地域显示悬浮窗，用于检测小队成员的“补正”。

## 功能

- **悬浮窗**：仅当玩家位于 新月岛南部（TerritoryType 1252）、新月岛北部（1346）时显示。
  > 说明：经 EXDViewer MCP 查询游戏数据核实，“超魔之塔”（ContentFinderCondition 1114）在国服当前版本数据中同样属于地域 1346（新月岛北部），并非独立 TerritoryType，因此无需额外 ID。
- 四个按钮：
  - `小队检测`（tooltip：检查小队补正）
  - `周围检测`（tooltip：检查周围20米内最多48名玩家的补正）
  - `目标检测`（tooltip：检查目标玩家的补正）
  - `清除`（tooltip：清除当前检测结果）
- 输出格式：`职业 玩家名 补正值`，其中补正值 = **2x+y**
  - `x`：辅助职业大师（Status 4226）的层数
  - `y`：所穿新月岛装备“特殊修正”效果之和
  - 本人按装备栏直接计算 y；其余小队成员通过游戏“查看装备”流程读取装备后计算 y，读取失败时显示 `?`。
- 悬浮窗结果按“职业 / 名称 / 补正”三列对齐显示，按补正值从高到低排序，行间交替底色。
- 套装规则：
  - 同时穿戴 `力之新月魔耳饰 + 力之新月魔项链 + 力之新月魔手镯 + 力之新月魔戒指 + 超力之新月魔戒指` 时，将超力之新月魔戒指的 特殊修正+2 计入 y；
  - 同时穿戴 `魔之新月魔耳饰 + 魔之新月魔项链 + 魔之新月魔手镯 + 魔之新月魔戒指 + 超魔之新月魔戒指` 时，将超魔之新月魔戒指的 特殊修正+2 计入 y。
- 插件只有悬浮窗与设置界面，无主界面。

## 设置

命令 `/nmc`（或在悬浮窗上右键）打开设置：

| 设置 | 默认 | 说明 |
| --- | --- | --- |
| 悬浮窗输出 | 开启 | 在悬浮窗中输出检测结果 |
| 聊天输出 | 关闭 | 开启后通过 `/p` 将检测结果发送至小队频道（队内玩家可见） |

## 数据来源

- 地域 / 状态 / 物品 ID、装备特殊修正表均通过 **EXDViewer MCP** 查询游戏数据核实。
- 装备特殊修正表（`Data/CorrectionData.Items.g.cs`）由根目录 `Equipment.csv`（253 件新月岛装备）生成。
- 如需重新生成该表：`node gen_data.mjs`（生成脚本，仅依赖 Node.js）。
- 其他玩家的已装备物品通过游戏“查看装备”流程读取：请求前先关闭残留窗口、重置请求状态，等待数据就绪后优先读取 AgentInspect 缓存物品、回退到 Examine 容器，每个目标带重试与超时；失败时该成员显示为 `x★+?`，可在 `/xllog` 中查看 `[补正检测]` 日志定位原因。

## 构建

```bash
git submodule update --init --recursive
dotnet build SpecialAttributeCheck.slnx -c Release
```

要求：
- .NET SDK（net10.0-windows）
- 本机已安装 XIVLauncher（卫月），且 `%APPDATA%\XIVLauncherCN\addon\Hooks\dev\` 下存在 Dalamud 程序集（运行过一次游戏后自动生成）
  - 若使用国际服路径，可用 `-p:DalamudLibPath=...` 或 `DALAMUD_HOME` 覆盖 SDK 默认目录
- NuGet 能访问网络以下载 OmenTools 依赖（GuerrillaNtp 等）

构建产物：`SpecialAttributeCheck/bin/x64/Release/SpecialAttributeCheck.dll` 与对应 `.json` 清单，可直接放入卫月插件目录或通过开发插件列表加载。

## 依赖

- [OmenTools](https://github.com/AtmoOmen/OmenTools)（以 `third_party/OmenTools` 子模块方式引入，提供 DService 服务容器、ImGui 辅助等）
