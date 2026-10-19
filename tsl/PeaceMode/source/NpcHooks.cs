using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using UnifiedServerProcess;

namespace PeaceMode;

public sealed partial class PeaceModePlugin
{
    private void BindNpcHooks()
    {
        Bind(() => On.Terraria.NPCSystemContext.NewNPC += OnNewNpc, () => On.Terraria.NPCSystemContext.NewNPC -= OnNewNpc);
        Bind(() => On.Terraria.NPCSystemContext.SpawnNPC += OnNaturalSpawn, () => On.Terraria.NPCSystemContext.SpawnNPC -= OnNaturalSpawn);
        Bind(() => On.Terraria.NPCSystemContext.SpawnOnPlayer += OnSpawnOnPlayer, () => On.Terraria.NPCSystemContext.SpawnOnPlayer -= OnSpawnOnPlayer);
        Bind(() => On.Terraria.NPCSystemContext.SpawnBoss += OnSpawnBoss, () => On.Terraria.NPCSystemContext.SpawnBoss -= OnSpawnBoss);
        Bind(() => On.Terraria.NPCSystemContext.SpawnWOF += OnSpawnWof, () => On.Terraria.NPCSystemContext.SpawnWOF -= OnSpawnWof);
        Bind(() => On.Terraria.NPCSystemContext.SpawnSkeletron += OnSpawnSkeletron, () => On.Terraria.NPCSystemContext.SpawnSkeletron -= OnSpawnSkeletron);
        Bind(() => On.Terraria.NPCSystemContext.SpawnMechQueen += OnSpawnMechQueen, () => On.Terraria.NPCSystemContext.SpawnMechQueen -= OnSpawnMechQueen);
        Bind(() => On.Terraria.NPCSystemContext.ReleaseNPC += OnReleaseNpc, () => On.Terraria.NPCSystemContext.ReleaseNPC -= OnReleaseNpc);
        Bind(() => On.OTAPI.HooksSystemContext.NPCSystemContext.InvokeSpawn += OnInvokeSpawn, () => On.OTAPI.HooksSystemContext.NPCSystemContext.InvokeSpawn -= OnInvokeSpawn);
        Bind(() => On.Terraria.NPC.OnSpawn += OnNpcSpawn, () => On.Terraria.NPC.OnSpawn -= OnNpcSpawn);
        Bind(() => On.Terraria.NPC.Transform += OnTransform, () => On.Terraria.NPC.Transform -= OnTransform);
        Bind(() => On.Terraria.NPC.UpdateNPC += OnUpdateNpc, () => On.Terraria.NPC.UpdateNPC -= OnUpdateNpc);
        Bind(() => On.Terraria.WorldGenSystemContext.SpawnTownNPC += OnSpawnTownNpc, () => On.Terraria.WorldGenSystemContext.SpawnTownNPC -= OnSpawnTownNpc);
        Bind(() => On.Terraria.WorldGenSystemContext.SpawnTravelNPC += OnSpawnTravelNpc, () => On.Terraria.WorldGenSystemContext.SpawnTravelNPC -= OnSpawnTravelNpc);
        Bind(() => On.Terraria.WorldGenSystemContext.SpawnHomelessNPC += OnSpawnHomelessNpc, () => On.Terraria.WorldGenSystemContext.SpawnHomelessNPC -= OnSpawnHomelessNpc);
    }

    private int OnNewNpc(On.Terraria.NPCSystemContext.orig_NewNPC orig, NPCSystemContext self,
        IEntitySource source, int x, int y, int type, int start, float ai0, float ai1, float ai2, float ai3, int target)
    {
        // 原版生成失败返回 maxNPCs；保留其已分配的哨兵槽，避免返回 -1 破坏调用者。
        return _enabled ? Main.maxNPCs : orig(self, source, x, y, type, start, ai0, ai1, ai2, ai3, target);
    }

    private void OnNaturalSpawn(On.Terraria.NPCSystemContext.orig_SpawnNPC orig, NPCSystemContext self)
    { if (!_enabled) orig(self); }

    private void OnSpawnOnPlayer(On.Terraria.NPCSystemContext.orig_SpawnOnPlayer orig, NPCSystemContext self,
        int player, int type, float ai0, float ai1, float ai2, float ai3)
    { if (!_enabled) orig(self, player, type, ai0, ai1, ai2, ai3); }

