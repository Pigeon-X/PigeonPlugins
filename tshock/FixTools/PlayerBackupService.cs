using System.Security.Cryptography;
using Newtonsoft.Json;
using TShockAPI;
using static FixTools.FixTools;

namespace FixTools;

internal static class PlayerBackupService
{
    private static readonly object Gate = new();
    private static readonly string Root = Path.Combine(WritePlayer.AutoSaveDir, "玩家备份");
    private static readonly string IndexPath = Path.Combine(Root, "player_backup_index.json");

    private sealed class BackupIndex
    {
        public int Version { get; set; } = 1;
        public List<BackupRecord> Backups { get; set; } = new();
    }

    private sealed class BackupRecord
    {
        public string Key { get; set; } = "";
        public int AccountId { get; set; }
        public string AccountName { get; set; } = "";
        public string PlayerName { get; set; } = "";
        public string Uuid { get; set; } = "";
        public string FilePath { get; set; } = "";
        public string CreatedLocal { get; set; } = "";
        public string CreatedUtc { get; set; } = "";
        public string Reason { get; set; } = "";
        public string Sha256 { get; set; } = "";
        public long Size { get; set; }
    }

    public static void BackupOnJoinIfMissing(TSPlayer? player)
    {
        if (!FixTools.Config.BackupPlayerOnJoin || player?.RealPlayer != true || player.TPlayer is null || !player.IsLoggedIn) return;
        var key = GetPlayerKey(player);
        lock (Gate)
        {
            var index = LoadIndex();
            if (index.Backups.Any(record => string.Equals(record.Key, key, StringComparison.Ordinal) && File.Exists(ToAbsolute(record.FilePath)))) return;
        }
        Backup(player, "join");
    }

    public static void BackupOnLeave(TSPlayer? player)
    {
        if (!FixTools.Config.BackupPlayerOnLeave || player?.RealPlayer != true || player.TPlayer is null) return;
        Backup(player, "leave");
    }

