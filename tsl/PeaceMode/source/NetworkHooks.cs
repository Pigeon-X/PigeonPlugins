using System.Buffers.Binary;
using Microsoft.Xna.Framework;
using Terraria.Chat;
using Terraria.Localization;
using UnifierTSL.Events.Core;
using UnifierTSL.Events.Handlers;

namespace PeaceMode;

public sealed partial class PeaceModePlugin
{
    private static readonly HashSet<string> EventMessages = new(StringComparer.Ordinal)
    {
        "Announcement.HasAwoken", "Announcement.HasBeenDefeated", "Announcement.HasBeenSlain",
        "LegacyMisc.8", "LegacyMisc.9", "LegacyMisc.20", "LegacyMisc.28", "LegacyMisc.29", "LegacyMisc.30",
        "LegacyMisc.31", "LegacyMisc.32", "LegacyMisc.33", "LegacyMisc.34", "LegacyMisc.35", "LegacyMisc.36",
        "LegacyMisc.0", "LegacyMisc.1", "LegacyMisc.2", "LegacyMisc.3", "LegacyMisc.4", "LegacyMisc.5", "LegacyMisc.6", "LegacyMisc.7",
        "LegacyMisc.10", "LegacyMisc.11", "LegacyMisc.24", "LegacyMisc.25", "LegacyMisc.26", "LegacyMisc.27",
        "LegacyMisc.41", "LegacyMisc.42", "LegacyMisc.43", "LegacyMisc.44", "LegacyMisc.45", "LegacyMisc.46", "LegacyMisc.47",
        "LegacyMisc.48", "LegacyMisc.52", "LegacyMisc.99", "LegacyMisc.107", "LegacyMisc.108", "LegacyMisc.106",
        "LegacyWorldGen.59", "LegacyWorldGen.74", "LegacyWorldGen.75", "LegacyWorldGen.92", "LegacyWorldGen.93"
    };

    private void BindNetworkHooks()
    {
        Bind(() => NetPacketHandler.ProcessPacketEvent.Register(OnPacket, HandlerPriority.Highest, FilterEventOption.All),
            () => NetPacketHandler.ProcessPacketEvent.UnRegister(OnPacket));
        Bind(() => On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessage += OnBroadcast,
            () => On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessage -= OnBroadcast);
        Bind(() => On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessageAs += OnBroadcastAs,
            () => On.Terraria.Chat.ChatHelperSystemContext.BroadcastChatMessageAs -= OnBroadcastAs);
    }

    private void OnPacket(ref ReadonlyEventArgs<ProcessPacketEvent> args)
    {
        if (!_enabled || args.Content.EventType != ProcessPacketEventType.BeforeAllLogic) return;
        var data = args.Content.RawData;
        if (data.IsEmpty) return;
        var blocked = data[0] switch
        {
            // 61: 玩家 Int16 + NPC/事件 Int16。仅保留不产生 NPC/事件的两本战斗书与商贩背包。
            61 => data.Length < 5 || BinaryPrimitives.ReadInt16LittleEndian(data[3..]) is not (-11 or -17 or -18),
            71 => true,  // 释放动物/NPC
            111 => true, // 切换派对
            113 => true, // 召唤撒旦军队水晶
            130 => true, // 钓出 NPC
            _ => false
        };
        if (blocked)
        {
            args.Handled = true;
            args.StopPropagation = true;
        }
    }

    private static bool IsEventAnnouncement(NetworkText text, Color color)
    {
        if (text._mode != NetworkText.Mode.LocalizationKey) return false;
        return EventMessages.Contains(text._text) ||
            text._text.StartsWith("Announcement.", StringComparison.Ordinal) ||
            text._text.StartsWith("Game.Invasion", StringComparison.Ordinal) ||
            text._text.StartsWith("DungeonDefenders2.", StringComparison.Ordinal) ||
            text._text.StartsWith("Game.BirthdayParty", StringComparison.Ordinal) ||
            text._text.StartsWith("Game.LanternNight", StringComparison.Ordinal);
    }

    private void OnBroadcast(On.Terraria.Chat.ChatHelperSystemContext.orig_BroadcastChatMessage orig,
        ChatHelperSystemContext self, NetworkText text, Color color, int excludedPlayer)
    {
        if (!_enabled || !IsEventAnnouncement(text, color)) orig(self, text, color, excludedPlayer);
    }

    private void OnBroadcastAs(On.Terraria.Chat.ChatHelperSystemContext.orig_BroadcastChatMessageAs orig,
        ChatHelperSystemContext self, byte messageAuthor, NetworkText text, Color color, int excludedPlayer)
    {
        // 玩家聊天走 messageAuthor；只过滤系统发送的事件公告。
        if (!_enabled || messageAuthor != byte.MaxValue || !IsEventAnnouncement(text, color))
            orig(self, messageAuthor, text, color, excludedPlayer);
    }
}
