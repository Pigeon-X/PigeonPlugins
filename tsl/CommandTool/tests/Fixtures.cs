using Terraria;
using UnifierTSL.Plugins;
using UnifierTSL.Servers;
using UnifierTSL.Surface.Hosting;
using UnifierTSL.Surface.Prompting;

internal sealed class FixtureConsole:ILauncherSurfaceHost
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
internal sealed class FixtureRegistrar:IPluginConfigRegistrar
{
    public string Directory=>Environment.CurrentDirectory;
    public IConfigOption DefaultOption=>throw new NotSupportedException();
    public IConfigRegistrationBuilder<T> CreateConfigRegistration<T>(string path) where T:class,new()=>new FixtureConfig<T>(path);
    public IConfigRegistrationBuilder<T> CreateConfigRegistration<T>(string path,ConfigFormat format) where T:class,new()=>new FixtureConfig<T>(path);
    public IConfigRegistrationBuilder<T> CreateConfigRegistration<F,T>(string path) where F:IConfigFormatProvider,new() where T:class,new()=>new FixtureConfig<T>(path);
}
internal sealed class FixtureConfig<T>(string path):IConfigRegistrationBuilder<T>,IPluginConfigHandle<T> where T:class,new()
{
    private T? _value=new();
    public string FilePath=>path;
    public event AsyncConfigChangedHandler<T>? OnChangedAsync {add{} remove{}}
    public IConfigRegistrationBuilder<T> WithDefault(Func<T> factory){_value=factory();return this;}
    public IConfigRegistrationBuilder<T> OnSerializationFailure(SerializationFailureHandling h)=>this;
    public IConfigRegistrationBuilder<T> OnDeserializationFailure(DeserializationFailureHandling h,bool autoPersistFallback=true)=>this;
    public IConfigRegistrationBuilder<T> TriggerReloadOnExternalChange(bool enabled)=>this;
    public IConfigRegistrationBuilder<T> WithValidation(Func<T,bool> validator)=>this;
    public IPluginConfigHandle<T> Complete()=>this;
    public Task<T> RequestAsync(bool reloadFromIO=false,CancellationToken cancellationToken=default)=>Task.FromResult(_value!);
    public T Request(bool reloadFromIO=false)=>_value!;
    public T? TryGetCurrent()=>_value;
    public Task OverwriteAsync(T? value,CancellationToken cancellationToken=default){_value=value;return Task.CompletedTask;}
    public void Overwrite(T? value)=>_value=value;
    public Task ReloadAsync(CancellationToken cancellationToken=default)=>Task.CompletedTask;
    public void Reload(){}
    public T? ModifyInMemory(Func<T?,T?> update)=>_value=update(_value);
    public void Dispose(){}
}
internal sealed class FixtureWorld(string name):IWorldDataProvider
{
    public string WorldName=>name;
    public string WorldFileName=>name+".wld";
    public Terraria.IO.WorldFileData ApplyMetadata(ServerContext s)=>null!;
    public IWorldDataProvider GenerateOrLoadExisting(string n,int size,int difficulty,int evil,string seed)=>this;
    public IWorldDataProvider FromBytes(string n,byte[] bytes)=>this;
}
