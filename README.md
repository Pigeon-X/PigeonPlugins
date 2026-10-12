# Pigeon Plugins

独立维护的鸽子服插件源码仓库。

**本仓库是所有独立 TShock / TSL 插件的唯一源码权威。** 插件库（`_参考资料\插件库`）
只保留产物、配置模板与指向本仓库的出处记录，不再存放第二份源码。

## 目录

- tshock/：TShock 6.2.x 独立插件。
- tsl/：UnifierTSL / TSL 独立插件与必要共享库。
- tshock/Shared/：公共源码（上游 UnrealMultiple 的 `Shared` 约定），由 `template.targets` 统一引入。
- tshock/SourceGen/：Roslyn 源生成器，供 tshock 与 tsl 的 LazyAPI 共用。
- tshock/LazyAPI/：TShock 侧 LazyAPI，Chameleon / PlayerSpeed / ProgressBag 等依赖它。
- template.targets：仓库级构建模板。**必须存在于仓库根**——`tshock/` 下多数 `.csproj`
  都是 `<Import Project="..\..\template.targets" />`，缺了它这些工程无法还原与编译。

## 构建

```powershell
# 单个插件
dotnet build tshock\<插件名>\<插件名>.csproj -c Release

# 产物统一输出到
out\Release\
```

`template.targets` 只在工程自身未声明时兜底 `TargetFramework=net9.0`，
因此 `SourceGen`（netstandard2.0）等工程不会被覆盖。

## 边界

本仓库只保存独立插件源码、工程文件和插件内置资源模板。不保存编译产物、运行配置、数据库、日志、部署包或 AI 分析文件。

以下耦合项目不进入本仓库：

- Dimensions / 维度核心
- TsWeb / TsWeb.Sync
- PGameAPI / PigeonTR.Bot
- PigeonRPG 业务模块
- Options / runtime / generated / decompiled 输出

## 已收录

TShock：AntiCheatingTool、FixTools、TileHelper、CGive、HelpPlus、PlayerReward、CommandTool、PlaceholderAPI、Chameleon、MapTp、TeleportRequest、VeinMiner、ProgressBag、PlayerSpeed、RandomFishingLoot、RandRespawn、PersonalPermission、PerPlayerLoot、SwitchCommands、VotePlus、LazyAPI、PermaBuff、Permabuffs、PeaceMode。

TSL：LazyAPI、Chrome.Title、PlaceholderAPI、CommandTool、AntiCheatingTool、FixTools、145修复小公举、MapTp、TeleportRequest、ProgressBag、ProgressControls、CGive、VeinMiner、TileHelper、HotReload。

### ProgressControls 的归属（2026-10-12 源码排重）

- **TShock 侧已合并进 FixTools**：原独立插件 `ProgressControls`（显示名「计划书」）已作为
  内部模块并入 `tshock/FixTools/`，实现位于 **`tshock/FixTools/ProgressControl/`**
  （`PControl.cs` / `PControl.Initialize.cs` / `PControl.Utils.cs` / `Config.cs` / `I18n.cs`），
  随 `FixTools.dll` 一起编译与部署。因此**不再收录** `tshock/ProgressControls/`
  —— 该目录已删除（其内容与本仓库 `tsl/ProgressControls/source/` 逐字节相同，是历史副本）。
- **TSL 侧仍独立**：`tsl/ProgressControls/source/` 保留（属 15-UTSL 的地盘）；
  它是 TSL 版 `ProgressControls.dll` 的唯一源码。
  ⚠ 现状：该 `.csproj` 与 `tsl/` 下其它工程一样写 `<Import Project="..\..\template.targets" />`，
  但相对 `source/` 只上溯到 `tsl/`，**找不到仓库根的 `template.targets`**，`dotnet build` 会报
  `NETSDK1013`。需要 15-UTSL 侧确认是改 Import 深度（`..\..\..\`）还是另有构建入口。

### PermaBuff 与 Permabuffs 是两支不同的插件

- `tshock/PermaBuff/`：上游 UnrealMultiple 的「永久 Buff」原始实现（程序集 `PermaBuff`）。
- `tshock/Permabuffs/`：本机线上实际在跑的版本，命名空间 `Permabuffs_V2`，作者 Zaicon & Cai，
  额外提供 `BuffGroup` / `RegionBuff`（区域 Buff）等能力，程序集名仍为 `Permabuffs`。
  该支源码由线上 `Permabuffs.dll` 反编译得到并修正到可编译、行为等价，
  目的是让线上这支插件不再只以二进制形式存在。
  **不要把二者互相替换。**

### PeaceMode

`tshock/PeaceMode/`：和平模式（作者 lmx12330），禁止世界中全部 NPC 与事件生成。
本机线上在跑，但此前**全盘没有任何源码、上游也未登记**，只有编译产物；
现由线上 `PeaceMode.dll` 反编译得到并修正到可编译，避免该插件只能以二进制形式存在。

## 由反编译补齐的源码

以下两支此前只有二进制，现已在仓库内有可编译源码，重建产物类清单与线上一致：

| 目录 | 线上程序集 | 说明 |
|---|---|---|
| `tshock/Permabuffs/` | `Permabuffs.dll` | 命名空间 `Permabuffs_V2`，含区域 Buff 能力 |
| `tshock/PeaceMode/` | `PeaceMode.dll` | 和平模式 |

两者重建后都会多出一个 `Localizer.I18n` 类：现源码经 `tshock/Shared/I18n.cs` 走共享本地化，
线上那版是更早构建、尚未接入。功能上不冲突。

## 初始化提交

仓库采用源码快照方式建立。编译依赖和参考 DLL 不提交；构建时在项目属性或本机 DevelopmentDependencies 中指向 TShock/UnifierTSL 运行目录。

## Git 上游

- GitHub origin：git@ssh.github.com:Pigeon-X/PigeonPlugins.git
- 本地工作区：C:\Users\59934\Saved Games\流光核心源码\_git\PigeonPlugins
- 本地同步工具：D:\59934\Desktop\AI维护文件\11-测试工具与报告\PigeonPlugins-Git同步
- 远程服务器只用于测试和部署，不配置 Git 远端，不保存 Git 工作区。

## 外部归属

- CustomPlayer（TShock / TSL）已归入 PGameAPI / Bot 项目的 src/CustomPlayer，不再由 PigeonPlugins 维护。

- StatusTextManager（TShock / TSL）已归入 PGameAPI / PigeonDimension 的 src/PigeonDimension/StatusTextManager，不再由 PigeonPlugins 维护。