    private void OnSpawnBoss(On.Terraria.NPCSystemContext.orig_SpawnBoss orig, NPCSystemContext self,
        int x, int y, int type, int target, float ai0, float ai1, float ai2, float ai3)
    { if (!_enabled) orig(self, x, y, type, target, ai0, ai1, ai2, ai3); }

    private void OnSpawnWof(On.Terraria.NPCSystemContext.orig_SpawnWOF orig, NPCSystemContext self, Vector2 position)
    { if (!_enabled) orig(self, position); }

    private void OnSpawnSkeletron(On.Terraria.NPCSystemContext.orig_SpawnSkeletron orig, NPCSystemContext self, int player, bool redHatMode)
    { if (!_enabled) orig(self, player, redHatMode); }

    private bool OnSpawnMechQueen(On.Terraria.NPCSystemContext.orig_SpawnMechQueen orig, NPCSystemContext self, int player)
    { return !_enabled && orig(self, player); }

    private int OnReleaseNpc(On.Terraria.NPCSystemContext.orig_ReleaseNPC orig, NPCSystemContext self,
        int x, int y, int type, int style, int player)
    { return _enabled ? Main.maxNPCs : orig(self, x, y, type, style, player); }

    private bool OnInvokeSpawn(On.OTAPI.HooksSystemContext.NPCSystemContext.orig_InvokeSpawn orig,
        OTAPI.HooksSystemContext.NPCSystemContext self, ref int index)
    {
        if (!_enabled) return orig(self, ref index);
        if ((uint)index < (uint)self.root.Main.npc.Length && self.root.Main.npc[index] is { } npc)
            RemoveNpc(self.root, npc);
        return false;
    }

    private void OnNpcSpawn(On.Terraria.NPC.orig_OnSpawn orig, NPC self, RootContext root, IEntitySource source)
    {
        if (_enabled) RemoveNpc(root, self);
        else orig(self, root, source);
    }

    private void OnTransform(On.Terraria.NPC.orig_Transform orig, NPC self, RootContext root,
        int type, float ai0, float ai1, float ai2, float ai3, bool reposition)
    {
        if (_enabled) RemoveNpc(root, self);
        else orig(self, root, type, ai0, ai1, ai2, ai3, reposition);
    }

    private void OnUpdateNpc(On.Terraria.NPC.orig_UpdateNPC orig, NPC self, RootContext root, int index)
    {
        if (_enabled) RemoveNpc(root, self);
        else orig(self, root, index);
    }

    private TownNPCSpawnResult OnSpawnTownNpc(On.Terraria.WorldGenSystemContext.orig_SpawnTownNPC orig,
        WorldGenSystemContext self, int x, int y, bool canSpawn)
    { return _enabled ? TownNPCSpawnResult.Blocked : orig(self, x, y, canSpawn); }

    private void OnSpawnTravelNpc(On.Terraria.WorldGenSystemContext.orig_SpawnTravelNPC orig, WorldGenSystemContext self)
    { if (!_enabled) orig(self); }

    private void OnSpawnHomelessNpc(On.Terraria.WorldGenSystemContext.orig_SpawnHomelessNPC orig, WorldGenSystemContext self)
    { if (!_enabled) orig(self); }

    private static void RemoveNpc(RootContext root, NPC npc)
    {
        var wasActive = npc.active;
        npc.active = false;
        npc.life = 0;
        npc.netUpdate = false;
        npc.spawnNeedsSyncing = false;
        // 直接移除，不经过死亡、掉落或世界进度逻辑；只同步曾经可见的合法 NPC 槽。
        if (wasActive && Main.netMode == 2 && (uint)npc.whoAmI < (uint)Main.maxNPCs)
            root.NetMessage.SendData(23, number: npc.whoAmI);
    }

    private static void ClearNpcs(RootContext root)
    {
        if (root.Main.npc is not { } npcs) return;
        foreach (var npc in npcs)
        {
            if (npc is not null && (npc.active || npc.spawnNeedsSyncing || npc.netUpdate))
                RemoveNpc(root, npc);
        }
    }
}
