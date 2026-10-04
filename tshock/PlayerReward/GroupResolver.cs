using System.Collections;
using System.Reflection;
using TShockAPI;

namespace PlayerReward;

public static class GroupResolver
{
    private static readonly object Sync = new();
    private static Type? customUtilsType;
    private static Type? customHelpersType;
    private static bool warningLogged;

    public static HashSet<string> ForPlayer(TSPlayer? player, string playerName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (player?.Group != null)
        {
            AddGroupChain(result, player.Group);
        }

        var customPlayer = FindCustomPlayer(player, playerName);
        if (customPlayer != null)
        {
            foreach (var groupName in ReadStringList(customPlayer, "HaveGroupNames"))
            {
                if (!string.IsNullOrWhiteSpace(groupName))
                {
                    result.Add(groupName.Trim());
                }
            }

            if (ReadMember(customPlayer, "Group") is Group customGroup)
            {
                AddGroupChain(result, customGroup);
            }
        }

        return result;
    }

    public static HashSet<string> ForOfflineAccount(string playerName)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var account = TShock.UserAccounts.GetUserAccountByName(playerName);
        if (account == null)
        {
            return result;
        }

        var group = TShock.Groups.GetGroupByName(account.Group);
        if (group != null)
        {
            AddGroupChain(result, group);
        }

        return result;
    }

    private static void AddGroupChain(HashSet<string> groups, Group? group)
    {
        var current = group;
        var guard = 0;
        while (current != null && guard++ < 32)
        {
            if (!string.IsNullOrWhiteSpace(current.Name))
            {
                groups.Add(current.Name.Trim());
            }

            current = current.Parent;
        }
    }

    private static object? FindCustomPlayer(TSPlayer? player, string playerName)
    {
        try
        {
            var utils = ResolveCustomUtilsType();
            if (utils != null)
            {
                var method = utils.GetMethod("FindPlayer", BindingFlags.Public | BindingFlags.Static, null, new[] { typeof(string) }, null);
                if (method != null)
                {
                    var found = method.Invoke(null, new object[] { playerName });
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            if (player != null)
            {
                var helpers = ResolveCustomHelpersType();
                var playersField = helpers?.GetField("Players", BindingFlags.Public | BindingFlags.Static);
                if (playersField?.GetValue(null) is Array players && player.Index >= 0 && player.Index < players.Length)
                {
                    return players.GetValue(player.Index);
                }
            }
        }
        catch (Exception ex)
        {
            if (!warningLogged)
            {
                warningLogged = true;
                TShock.Log.ConsoleError("[PlayerReward] CustomPlayer 对接读取失败，将只使用 TShock 组：" + ex.Message);
            }
        }

        return null;
    }

    private static Type? ResolveCustomUtilsType()
    {
        lock (Sync)
        {
            if (customUtilsType != null)
            {
                return customUtilsType;
            }

            return customUtilsType = FindLoadedType("CustomPlayer.Utils");
        }
    }

    private static Type? ResolveCustomHelpersType()
    {
        lock (Sync)
        {
            if (customHelpersType != null)
            {
                return customHelpersType;
            }

            return customHelpersType = FindLoadedType("CustomPlayer.CustomPlayerPluginHelpers");
        }
    }

    private static Type? FindLoadedType(string fullName)
    {
        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var type = assembly.GetType(fullName, false);
                if (type != null)
                {
                    return type;
                }
            }
            catch
            {
                // 某些动态程序集不能在当前加载上下文中枚举，忽略即可。
            }
        }

        return null;
    }

    private static IEnumerable<string> ReadStringList(object instance, string memberName)
    {
        if (ReadMember(instance, memberName) is IEnumerable values)
        {
            foreach (var value in values)
            {
                if (value is string text)
                {
                    yield return text;
                }
            }
        }
    }

    private static object? ReadMember(object instance, string memberName)
    {
        var type = instance.GetType();
        var property = type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance);
        if (property != null)
        {
            return property.GetValue(instance);
        }

        var field = type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
        return field?.GetValue(instance);
    }
}
