using System.Net.Http;
using System.Text;
using Newtonsoft.Json;
using TShockAPI;
using static FixTools.FixTools;

namespace FixTools;

/// <summary>
/// TSM 世界重建客户端。
/// 契约：POST {TSM地址}/tsm/world/rebuild，Header: X-TSM-Token
/// 本类只做"发请求 + 翻译状态码"，**不做任何生命周期 / 文件删除 / 关服 / 退出**。
/// </summary>
internal static class TsmRebuildClient
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(10) };

    internal sealed class TsmOptions
    {
        public bool 启用 { get; set; } = true;
        public string TSM控制地址 { get; set; } = "http://127.0.0.1:8765";
        public string TSM控制令牌 { get; set; } = "";
        public string TSM服务器ID { get; set; } = "";   // 统一数字 id：0=鸽子直播服 1=流光城 2=泰拉大陆 3=流光神域
    }

    internal static string OptionsPath => Path.Combine(MainPath, "TSM对接.json");

    internal static TsmOptions LoadOptions()
    {
        try
        {
            if (File.Exists(OptionsPath))
            {
                return JsonConvert.DeserializeObject<TsmOptions>(File.ReadAllText(OptionsPath))
                       ?? new TsmOptions();
            }

            var fresh = new TsmOptions();
            File.WriteAllText(OptionsPath, JsonConvert.SerializeObject(fresh, Formatting.Indented));
            return fresh;
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[{FixTools.PluginName}] 读取 TSM 对接配置失败：{ex.Message}");
            return new TsmOptions();
        }
    }

    /// <summary>
    /// 请求 TSM 重建世界。成功（202 已受理）返回 true；其余情况返回 false 并给出人话提示。
    /// 失败时**不做任何降级**（不删图、不改配置、不关服、不退出）。
    /// </summary>
    internal static bool TryRebuild(string reason, string actor, out string message, out int statusCode)
    {
        statusCode = 0;
        var opt = LoadOptions();

        if (!opt.启用)
        {
            message = "TSM 对接已关闭（TSM对接.json -> 启用=false），已拒绝执行。";
            return false;
        }

        if (string.IsNullOrWhiteSpace(opt.TSM服务器ID))
        {
            message = $"未配置 TSM服务器ID（{OptionsPath}），已拒绝执行；不做任何降级操作。";
            return false;
        }

        object payload = new
        {
            serverId = opt.TSM服务器ID,
            requestId = "fixtools-" + DateTime.Now.ToString("yyyyMMdd") + "-" + Guid.NewGuid().ToString("N"),
            reason,
            actor,
            confirm = true,
            backup = true,
            startAfterRebuild = true
        };

        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Post,
                opt.TSM控制地址.TrimEnd('/') + "/tsm/world/rebuild");
            req.Headers.Add("X-TSM-Token", opt.TSM控制令牌 ?? "");
            req.Content = new StringContent(
                JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json");

            using var resp = Http.Send(req);
            statusCode = (int)resp.StatusCode;

            message = statusCode switch
            {
                202 => "TSM 已受理世界重建（202）；重建中，请勿重复操作。",
                400 => "TSM 拒绝：参数错误或 confirm 不为 true（400）。",
                401 => "TSM 拒绝：X-TSM-Token 无效（401）。",
                404 => "TSM 拒绝：serverId 不存在（404）。",
                409 => "TSM 拒绝：正在重建或存在进程冲突（409）。",
                503 => "TSM 尚未就绪（503 manager_not_ready），请稍后重试。",
                500 => "TSM 内部错误（500）。",
                _   => $"TSM 返回未预期状态 {statusCode}。"
            };
            return statusCode == 202;
        }
        catch (Exception ex)
        {
            message = "TSM 控制接口不可用，已拒绝执行（不做任何降级操作）：" + ex.Message;
            return false;
        }
    }
}
