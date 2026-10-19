using System.Reflection;
using UnifierTSL;
using UnifierTSL.PluginHost.Hosts.Dotnet;
using UnifierTSL.PluginService;

string path = Path.Combine(AppContext.BaseDirectory,"loader.log");
File.WriteAllText(path, "BoundaryTransfer 原生插件加载器验证\n");
void L(string text) { File.AppendAllText(path,text+Environment.NewLine); Console.WriteLine(text); }
int pass=0;
void Check(string text,bool ok) { L((ok?"PASS ":"FAIL ")+text); if(!ok) throw new Exception(text); pass++; }
try
{
    typeof(UnifierApi).Assembly.GetType("UnifierTSL.Surface.Hosting.LauncherSurfaceConsole")!
        .GetMethod("Initialize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,[new TestConsole()]);
    Initializer.Initialize();
    typeof(UnifierApi).GetField("<EventHub>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,new EventHub());
    var host = new DotnetPluginHost();
    await host.InitializePluginsAsync();
    var plugin = host.Plugins.SingleOrDefault(p=>p.Name=="BoundaryTransfer");
    Check("原生宿主发现新插件",plugin is not null);
    Check("加载及初始化成功",plugin!.LoadStatus==PluginLoadStatus.Loaded);
    Check("元数据版本1.4.1",plugin.Metadata.Version.ToString()=="1.4.1");
    string config=Path.Combine(Environment.CurrentDirectory,"config","BoundaryTransfer","BoundaryTransfer.json");
    Check("原生配置自动生成",File.Exists(config));
    string text=await File.ReadAllTextAsync(config);
    Check("中文配置键可读且未Unicode转义",text.Contains("从左到右的世界名") && !text.Contains("\\u4"));
    Check("新配置仅身体碰边容差且无旧自动提示开关",text.Contains("碰边容差_像素") && !text.Contains("触发边界距离_格") && !text.Contains("显示切换提示"));
    var settingsField=plugin.Plugin.GetType().GetField("_settings",BindingFlags.NonPublic|BindingFlags.Instance)!;
    object Settings()=>settingsField.GetValue(plugin.Plugin)!;
    bool Enabled()=>(bool)Settings().GetType().GetProperty("Enabled")!.GetValue(Settings())!;
    string[] Worlds()=>(string[])Settings().GetType().GetProperty("Worlds")!.GetValue(Settings())!;
    void ManualReload()
    {
        var handle=plugin.Plugin.GetType().GetField("_config",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(plugin.Plugin)!;
        var cfg=handle.GetType().GetMethod("Request",new[]{typeof(bool)})!.Invoke(handle,new object[]{true});
        plugin.Plugin.GetType().GetMethod("ApplyConfig",BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(plugin.Plugin,new object[]{cfg});
    }
    Check("空排列默认不建立任意连接",Enabled() && Worlds().Length==0);
    await File.WriteAllTextAsync(config,"{\"启用\":false}");
    await Task.Delay(800);
    Check("外部改文件不自动重载",Enabled());
    ManualReload();
    Check("手动重载关闭插件触发",!Enabled());
    await File.WriteAllTextAsync(config,"{\"启用\":true,\"触发边界距离_格\":48,\"显示切换提示\":true,\"从左到右的世界名\":[\"左侧世界\",\"中间世界\",\"右侧世界\"]}");
    await Task.Delay(800);
    Check("外部改文件不自动改世界排列",Worlds().Length==0);
    ManualReload();
    Check("手动重载世界排列",Enabled() && Worlds().SequenceEqual(new[]{"左侧世界","中间世界","右侧世界"}));
    Check("旧配置重载仍使用像素容差且无提示状态",(int)Settings().GetType().GetProperty("ContactTolerance")!.GetValue(Settings())! == 2 && Settings().GetType().GetProperty("ShowMessages") is null && Settings().GetType().GetProperty("TriggerInset") is null);
    await File.WriteAllTextAsync(config,"{\"从左到右的世界名\":[\"重复\",\"重复\"]}");
    await Task.Delay(800);
    Check("重复世界排列不会自动生效",Worlds().SequenceEqual(new[]{"左侧世界","中间世界","右侧世界"}));
    ManualReload();
    Check("手动重载重复世界排列被拒绝保留有效配置",Worlds().SequenceEqual(new[]{"左侧世界","中间世界","右侧世界"}));
    await host.ShutdownAsync();
    await host.UnloadPluginsAsync();
    Check("原生插件关闭卸载成功",true);
    L($"RESULT PASS={pass} FAIL=0");
}
catch(Exception ex){L("UNHANDLED "+ex);Environment.ExitCode=1;}
