using System.Globalization;
using TShockAPI;
using TShockAPI.DB;

namespace PlayerReward;

public enum RewardType
{
    Player,
    Sponsor
}

public sealed class RewardStore
{
    private const string TableName = "PlayerRewardClaim";

    public void EnsureSchema()
    {
        var sqlType = TShock.DB.GetSqlType();
        var timestampType = sqlType == SqlType.Postgres ? "TIMESTAMP" : "DATETIME";
        TShock.DB.Query($@"
CREATE TABLE IF NOT EXISTS {TableName} (
    PlayerName VARCHAR(200) NOT NULL,
    PackType VARCHAR(32) NOT NULL,
    PackName VARCHAR(200) NOT NULL,
    ClaimedAt {timestampType} NOT NULL,
    PRIMARY KEY (PlayerName, PackType, PackName)
)");
    }

    public HashSet<string> GetClaimed(string playerName, RewardType type)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = TShock.DB.QueryReader(
            $"SELECT PackName FROM {TableName} WHERE PlayerName=@0 AND PackType=@1",
            playerName,
            type.ToString());
        while (reader.Read())
        {
            var packName = reader.Get<string>("PackName");
            if (!string.IsNullOrWhiteSpace(packName))
            {
                result.Add(packName);
            }
        }

        return result;
    }

    public bool TryClaim(string playerName, RewardType type, string packName)
    {
        if (this.HasClaimed(playerName, type, packName))
        {
            return false;
        }

        try
        {
            TShock.DB.Query(
                $"INSERT INTO {TableName} (PlayerName, PackType, PackName, ClaimedAt) VALUES (@0, @1, @2, @3)",
                playerName,
                type.ToString(),
                packName,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
            return true;
        }
        catch
        {
            // 并发领取时主键会拒绝第二次插入；此时按“已经领取”处理。
            if (this.HasClaimed(playerName, type, packName))
            {
                return false;
            }

            throw;
        }
    }

    public bool HasClaimed(string playerName, RewardType type, string packName)
    {
        using var reader = TShock.DB.QueryReader(
            $"SELECT 1 FROM {TableName} WHERE PlayerName=@0 AND PackType=@1 AND PackName=@2",
            playerName,
            type.ToString(),
            packName);
        return reader.Read();
    }

    public void RemoveClaim(string playerName, RewardType type, string packName)
    {
        TShock.DB.Query(
            $"DELETE FROM {TableName} WHERE PlayerName=@0 AND PackType=@1 AND PackName=@2",
            playerName,
            type.ToString(),
            packName);
    }

    public int ResetPlayer(string playerName, RewardType type)
    {
        return TShock.DB.Query(
            $"DELETE FROM {TableName} WHERE PlayerName=@0 AND PackType=@1",
            playerName,
            type.ToString());
    }

    public int ResetAll(RewardType type)
    {
        return TShock.DB.Query(
            $"DELETE FROM {TableName} WHERE PackType=@0",
            type.ToString());
    }

    public void TryMigrateLegacyTable()
    {
        try
        {
            using var reader = TShock.DB.QueryReader(
                "SELECT Name, obtained_player_packs, obtained_sponsor_packs FROM PlayerReward");
            var imported = 0;
            while (reader.Read())
            {
                var playerName = reader.Get<string>("Name");
                if (string.IsNullOrWhiteSpace(playerName))
                {
                    continue;
                }

                imported += this.ImportLegacyColumn(playerName, RewardType.Player, reader.Get<string>("obtained_player_packs"));
                imported += this.ImportLegacyColumn(playerName, RewardType.Sponsor, reader.Get<string>("obtained_sponsor_packs"));
            }

            if (imported > 0)
            {
                TShock.Log.ConsoleInfo($"[PlayerReward] 已从旧 PlayerReward 表迁移 {imported} 条领取记录。");
            }
        }
        catch
        {
            // 旧表不存在或结构不同：这是正常情况，不阻止插件启动。
        }
    }

    private int ImportLegacyColumn(string playerName, RewardType type, string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        var count = 0;
        foreach (var pack in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (this.TryClaim(playerName, type, pack))
            {
                count++;
            }
        }

        return count;
    }
}
