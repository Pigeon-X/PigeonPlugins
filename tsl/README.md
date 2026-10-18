# UTSL 插件开发目录

本目录存放 PigeonPlugins 的 UnifierTSL 变体源码。插件按独立 `.csproj`
维护，不直接复用 `tshock/` 下的 TShock 二进制。

## 当前纳入开发的插件

| 插件 | 源码 | 版本 |
|---|---|---|
| PeaceMode | `tsl/PeaceMode/` | 1.0.0 |
| SignCommand | `tsl/SignCommand/` | 1.2.6（已合并到 CommandTool，停止生产部署） |
| BoundaryTransfer | `tsl/BoundaryTransfer/` | 1.2.0 |
| CommandTool | `tsl/CommandTool/` | 2.5.0 |

两套源码已与 `_发布/` 中的对应源码包逐文件哈希比对一致。

当前本地 Release 构建结果：

- PeaceMode.dll：`D7CDD6932EFFFA223F984D6853CA45353723016530E806881E8E2908876D3D1C`（36,864 B）
- SignCommand.dll：`1822558262137D5593A81624FA588086A10A8DE10B1896FAAFA1FCC903C4B490`（45,568 B）
- BoundaryTransfer.dll：`678D5B47AEB284071BCD14264C07B987262E89696DB8A26DA8EDD25F87346F00`（60,928 B）
- CommandTool.dll：`9D416F2475EC0CF6814785EB7842EA8C95689A744EFDFB07383E392DACD6E7B7`（94,720 B）

## 构建

默认 `UtBase` 指向本机已有的 UTSL 发行目录；如不存在，用 `UtBase` 覆盖：

```powershell
dotnet build .\tsl\PeaceMode\PeaceMode.UnifierTSL.csproj -c Release `
  -p:UtBase="E:\path\to\utsl-win-x64"

dotnet build .\tsl\SignCommand\SignCommand.UnifierTSL.csproj -c Release `
  -p:UtBase="E:\path\to\utsl-win-x64"

dotnet build .\tsl\BoundaryTransfer\BoundaryTransfer.UnifierTSL.csproj -c Release `
  -p:UtBase="E:\path\to\utsl-win-x64"

dotnet build .\tsl\CommandTool\CommandTool.UnifierTSL.csproj -c Release `
  -p:UtBase="E:\path\to\utsl-win-x64" -p:NuGetAudit=false
```

## 运行时文件

- PeaceMode 配置模板：`tsl/PeaceMode/config/PeaceMode.json`
- SignCommand 配置模板：`tsl/SignCommand/config/SignCommand.json`
- BoundaryTransfer 生产配置：`tsl/BoundaryTransfer/config/BoundaryTransfer.json`
- BoundaryTransfer 配置模板：`tsl/BoundaryTransfer/配置模板/BoundaryTransfer.json`
- CommandTool 生产配置：`tsl/CommandTool/config/CommandTool.v2.json`
- CommandTool 配置示例：`tsl/CommandTool/examples/CommandTool.v2.json`

生产部署仍按 0 人窗、备份、任务正门重启流程执行；本目录只做源码开发与构建。
