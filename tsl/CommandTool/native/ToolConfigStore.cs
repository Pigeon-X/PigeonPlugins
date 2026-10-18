using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace CommandTool;

internal static class ToolConfigStore
{
    internal static ToolConfig Read(string path)
    {
        var file=new FileInfo(path);
        if(!file.Exists||file.Length>8*1024*1024)throw new ArgumentException("统一配置不存在或超过8MiB。");
        using var reader=new JsonTextReader(new StringReader(File.ReadAllText(path,Encoding.UTF8))) {MaxDepth=32,DateParseHandling=DateParseHandling.None};
        var root=JObject.Load(reader,new JsonLoadSettings {DuplicatePropertyNameHandling=DuplicatePropertyNameHandling.Error});
        if(reader.Read())throw new ArgumentException("统一配置后不能包含其他内容。");
        if(root["配置版本"]?.Type!=JTokenType.Integer||root.Value<int>("配置版本")!=2)
            throw new ArgumentException("统一配置必须显式包含 配置版本:2；旧版请使用迁移入口，不会自动重写旧文件。");
        return ToolValidation.Snapshot(root.ToObject<ToolConfig>()??throw new ArgumentException("统一配置不能为空。"));
    }
    internal static void SaveAtomic(string path,ToolConfig value)
    {
        var snapshot=ToolValidation.Snapshot(value);
        path=Path.GetFullPath(path);string dir=Path.GetDirectoryName(path)??throw new ArgumentException("配置目录无效。");
        Directory.CreateDirectory(dir);
        string temp=Path.Combine(dir,Path.GetFileName(path)+"."+Guid.NewGuid().ToString("N")+".tmp");
        byte[] data=new UTF8Encoding(false).GetBytes(JsonConvert.SerializeObject(snapshot,Formatting.Indented));
        if(data.Length>8*1024*1024)throw new ArgumentException("统一配置超过8MiB，未写入。");
        using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {stream.Write(data);stream.Flush(true);}
        if(File.Exists(path))
            File.Replace(temp,path,path+"."+DateTime.UtcNow.ToString("yyyyMMddHHmmssfff")+"."+Guid.NewGuid().ToString("N")[..6]+".bak");
        else File.Move(temp,path);
        // 替换失败保留 .tmp 供恢复，不先截断有效配置；备份不自动删除。
    }
}
