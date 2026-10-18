using System.Reflection;
using UnifierTSL;
using UnifierTSL.PluginHost.Hosts.Dotnet;
using UnifierTSL.PluginService;
using UnifierTSL.Surface.Hosting;
using UnifierTSL.Surface.Prompting;

int passed=0;
void Check(string name,bool ok) {Console.WriteLine((ok?"PASS ":"FAIL ")+name);if(!ok)throw new Exception(name);passed++;}
try
{
    typeof(UnifierApi).Assembly.GetType("UnifierTSL.Surface.Hosting.LauncherSurfaceConsole")!
        .GetMethod("Initialize",BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(null,[new ProbeConsole()]);
    Initializer.Initialize();
    System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(Path.Combine(AppContext.BaseDirectory,"TShockAPI.dll"));
    typeof(UnifierApi).GetField("<EventHub>k__BackingField",BindingFlags.NonPublic|BindingFlags.Static)!.SetValue(null,new EventHub());
    string legacy=Path.Combine(Environment.CurrentDirectory,"CommandTool.json");
    string legacyText="{\"Enable\":true,\"SwitchCommandList\":{}}";File.WriteAllText(legacy,legacyText);
    var host=new DotnetPluginHost();await host.InitializePluginsAsync();
    var plugin=host.Plugins.SingleOrDefault(p=>p.Name=="CommandTool");
    Check("官方原生宿主发现合并插件",plugin is not null);
    Check("初始化及全部Hook绑定成功",plugin!.LoadStatus==PluginLoadStatus.Loaded);
    Check("插件版本2.5.0",plugin.Metadata.Version.ToString()=="2.5.0");
    Check("直接继承BasePlugin而非旧兼容壳",plugin.Plugin.GetType().BaseType?.Name=="BasePlugin");
    string path=Path.Combine(Environment.CurrentDirectory,"config","CommandTool","CommandTool.v2.json");
    Check("原生配置注册自动生成统一文件",File.Exists(path));
    string initial=File.ReadAllText(path);
    Check("中文版本化配置包含三个绑定列表",initial.Contains("配置版本")&&initial.Contains("标牌")&&initial.Contains("开关")&&initial.Contains("区域"));
    Check("旧CommandTool配置没有被覆盖",File.ReadAllText(legacy)==legacyText);
    var settingsField=plugin.Plugin.GetType().GetField("_settings",BindingFlags.NonPublic|BindingFlags.Instance)!;
    object Settings()=>settingsField.GetValue(plugin.Plugin)!;
    int Poll()=>(int)Settings().GetType().GetProperty("PollMs")!.GetValue(Settings())!;
    int Count(string name)=>((System.Collections.ICollection)Settings().GetType().GetProperty(name)!.GetValue(Settings())!).Count;
    Check("没有自动把无世界旧绑定投入使用",Count("Signs")==0&&Count("Switches")==0&&Count("Regions")==0);
    var tshock=AppDomain.CurrentDomain.GetAssemblies().First(a=>a.GetName().Name=="TShockAPI");
    var catalog=(System.Collections.IEnumerable)tshock.GetType("TShockAPI.Commands")!.GetField("ChatCommands")!.GetValue(null)!;
    var aliases=new HashSet<string>(new[]{"工具","commandtool","开关","kg","switch","标牌","sign","signcommand","sc","区域传送","ctregion"},StringComparer.OrdinalIgnoreCase);
    bool exposed=false;
    foreach(object command in catalog)
        foreach(string name in (System.Collections.IEnumerable)command.GetType().GetProperty("Names")!.GetValue(command)!)
            exposed|=aliases.Contains(name);
    Check("所有管理别名均未注册到公共目录",!exposed);
    File.WriteAllText(path,initial.Replace("\"区域检测间隔_毫秒\": 100","\"区域检测间隔_毫秒\": 150"));
    await Task.Delay(2500);
    Check("真实文件监视器应用有效配置",Poll()==150);
    File.WriteAllText(path,"{\"配置版本\":2,\"区域检测间隔_毫秒\":1}");await Task.Delay(2500);
    Check("非法数值热重载保留上次有效配置",Poll()==150);
    File.WriteAllText(path,"{\"Enable\":false}");await Task.Delay(2500);
    Check("旧schema误放统一路径拒绝重载",Poll()==150);
    File.WriteAllText(path,initial);await Task.Delay(2500);
    Check("修复文件后监视器继续工作",Poll()==100);
    await host.ShutdownAsync();await host.UnloadPluginsAsync();
    Check("插件关闭与卸载成功",true);
    Console.WriteLine($"RESULT PASS={passed} FAIL=0");
}
catch(Exception e){Console.Error.WriteLine("UNHANDLED "+e);Environment.ExitCode=1;}

internal sealed class ProbeConsole:ILauncherSurfaceHost
{
    private readonly TextWriter _output=Console.Out;
    public bool IsInteractive=>false;
    public string ReadLine(PromptSurfaceSpec spec,bool trim=false)=>"";
    public ConsoleKeyInfo ReadKey(bool intercept)=>default;
    public bool IsKeyAvailable()=>false;
    public void Write(string text)=>_output.Write(text);
    public void WriteAnsi(string text)=>_output.Write(text);
    public IDisposable BeginSurfaceActivityStatus(string category,string message)=>this;
    public void RefreshAppearanceSettings(){}
    public void Dispose(){}
}
