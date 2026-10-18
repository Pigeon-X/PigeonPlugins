using CommandTool;
using Newtonsoft.Json;
using System.Reflection;
using UnifierTSL;

var surface=new FixtureConsole();
typeof(UnifierApi).Assembly.GetType("UnifierTSL.Surface.Hosting.LauncherSurfaceConsole")!
    .GetMethod("Initialize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,[surface]);
Initializer.Initialize();
// CI/重定向输出无控制台句柄；仅测试中固定终端高度，避免发行版SampleServer构造读取WindowHeight失败。
using var terminalHeight=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowHeight))!.GetMethod!,
    (Func<Func<int>,int>)(_=>50));
using var terminalWidth=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowWidth))!.GetMethod!,
    (Func<Func<int>,int>)(_=>100));
using var terminalLeft=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowLeft))!.GetMethod!,
    (Func<Func<int>,int>)(_=>0));
using var terminalTop=new MonoMod.RuntimeDetour.Hook(typeof(Console).GetProperty(nameof(Console.WindowTop))!.GetMethod!,
    (Func<Func<int>,int>)(_=>0));
typeof(UnifierApi).GetField("<EventHub>k__BackingField",BindingFlags.Static|BindingFlags.NonPublic)!.SetValue(null,new EventHub());
TShockAPI.TShock.Config=new TShockAPI.Configuration.TShockConfig(new FixtureRegistrar(),"test-config");

int passed=0,failed=0;
void Check(string name,bool ok)
{
    if(ok){passed++;Console.WriteLine("PASS "+name);}
    else{failed++;Console.Error.WriteLine("FAIL "+name);}
}
try {ToolMigrationTests.RunAll();Check("旧中英文配置迁移完整断言组",true);}
catch(Exception e){Check("旧中英文配置迁移完整断言组",false);Console.Error.WriteLine(e);}
CoreModelChecks.Run(Check);
await SilenceChecks.Run(Check);
RuntimeChecks.Run(Check);

string testDir=Path.Combine(Path.GetTempPath(),"CommandTool.Tests-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(testDir);string configPath=Path.Combine(testDir,"CommandTool.v2.json");
try
{
    var first=new ToolConfig {PollMs=100};ToolConfigStore.SaveAtomic(configPath,first);
    Check("统一配置创建后严格读取",ToolConfigStore.Read(configPath).PollMs==100);
    var next=new ToolConfig {PollMs=150};ToolConfigStore.SaveAtomic(configPath,next);
    Check("原子替换保留上个版本备份",Directory.GetFiles(testDir,"*.bak").Length==1&&ToolConfigStore.Read(configPath).PollMs==150);
    try {ToolConfigStore.SaveAtomic(configPath,new ToolConfig{PollMs=1});Check("坏配置保存必须拒绝",false);}catch(ArgumentException){Check("坏配置保存必须拒绝",true);}
    Check("坏配置不会覆盖有效文件",ToolConfigStore.Read(configPath).PollMs==150);
    File.WriteAllText(configPath,"{\"Enable\":true}");
    try {ToolConfigStore.Read(configPath);Check("统一路径拒绝旧JSON模式误载",false);}catch(ArgumentException){Check("统一路径拒绝旧JSON模式误载",true);}
    File.WriteAllText(configPath,"{\"配置版本\":2,\"启用\":true,\"启用\":false}");
    try {ToolConfigStore.Read(configPath);Check("统一配置拒绝重复属性",false);}catch(JsonException){Check("统一配置拒绝重复属性",true);}
}
catch(Exception e){Check("严格读写配置IO断言组",false);Console.Error.WriteLine(e);}
Console.WriteLine($"TOTAL {passed+failed} / PASS {passed} / FAIL {failed}");
Console.WriteLine("测试临时配置与备份："+testDir);
return failed==0?0:1;
