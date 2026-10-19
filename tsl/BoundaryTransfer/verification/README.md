# 真实运行时离线验证

这是与纯算法 tests 不同的两组验证：

- `runtime`：载入真实 UTSL/OTAPI，构造真实 TileData/ServerContext，验证直达落点保持接触高度、真实 `Player.BordersMovement` 左右身体边界与速度归零、朝外输入、自动失败无聊天、隐藏指令、包钩子、会话复用守卫、超时及卸载策略。历史安全搜索算法仍保留在 `source` 供回归，但 1.4.1 正式运行支持同高度直达+海边修正+可选接缝模式与出生点落点；探针同时检查正式直达落点。当前 **93 PASS / 0 FAIL**。不假装已经运行完整网络切服，不调用真实客户端。
- `loader`：使用真实 DotnetPluginHost 加载编译 DLL，检查 1.4.1 元数据、原生配置生成（含海边/接缝/出生点字段、不生成旧搜索项）、外部修改不自动生效、手动重载、无效配置保留、卸载，**15 PASS / 0 FAIL**。

纯算法/配置/协议测试另计 **209 PASS / 0 FAIL**，包含接缝、地表范围、海边保护、直达落点、旧搜索键忽略、无特效 style 6 断言。

## 重跑

```powershell
dotnet build .\runtime\RuntimeProbe.csproj -c Release -p:UtBase="E:\实际UTSL目录"
dotnet .\runtime\bin\Release\net9.0\BoundaryTransfer.Tests.dll
```

探针已为无控制台/重定向输出固定终端尺寸，可直接运行；结果写入 EXE 同目录 `runtime.log`。

加载器需要一个全新的隔离工作目录，不要指向运行中的服务器：

```powershell
dotnet build .\loader\LoaderProbe.csproj -c Release -p:UtBase="E:\实际UTSL目录"
# 将主工程产出的 BoundaryTransfer.dll 放到 loader EXE 同目录下的 plugins 中，删除旧 config 后运行。
```

每轮加载器测试会改写其隔离目录的配置用于热重载验证，日志为 EXE 同目录 `loader.log`。重复测试应使用新的隔离目录；真实宿主可能把平铺 DLL 移到插件子目录，不要误以为重新复制平铺文件就一定替换了子目录旧版。

这两组验证均不能替代真实 Terraria 客户端在有玩家世界/空世界间双向联机实测，尤其不能替代对“跨图后是否仍有混沌传送粒子的客户端观感”的验收。
