# PeaceMode 1.0.1：UnifierTSL 和平模式

原生 UnifierTSL 插件，默认开启，作用于当前进程加载的所有世界。

## 效果

- 禁止全部 NPC：敌怪、Boss、城镇居民、旅商、宠物、动物和其他友好 NPC。
- 自然刷新、召唤物、NPC 释放、钓出 NPC、原版 Boss 召唤和事件召唤均无效。
- 开启后直接移除现有 NPC；不经过击杀、掉落、战利品或击败进度逻辑。
- 阻止哥布林、雪人、海盗、火星入侵、血月、日食、南瓜月、霜月、史莱姆雨、撒旦军队、四柱和月亮领主倒计时。
- 阻止下雨、金币雨、沙尘暴、大风、流星雨、陨石、派对、灯笼夜、邪教徒仪式、仙灵出没及片尾事件。
- 静默相关原版 NPC、Boss 和事件公告；保留普通玩家聊天和服务器通知。
- 正常昼夜转换、玩家、建筑和已有世界通关进度保留。

插件只控制 NPC 与世界事件；玩家召唤师仆从属于 Projectile，不作为 NPC 清除。客户端自己的物品使用动画或音效仍可能播放，服务端会拒绝生成 NPC/事件；部分消耗品的数量由客户端扣除，不保证返还。

## 安装与重载

1. 停服，将构建的 `PeaceMode.dll` 放到 UnifierTSL 启动目录的 `plugins` 文件夹。
2. 启动服务。首次自动生成 `config/PeaceMode/PeaceMode.json`，默认内容：

```json
{
  "启用和平模式": true
}
```

3. **外部修改不会自动生效。** 修改后执行：

```text
/和平 重载
```

或 `/peacemode 重载`。权限：`peace.reload`；服务器控制台可直接执行。

4. 修改为 `false` 并手动重载后关闭；重新开启后的下一次世界更新会清场。开关作用于所有已加载世界。

## 说明

- 本版引用 `TShockAPI` 仅用于注册手动重载命令。
- 关闭或卸载插件后恢复原版刷新与事件触发。被移除的 NPC 和已取消的事件不会自动恢复；可由原版重新刷新/召唤。
- 原版 NPC/事件提示不会在聊天中刷屏，插件加载时仅向服务端记录一次状态与配置路径。
- 只需部署插件 DLL，随附配置模板可选。不要把 TShock 版同名插件一起加载，也不要复制 OTAPI 或 UnifierTSL 依赖 DLL 到插件目录。

## 兼容与构建

构建目标：`.NET 9`，本地基线 `UnifierTSL 0.3.1 / Terraria 1.4.5.8`（`0.3.1-tshock6.1-cn/utsl-win-x64`）。

```powershell
dotnet build .\PeaceMode.UnifierTSL.csproj -c Release

# 在其他电脑指定本机 UnifierTSL 发行版根目录，其中必须有 lib/plugins：
dotnet build .\PeaceMode.UnifierTSL.csproj -c Release -p:UtBase="D:\UnifierTSL"
```

产物：`bin/Release/net9.0/PeaceMode.dll`。引用发行版中的 `TShockAPI.dll`、`UnifierTSL.dll`、`OTAPI.dll` 和 `OTAPI.Runtime.dll`，无额外 NuGet 依赖。

服务端插件若自行绕过原版协议发送自定义事件文本，需由该插件关闭自己的公告；和平模式不会屏蔽所有自定义聊天。游戏内多人联机的视觉效果仍建议安装后实际连接确认。
