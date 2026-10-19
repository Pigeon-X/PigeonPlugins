using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.Events;
using UnifiedServerProcess;

namespace PeaceMode;

public sealed partial class PeaceModePlugin
{
    private void BindEventHooks()
    {
        Bind(() => On.Terraria.Main.Update += OnUpdate, () => On.Terraria.Main.Update -= OnUpdate);
        Bind(() => On.Terraria.NetplaySystemContext.StartServer += OnStartServer, () => On.Terraria.NetplaySystemContext.StartServer -= OnStartServer);
        Bind(() => On.Terraria.MainSystemContext.UpdateTime += OnUpdateTime, () => On.Terraria.MainSystemContext.UpdateTime -= OnUpdateTime);
        Bind(() => On.Terraria.MainSystemContext.UpdateTime_StartDay += OnStartDay, () => On.Terraria.MainSystemContext.UpdateTime_StartDay -= OnStartDay);
        Bind(() => On.Terraria.MainSystemContext.UpdateTime_StartNight += OnStartNight, () => On.Terraria.MainSystemContext.UpdateTime_StartNight -= OnStartNight);
        Bind(() => On.Terraria.MainSystemContext.UpdateTime_SpawnTownNPCs += OnTownSpawnUpdate, () => On.Terraria.MainSystemContext.UpdateTime_SpawnTownNPCs -= OnTownSpawnUpdate);
        Bind(() => On.Terraria.MainSystemContext.CanStartInvasion += OnCanStartInvasion, () => On.Terraria.MainSystemContext.CanStartInvasion -= OnCanStartInvasion);
        Bind(() => On.Terraria.MainSystemContext.StartInvasion += OnStartInvasion, () => On.Terraria.MainSystemContext.StartInvasion -= OnStartInvasion);
        Bind(() => On.Terraria.MainSystemContext.UpdateInvasion += OnUpdateInvasion, () => On.Terraria.MainSystemContext.UpdateInvasion -= OnUpdateInvasion);
        Bind(() => On.Terraria.MainSystemContext.startPumpkinMoon += OnPumpkinMoon, () => On.Terraria.MainSystemContext.startPumpkinMoon -= OnPumpkinMoon);
        Bind(() => On.Terraria.MainSystemContext.startSnowMoon += OnSnowMoon, () => On.Terraria.MainSystemContext.startSnowMoon -= OnSnowMoon);
        Bind(() => On.Terraria.MainSystemContext.StartSlimeRain += OnSlimeRain, () => On.Terraria.MainSystemContext.StartSlimeRain -= OnSlimeRain);
        Bind(() => On.Terraria.MainSystemContext.UpdateSlimeRainWarning += OnSlimeWarning, () => On.Terraria.MainSystemContext.UpdateSlimeRainWarning -= OnSlimeWarning);
        Bind(() => On.Terraria.MainSystemContext.StartRain += OnRain, () => On.Terraria.MainSystemContext.StartRain -= OnRain);
        Bind(() => On.Terraria.WorldGenSystemContext.dropMeteor += OnDropMeteor, () => On.Terraria.WorldGenSystemContext.dropMeteor -= OnDropMeteor);
        Bind(() => On.Terraria.WorldGenSystemContext.meteor += OnMeteor, () => On.Terraria.WorldGenSystemContext.meteor -= OnMeteor);
        Bind(() => On.Terraria.WorldGenSystemContext.StartMeteorShower += OnMeteorShower, () => On.Terraria.WorldGenSystemContext.StartMeteorShower -= OnMeteorShower);
        Bind(() => On.Terraria.WorldGenSystemContext.StartImpendingDoom += OnImpendingDoom, () => On.Terraria.WorldGenSystemContext.StartImpendingDoom -= OnImpendingDoom);
        Bind(() => On.Terraria.WorldGenSystemContext.TriggerLunarApocalypse += OnLunar, () => On.Terraria.WorldGenSystemContext.TriggerLunarApocalypse -= OnLunar);
        Bind(() => On.Terraria.WorldGenSystemContext.UpdateLunarApocalypse += OnLunarUpdate, () => On.Terraria.WorldGenSystemContext.UpdateLunarApocalypse -= OnLunarUpdate);
        Bind(() => On.Terraria.GameContent.Events.DD2EventSystemContext.StartInvasion += OnDd2Start, () => On.Terraria.GameContent.Events.DD2EventSystemContext.StartInvasion -= OnDd2Start);
        Bind(() => On.Terraria.GameContent.Events.DD2EventSystemContext.SummonCrystal += OnCrystal, () => On.Terraria.GameContent.Events.DD2EventSystemContext.SummonCrystal -= OnCrystal);
        Bind(() => On.Terraria.GameContent.Events.DD2EventSystemContext.SummonCrystalDirect += OnCrystalDirect, () => On.Terraria.GameContent.Events.DD2EventSystemContext.SummonCrystalDirect -= OnCrystalDirect);
        Bind(() => On.Terraria.GameContent.Events.DD2EventSystemContext.UpdateTime += OnDd2Update, () => On.Terraria.GameContent.Events.DD2EventSystemContext.UpdateTime -= OnDd2Update);
        Bind(() => On.Terraria.GameContent.Events.SandstormSystemContext.StartSandstorm += OnSandstorm, () => On.Terraria.GameContent.Events.SandstormSystemContext.StartSandstorm -= OnSandstorm);
        Bind(() => On.Terraria.GameContent.Events.SandstormSystemContext.UpdateTime += OnSandstormUpdate, () => On.Terraria.GameContent.Events.SandstormSystemContext.UpdateTime -= OnSandstormUpdate);
        Bind(() => On.Terraria.GameContent.Events.LanternNightSystemContext.NaturalAttempt += OnNaturalLantern, () => On.Terraria.GameContent.Events.LanternNightSystemContext.NaturalAttempt -= OnNaturalLantern);
        Bind(() => On.Terraria.GameContent.Events.BirthdayPartySystemContext.NaturalAttempt += OnNaturalParty, () => On.Terraria.GameContent.Events.BirthdayPartySystemContext.NaturalAttempt -= OnNaturalParty);
        Bind(() => On.Terraria.GameContent.Events.LanternNightSystemContext.ToggleManualLanterns += OnLantern, () => On.Terraria.GameContent.Events.LanternNightSystemContext.ToggleManualLanterns -= OnLantern);
        Bind(() => On.Terraria.GameContent.Events.LanternNightSystemContext.UpdateTime += OnLanternUpdate, () => On.Terraria.GameContent.Events.LanternNightSystemContext.UpdateTime -= OnLanternUpdate);
        Bind(() => On.Terraria.GameContent.Events.BirthdayPartySystemContext.ToggleManualParty += OnParty, () => On.Terraria.GameContent.Events.BirthdayPartySystemContext.ToggleManualParty -= OnParty);
        Bind(() => On.Terraria.GameContent.Events.BirthdayPartySystemContext.UpdateTime += OnPartyUpdate, () => On.Terraria.GameContent.Events.BirthdayPartySystemContext.UpdateTime -= OnPartyUpdate);
        Bind(() => On.Terraria.GameContent.Events.CultistRitualSystemContext.UpdateTime += OnCultistUpdate, () => On.Terraria.GameContent.Events.CultistRitualSystemContext.UpdateTime -= OnCultistUpdate);
        Bind(() => On.Terraria.GameContent.Events.CultistRitualSystemContext.TrySpawning += OnCultistSpawn, () => On.Terraria.GameContent.Events.CultistRitualSystemContext.TrySpawning -= OnCultistSpawn);
        Bind(() => On.Terraria.GameContent.Events.CreditsRollEventSystemContext.TryStartingCreditsRoll += OnCredits, () => On.Terraria.GameContent.Events.CreditsRollEventSystemContext.TryStartingCreditsRoll -= OnCredits);
        Bind(() => On.Terraria.GameContent.Events.CreditsRollEventSystemContext.UpdateTime += OnCreditsUpdate, () => On.Terraria.GameContent.Events.CreditsRollEventSystemContext.UpdateTime -= OnCreditsUpdate);
        Bind(() => On.Terraria.GameContent.Events.MysticLogFairiesEvent.UpdateTime += OnFairies, () => On.Terraria.GameContent.Events.MysticLogFairiesEvent.UpdateTime -= OnFairies);
    }

