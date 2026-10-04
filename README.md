# Pigeon Plugins

独立维护的鸽子服插件源码仓库。

## 目录

- tshock/：TShock 6.2.1 独立插件。
- tsl/：UnifierTSL / TSL 独立插件与必要共享库。

## 边界

本仓库只保存独立插件源码、工程文件和插件内置资源模板。不保存编译产物、运行配置、数据库、日志、部署包或 AI 分析文件。

以下耦合项目不进入本仓库：

- Dimensions / 维度核心
- TsWeb / TsWeb.Sync
- PGameAPI / PigeonTR.Bot
- PigeonRPG 业务模块
- Options / runtime / generated / decompiled 输出

## 已收录

TShock：AntiCheatingTool、CustomPlayer、FixTools、TileHelper、CGive、HelpPlus、PlayerReward、Chameleon、MapTp、StatusTextManager、TeleportRequest、VeinMiner、ProgressBag、ProgressControls、PlayerSpeed、RandomFishingLoot、RandRespawn、PersonalPermission、PerPlayerLoot、SwitchCommands、VotePlus。

TSL：LazyAPI、Chrome.Title、PlaceholderAPI、CommandTool、AntiCheatingTool、FixTools、145修复小公举、MapTp、TeleportRequest、ProgressBag、ProgressControls、CGive、VeinMiner、StatusTextManager、TileHelper、HotReload、CustomPlayer。

## 初始化提交

仓库采用源码快照方式建立。编译依赖和参考 DLL 不提交；构建时在项目属性或本机 DevelopmentDependencies 中指向 TShock/UnifierTSL 运行目录。
