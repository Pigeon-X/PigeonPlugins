using Terraria;
using Terraria.Net;
using Terraria.Net.Sockets;
using UnifierTSL.Plugins;
using UnifierTSL.Servers;
using UnifierTSL.Surface.Hosting;
using UnifierTSL.Surface.Prompting;

internal sealed class TestConsole : ILauncherSurfaceHost
{
    private readonly TextWriter _output = Console.Out;
    public bool IsInteractive => false;
    public string ReadLine(PromptSurfaceSpec contextSpec, bool trim = false) => "";
    public ConsoleKeyInfo ReadKey(bool intercept) => default;
    public bool IsKeyAvailable() => false;
    public void Write(string text) => _output.Write(text);
    public void WriteAnsi(string text) => _output.Write(text);
    public IDisposable BeginSurfaceActivityStatus(string category, string message) => this;
    public void RefreshAppearanceSettings() { }
    public void Dispose() { }
}
internal sealed class TestRegistrar : IPluginConfigRegistrar
{
    public object? Handle;
    public string Directory => Environment.CurrentDirectory;
    public IConfigOption DefaultOption => throw new NotSupportedException();
    public IConfigRegistrationBuilder<T> CreateConfigRegistration<T>(string path) where T : class, new() { var h = new TestConfig<T>(path); Handle = h; return h; }
    public IConfigRegistrationBuilder<T> CreateConfigRegistration<T>(string path, ConfigFormat format) where T : class, new() => CreateConfigRegistration<T>(path);
    public IConfigRegistrationBuilder<T> CreateConfigRegistration<TFormat,T>(string path) where TFormat : IConfigFormatProvider,new() where T : class,new() => CreateConfigRegistration<T>(path);
}
internal sealed class TestConfig<T>(string path) : IConfigRegistrationBuilder<T>, IPluginConfigHandle<T> where T:class,new()
{
    private T? _value = new();
    public string FilePath => path;
    public event AsyncConfigChangedHandler<T>? OnChangedAsync;
    public int SubscriberCount => OnChangedAsync?.GetInvocationList().Length ?? 0;
    public IConfigRegistrationBuilder<T> WithDefault(Func<T> factory) { _value = factory(); return this; }
    public IConfigRegistrationBuilder<T> OnSerializationFailure(SerializationFailureHandling handling) => this;
    public IConfigRegistrationBuilder<T> OnDeserializationFailure(DeserializationFailureHandling handling, bool autoPersistFallback = true) => this;
    public IConfigRegistrationBuilder<T> TriggerReloadOnExternalChange(bool enabled) => this;
    public IConfigRegistrationBuilder<T> WithValidation(Func<T,bool> validator) => this;
    public IPluginConfigHandle<T> Complete() => this;
    public Task<T> RequestAsync(bool reloadFromIO = false, CancellationToken cancellationToken=default) => Task.FromResult(_value!);
    public T Request(bool reloadFromIO=false) => _value!;
    public T? TryGetCurrent() => _value;
    public Task OverwriteAsync(T? value,CancellationToken cancellationToken=default) { _value=value; return Task.CompletedTask; }
    public void Overwrite(T? value) { _value=value; }
    public Task ReloadAsync(CancellationToken cancellationToken=default) => Task.CompletedTask;
    public void Reload() { }
    public T? ModifyInMemory(Func<T?,T?> updater) => _value=updater(_value);
    public void Dispose() { }
    public async Task Change(T value) { _value=value; if(OnChangedAsync is not null) await OnChangedAsync(this,value); }
}
internal sealed class FakeWorld(string name) : IWorldDataProvider
{
    public string WorldName => name;
    public string WorldFileName => name + ".wld";
    public Terraria.IO.WorldFileData ApplyMetadata(ServerContext server) => null!;
    public IWorldDataProvider GenerateOrLoadExisting(string n,int size,int difficulty,int evil,string seed) => this;
    public IWorldDataProvider FromBytes(string n,byte[] bytes) => this;
}
internal sealed class CaptureSocket : ISocket
{
    public bool Connected = true;
    public int Packets;
    public void Close() => Connected = false;
    public bool IsConnected() => Connected;
    public void Connect(RemoteAddress address) => Connected = true;
    public void AsyncSend(byte[] data,int offset,int size,SocketSendCallback callback,object state) { Packets++; callback(state); }
    public void AsyncSendNoCopy(byte[] data,int offset,int size,SocketSendCallback callback,object? state=null) { Packets++; callback(state!); }
    public void AsyncSend(ReadOnlyMemory<byte> data,SocketSendCallback callback,object? state=null) { Packets++; callback(state!); }
    public void AsyncReceive(byte[] data,int offset,int size,SocketReceiveCallback callback,object state) { }
    public bool IsDataAvailable() => false;
    public RemoteAddress GetRemoteAddress() => new TcpAddress(System.Net.IPAddress.Loopback,7777);
    public bool StartListening(UnifiedServerProcess.RootContext root,SocketConnectionAccepted callback) => false;
    public void StopListening() { }
}