    private void OnStartServer(On.Terraria.NetplaySystemContext.orig_StartServer orig, NetplaySystemContext self)
    {
        orig(self);
        if (_enabled) Enforce(self.root);
    }

    private void OnUpdate(On.Terraria.Main.orig_Update orig, Main self, RootContext root, GameTime time)
    {
        if (_enabled) Enforce(root);
        orig(self, root, time);
        if (_enabled) Enforce(root);
    }

    private void OnUpdateTime(On.Terraria.MainSystemContext.orig_UpdateTime orig, MainSystemContext self)
    {
        if (_enabled) SyncClearedEvents(self.root);
        orig(self);
        if (_enabled) SyncClearedEvents(self.root);
    }

    private void OnStartDay(On.Terraria.MainSystemContext.orig_UpdateTime_StartDay orig, MainSystemContext self, ref bool stopEvents)
    {
        if (_enabled)
        {
            SyncClearedEvents(self.root);
            stopEvents = true;
        }
        orig(self, ref stopEvents);
        if (_enabled) SyncClearedEvents(self.root);
    }

    private void OnStartNight(On.Terraria.MainSystemContext.orig_UpdateTime_StartNight orig, MainSystemContext self, ref bool stopEvents)
    {
        if (_enabled)
        {
            SyncClearedEvents(self.root);
            stopEvents = true;
        }
        orig(self, ref stopEvents);
        if (_enabled) SyncClearedEvents(self.root);
    }