    private static void Backup(TSPlayer player, string reason)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "PigeonRPG_Backup", Guid.NewGuid().ToString("N"));
        var tempPlayer = Path.Combine(tempRoot, "player");
        try
        {
            Directory.CreateDirectory(tempPlayer);
            if (!WritePlayer.Export(player.TPlayer, tempPlayer)) return;
            var source = Directory.GetFiles(tempPlayer, "*.plr").FirstOrDefault();
            if (source is null) return;

            var key = GetPlayerKey(player);
            var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
            var targetDir = Path.Combine(Root, key);
            Directory.CreateDirectory(targetDir);
            var target = Path.Combine(targetDir, $"{timestamp}_{reason}.plr");
            File.Copy(source, target, true);

            var record = new BackupRecord
            {
                Key = key,
                AccountId = player.Account?.ID ?? 0,
                AccountName = player.Account?.Name ?? "",
                PlayerName = player.Name ?? "",
                Uuid = player.UUID ?? "",
                FilePath = ToRelative(target),
                CreatedLocal = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                CreatedUtc = DateTime.UtcNow.ToString("O"),
                Reason = reason,
                Sha256 = GetSha256(target),
                Size = new FileInfo(target).Length
            };

            lock (Gate)
            {
                var index = LoadIndex();
                index.Backups.RemoveAll(item => string.Equals(item.FilePath, record.FilePath, StringComparison.OrdinalIgnoreCase));
                index.Backups.Add(record);
                Rotate(index);
                SaveIndex(index);
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[{FixTools.PluginName}] 玩家备份失败：{player.Name}，{ex.Message}");
        }
        finally
        {
            TryDeleteDirectory(tempRoot);
        }
    }

    public static IReadOnlyList<BackupRecordView> Find(string query)
    {
        lock (Gate)
        {
            var normalized = query.Trim();
            return LoadIndex().Backups
                .Where(record => File.Exists(ToAbsolute(record.FilePath)))
                .Where(record => string.Equals(record.AccountName, normalized, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.PlayerName, normalized, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.Uuid, normalized, StringComparison.OrdinalIgnoreCase) ||
                                 string.Equals(record.Key, normalized, StringComparison.OrdinalIgnoreCase) ||
                                 record.AccountName.Contains(normalized, StringComparison.OrdinalIgnoreCase) ||
                                 record.PlayerName.Contains(normalized, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(record => record.CreatedUtc)
                .Select(record => new BackupRecordView(record.AccountName, record.PlayerName, record.CreatedLocal, record.Reason, record.FilePath, record.Sha256, record.Size))
                .ToList();
        }
    }

    public static void HandleBackupCommand(CommandArgs args)
    {
        if (args.Parameters.Count < 1) { args.Player.SendInfoMessage("用法：/pbackup <玩家名或账号名>"); return; }
        var records = Find(args.Parameters[0]);
        if (records.Count == 0) { args.Player.SendInfoMessage("没有找到该玩家的备份。"); return; }
        args.Player.SendInfoMessage($"[{FixTools.PluginName}] 玩家备份索引：{args.Parameters[0]}");
        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            args.Player.SendInfoMessage($"[{i + 1}] {record.CreatedLocal} | {record.Reason} | {record.Size / 1024.0:0.0} KB | {Path.GetFileName(record.FilePath)}");
        }
    }

    public static void HandleRestoreCommand(CommandArgs args)
    {
        if (args.Parameters.Count < 1) { args.Player.SendInfoMessage("用法：/prestore <玩家名> [序号，默认1最新]"); return; }
        var records = Find(args.Parameters[0]);
        if (records.Count == 0) { args.Player.SendErrorMessage("没有找到该玩家的备份。"); return; }
        var index = 1;
        if (args.Parameters.Count > 1 && (!int.TryParse(args.Parameters[1], out index) || index < 1 || index > records.Count))
        {
            args.Player.SendErrorMessage($"序号无效，当前可用范围：1-{records.Count}");
            return;
        }
        var selected = records[index - 1];
        try
        {
            var absolute = ToAbsolute(selected.FilePath);
            if (!File.Exists(absolute)) { args.Player.SendErrorMessage("备份文件不存在，无法恢复。"); return; }
            if (ReaderPlayer.RestoreSsc(absolute, selected.PlayerName))
            {
                args.Player.SendSuccessMessage($"已恢复 {selected.PlayerName} 的 SSC：{selected.CreatedLocal}（序号 {index}）");
                TShock.Log.ConsoleInfo($"[{FixTools.PluginName}] {args.Player.Name} 恢复 {selected.PlayerName} SSC，备份={selected.FilePath}");
            }
            else
            {
                args.Player.SendErrorMessage("恢复失败，请查看服务器日志。");
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[{FixTools.PluginName}] SSC 恢复失败：{selected.PlayerName}，{ex}");
            args.Player.SendErrorMessage("恢复失败，请查看服务器日志。");
        }
    }

    private static void Rotate(BackupIndex index)
    {
        var keep = Math.Clamp(Config.PlayerBackupKeep, 1, 100);
        var remove = index.Backups.GroupBy(record => record.Key, StringComparer.Ordinal)
            .SelectMany(group => group.OrderByDescending(record => record.CreatedUtc).Skip(keep)).ToList();
        foreach (var record in remove)
        {
            index.Backups.Remove(record);
            var absolute = ToAbsolute(record.FilePath);
            if (absolute.StartsWith(Root, StringComparison.OrdinalIgnoreCase) && File.Exists(absolute)) File.Delete(absolute);
        }
    }

    private static BackupIndex LoadIndex()
    {
        try
        {
            return !File.Exists(IndexPath)
                ? new BackupIndex()
                : JsonConvert.DeserializeObject<BackupIndex>(File.ReadAllText(IndexPath)) ?? new BackupIndex();
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[{FixTools.PluginName}] 玩家备份索引读取失败：{ex.Message}");
            return new BackupIndex();
        }
    }

    private static void SaveIndex(BackupIndex index)
    {
        Directory.CreateDirectory(Root);
        var temp = IndexPath + ".tmp";
        File.WriteAllText(temp, JsonConvert.SerializeObject(index, Formatting.Indented));
        File.Copy(temp, IndexPath, true);
        File.Delete(temp);
    }

    private static string GetPlayerKey(TSPlayer player) => player.Account?.ID > 0 ? player.Account.ID.ToString() : SafeFileName(player.Name ?? player.UUID ?? "unknown");

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return new string(value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray());
    }

    private static string ToRelative(string absolute) => Path.GetRelativePath(Root, absolute);
    private static string ToAbsolute(string relative) => Path.GetFullPath(Path.Combine(Root, relative));
    private static string GetSha256(string file) { using var stream = File.OpenRead(file); return Convert.ToHexString(SHA256.HashData(stream)); }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path) && path.StartsWith(Path.Combine(Path.GetTempPath(), "PigeonRPG_Backup"), StringComparison.OrdinalIgnoreCase))
                Directory.Delete(path, true);
        }
        catch { }
    }
}

internal sealed record BackupRecordView(string AccountName, string PlayerName, string CreatedLocal, string Reason, string FilePath, string Sha256, long Size);