using TShockAPI;
using TShockAPI.DB;

namespace CGive;

public class CGive
{
    public string Executer { get; set; } = "";

    public string cmd { get; set; } = "";

    public string who { get; set; } = "";

    public int id { get; set; }

    public bool Execute()
    {
        if (this.who == "-1")
        {
            var executer = ResolveExecutor(this.Executer);
            if (!executer.HasValue) return false;

            this.Save();
            using (var re = Data.QueryReader(
                "SELECT id FROM CGive WHERE executer=@0 AND cmd=@1 AND who=@2 ORDER BY id DESC LIMIT 1",
                this.Executer, this.cmd, this.who))
            {
                if (re.Read())
                    this.id = re.Reader.GetInt32(0);
            }

            foreach (var tSPlayer in TShock.Players)
            {
                if (tSPlayer is { Active: true })
                {
                    Commands.HandleCommand(executer.Value, this.cmd.Replace("{name}", tSPlayer.Name));
                    new Given { Name = tSPlayer.Name, Id = this.id }.Save();
                }
            }
            return true;
        }

        var target = TShock.Players.FirstOrDefault(p =>
            p != null && p.Active && p.Name.Equals(this.who, StringComparison.OrdinalIgnoreCase));
        if (target is null) return false;

        var targetExecutor = ResolveExecutor(this.Executer);
        if (!targetExecutor.HasValue) return false;

        Commands.HandleCommand(targetExecutor.Value, this.cmd.Replace("{name}", target.Name));
        return true;
    }

    public bool ExecuteOnLogin(TSPlayer target)
    {
        var executer = ResolveExecutor(this.Executer);
        if (!executer.HasValue || target is null) return false;

        Commands.HandleCommand(executer.Value, this.cmd.Replace("{name}", target.Name));
        return true;
    }

    private static CommandExecutor? ResolveExecutor(string name)
    {
        if (name.Equals("server", StringComparison.OrdinalIgnoreCase))
            return new CommandExecutor(null, byte.MaxValue);

        var player = TShock.Players.FirstOrDefault(p =>
            p != null && p.Active && p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));
        if (player is null) return null;

        return new CommandExecutor(player.GetCurrentServer(), (byte)player.Index);
    }
    public static IEnumerable<CGive> GetCGive()
    {
        var list = new List<CGive>();
        using (var re = Data.QueryReader("SELECT executer,cmd,who,id FROM CGive"))
        {
            while (re.Read())
            {
                list.Add(new CGive
                {
                    Executer = re.Reader.GetString(0),
                    cmd = re.Reader.GetString(1),
                    who = re.Reader.GetString(2),
                    id = re.Reader.GetInt32(3)
                });
            }
        }
        return list;
    }

    /// <summary>
    /// ֻ��ѯ��ָ���������صļ�¼��personal ģʽ����ȫ��ģʽ��who='-1'��������ȫ��ɨ��
    /// </summary>
    public static IEnumerable<CGive> GetCGiveForPlayer(string playerName)
    {
        var list = new List<CGive>();
        using var re = Data.QueryReader(
            "SELECT executer,cmd,who,id FROM CGive WHERE who=@0 OR who='-1'", playerName);
        while (re.Read())
        {
            list.Add(new CGive
            {
                Executer = re.Reader.GetString(0),
                cmd = re.Reader.GetString(1),
                who = re.Reader.GetString(2),
                id = re.Reader.GetInt32(3)
            });
        }
        return list;
    }

    /// <summary>
    /// ֻ��ѯָ��������� personal ģʽ��¼������ REST �ӿڵȾ�ȷ��ѯ������
    /// </summary>
    public static IEnumerable<CGive> GetCGiveByWho(string who)
    {
        var list = new List<CGive>();
        using var re = Data.QueryReader(
            "SELECT executer,cmd,who,id FROM CGive WHERE who=@0", who);
        while (re.Read())
        {
            list.Add(new CGive
            {
                Executer = re.Reader.GetString(0),
                cmd = re.Reader.GetString(1),
                who = re.Reader.GetString(2),
                id = re.Reader.GetInt32(3)
            });
        }
        return list;
    }

    public void Save()
    {
        Data.Command($"INSERT INTO CGive(executer,cmd,who) VALUES (@0,@1,@2)", this.Executer, this.cmd, this.who);
    }

    public void Del()
    {
        Data.Command($"DELETE FROM CGive WHERE id=@0", this.id);
    }
}