    private void OnTownSpawnUpdate(On.Terraria.MainSystemContext.orig_UpdateTime_SpawnTownNPCs orig, MainSystemContext self)
    { if (!_enabled) orig(self); }
    private bool OnCanStartInvasion(On.Terraria.MainSystemContext.orig_CanStartInvasion orig, MainSystemContext self, int type, bool ignoreDelay)
    { return !_enabled && orig(self, type, ignoreDelay); }
    private void OnStartInvasion(On.Terraria.MainSystemContext.orig_StartInvasion orig, MainSystemContext self, int type)
    { if (!_enabled) orig(self, type); }
    private void OnUpdateInvasion(On.Terraria.MainSystemContext.orig_UpdateInvasion orig, MainSystemContext self)
    { if (_enabled) SyncClearedEvents(self.root); else orig(self); }
    private void OnPumpkinMoon(On.Terraria.MainSystemContext.orig_startPumpkinMoon orig, MainSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnSnowMoon(On.Terraria.MainSystemContext.orig_startSnowMoon orig, MainSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnSlimeRain(On.Terraria.MainSystemContext.orig_StartSlimeRain orig, MainSystemContext self, bool announce)
    { if (!_enabled) orig(self, announce); }
    private void OnSlimeWarning(On.Terraria.MainSystemContext.orig_UpdateSlimeRainWarning orig, MainSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnRain(On.Terraria.MainSystemContext.orig_StartRain orig, MainSystemContext self, bool instant, float? strengthOverride, bool guaranteeCoinRain)
    { if (!_enabled) orig(self, instant, strengthOverride, guaranteeCoinRain); }
    private bool OnDropMeteor(On.Terraria.WorldGenSystemContext.orig_dropMeteor orig, WorldGenSystemContext self, bool underground)
    { return !_enabled && orig(self, underground); }
    private bool OnMeteor(On.Terraria.WorldGenSystemContext.orig_meteor orig, WorldGenSystemContext self, int x, int y, bool ignorePlayers)
    { return !_enabled && orig(self, x, y, ignorePlayers); }
    private void OnMeteorShower(On.Terraria.WorldGenSystemContext.orig_StartMeteorShower orig, WorldGenSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnImpendingDoom(On.Terraria.WorldGenSystemContext.orig_StartImpendingDoom orig, WorldGenSystemContext self, int countdown)
    { if (!_enabled) orig(self, countdown); }
    private void OnLunar(On.Terraria.WorldGenSystemContext.orig_TriggerLunarApocalypse orig, WorldGenSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnLunarUpdate(On.Terraria.WorldGenSystemContext.orig_UpdateLunarApocalypse orig, WorldGenSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnDd2Start(On.Terraria.GameContent.Events.DD2EventSystemContext.orig_StartInvasion orig, DD2EventSystemContext self, int difficulty)
    { if (!_enabled) orig(self, difficulty); }
    private void OnCrystal(On.Terraria.GameContent.Events.DD2EventSystemContext.orig_SummonCrystal orig, DD2EventSystemContext self, int x, int y, int player)
    { if (!_enabled) orig(self, x, y, player); }
    private void OnCrystalDirect(On.Terraria.GameContent.Events.DD2EventSystemContext.orig_SummonCrystalDirect orig, DD2EventSystemContext self, int x, int y, int player)
    { if (!_enabled) orig(self, x, y, player); }
    private void OnDd2Update(On.Terraria.GameContent.Events.DD2EventSystemContext.orig_UpdateTime orig, DD2EventSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnSandstorm(On.Terraria.GameContent.Events.SandstormSystemContext.orig_StartSandstorm orig, SandstormSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnSandstormUpdate(On.Terraria.GameContent.Events.SandstormSystemContext.orig_UpdateTime orig, SandstormSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnNaturalLantern(On.Terraria.GameContent.Events.LanternNightSystemContext.orig_NaturalAttempt orig, LanternNightSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnNaturalParty(On.Terraria.GameContent.Events.BirthdayPartySystemContext.orig_NaturalAttempt orig, BirthdayPartySystemContext self)
    { if (!_enabled) orig(self); }
    private void OnLantern(On.Terraria.GameContent.Events.LanternNightSystemContext.orig_ToggleManualLanterns orig, LanternNightSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnLanternUpdate(On.Terraria.GameContent.Events.LanternNightSystemContext.orig_UpdateTime orig, LanternNightSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnParty(On.Terraria.GameContent.Events.BirthdayPartySystemContext.orig_ToggleManualParty orig, BirthdayPartySystemContext self)
    { if (!_enabled) orig(self); }
    private void OnPartyUpdate(On.Terraria.GameContent.Events.BirthdayPartySystemContext.orig_UpdateTime orig, BirthdayPartySystemContext self)
    { if (!_enabled) orig(self); }
    private void OnCultistUpdate(On.Terraria.GameContent.Events.CultistRitualSystemContext.orig_UpdateTime orig, CultistRitualSystemContext self)
    { if (!_enabled) orig(self); }
    private bool OnCultistSpawn(On.Terraria.GameContent.Events.CultistRitualSystemContext.orig_TrySpawning orig, CultistRitualSystemContext self, int x, int y, bool force)
    { return !_enabled && orig(self, x, y, force); }
    private void OnCredits(On.Terraria.GameContent.Events.CreditsRollEventSystemContext.orig_TryStartingCreditsRoll orig, CreditsRollEventSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnCreditsUpdate(On.Terraria.GameContent.Events.CreditsRollEventSystemContext.orig_UpdateTime orig, CreditsRollEventSystemContext self)
    { if (!_enabled) orig(self); }
    private void OnFairies(On.Terraria.GameContent.Events.MysticLogFairiesEvent.orig_UpdateTime orig, MysticLogFairiesEvent self, RootContext root)
    { if (!_enabled) orig(self, root); }

    private static void Enforce(RootContext root)
    {
        ClearNpcs(root);
        SyncClearedEvents(root);
    }

    private static void SyncClearedEvents(RootContext root)
    {
        var hadDd2 = root.DD2Event.Ongoing;
        var hadProgress = hadDd2 || root.Main.invasionType != 0 || root.Main.invasionProgressDisplayLeft > 0 ||
            root.Main.pumpkinMoon || root.Main.snowMoon;
        var hadLunar = root.NPC.LunarApocalypseIsUp || root.NPC.TowerActiveSolar || root.NPC.TowerActiveVortex ||
            root.NPC.TowerActiveNebula || root.NPC.TowerActiveStardust || root.NPC.ShieldStrengthTowerSolar != 0 ||
            root.NPC.ShieldStrengthTowerVortex != 0 || root.NPC.ShieldStrengthTowerNebula != 0 || root.NPC.ShieldStrengthTowerStardust != 0;
        var hadCountdown = root.NPC.MoonLordCountdown > 0;
        var hadCredits = root.CreditsRollEvent.IsEventOngoing;
        var changed = ClearEvents(root);
        if (Main.netMode != 2) return;
        if (changed || hadLunar || hadCountdown) root.NetMessage.SendData(7);
        // 已经在进行中的事件还拥有独立客户端状态，仅 WorldData 不足以清除。
        if (hadDd2)
        {
            root.NetMessage.SendData(114);
            root.NetMessage.SendData(116, number: 0);
        }
        if (hadLunar) root.NetMessage.SendData(101);
        if (hadCountdown) root.NetMessage.SendData(103);
        if (hadCredits) root.NetMessage.SendData(140, number: 0, number2: 0);
        // progress=-1 是原版隐藏进度条的哨兵；发送全零反而会短暂显示空进度条。
        if (hadProgress) root.NetMessage.SendData(78, number: -1, number2: 1, number3: 0, number4: 0);
    }

    private static bool ClearEvents(RootContext root)
    {
        var main = root.Main;
        var changed = main.invasionType != 0 || main.invasionSize != 0 || main.invasionProgressDisplayLeft != 0 ||
            main.bloodMoon || main.eclipse || main.pumpkinMoon || main.snowMoon || main.slimeRain ||
            main.raining || main.maxRaining != 0 || main.windSpeedTarget != 0 ||
            root.NPC.LunarApocalypseIsUp || root.DD2Event.Ongoing || root.Sandstorm.Happening ||
            root.BirthdayParty.PartyIsUp || root.LanternNight.LanternsUp || root.CreditsRollEvent.IsEventOngoing;

        main.invasionType = main.invasionSize = main.invasionDelay = main.invasionWarn = 0;
        main.invasionSizeStart = main.invasionProgress = main.invasionProgressMax = main.invasionProgressWave = 0;
        main.invasionProgressIcon = main.invasionProgressDisplayLeft = 0;
        main.invasionProgressAlpha = 0;
        main.bloodMoon = main.eclipse = main.pumpkinMoon = main.snowMoon = false;
        main.slimeRain = false;
        main.slimeRainTime = 0;
        main.slimeWarningTime = main.slimeRainKillCount = 0;
        main.raining = false;
        main.rainTime = main.coinRain = 0;
        main.maxRaining = main.oldMaxRaining = 0;
        main.windSpeedCurrent = main.windSpeedTarget = 0;
        // 保持风力重掷计时未到期，避免每帧随机风力再被清理而反复同步。
        main.windCounter = 1800;
        main.extremeWindCounter = 20;
        main.afterPartyOfDoom = false;
        root.WorldGen.spawnEye = root.WorldGen.spawnMeteor = false;
        root.WorldGen.spawnHardBoss = root.WorldGen.meteorShowerCount = 0;
        root.NPC.MoonLordCountdown = root.NPC.MaxMoonLordCountdown = 0;
        root.NPC.totalInvasionPoints = root.NPC.waveKills = 0;
        root.NPC.waveNumber = 0;
        root.NPC.LunarApocalypseIsUp = false;
        root.NPC.TowerActiveSolar = root.NPC.TowerActiveVortex = root.NPC.TowerActiveNebula = root.NPC.TowerActiveStardust = false;
        root.NPC.ShieldStrengthTowerSolar = root.NPC.ShieldStrengthTowerVortex = root.NPC.ShieldStrengthTowerNebula = root.NPC.ShieldStrengthTowerStardust = 0;
        root.DD2Event.Ongoing = false;
        root.DD2Event.TimeLeftBetweenWaves = 0;
        root.Sandstorm.Happening = false;
        root.Sandstorm.TimeLeft = 0;
        root.Sandstorm.Severity = root.Sandstorm.IntendedSeverity = 0;
        root.LanternNight.ManualLanterns = root.LanternNight.GenuineLanterns = root.LanternNight.NextNightIsLanternNight = false;
        root.LanternNight._wasLanternNight = false;
        root.BirthdayParty.ManualParty = root.BirthdayParty.GenuineParty = root.BirthdayParty._wasCelebrating = false;
        root.BirthdayParty.CelebratingNPCs.Clear();
        root.CreditsRollEvent.Reset();
        return changed;
    }
}
