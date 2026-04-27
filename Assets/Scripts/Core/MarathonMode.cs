using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 40-wave marathon mode launcher + persistent run state.
/// Lives in static state so it survives the MainMenu → Gameplay scene load.
/// Wave generation, boss timing, buff-selection cadence and pity counter
/// all live here.
/// </summary>
public static class MarathonMode
{
    public const int TOTAL_WAVES        = 40;
    public const int BOSS_INTERVAL      = 10;  // boss every 10 waves
    public const int MINIBOSS_INTERVAL  = 5;   // miniboss every 5 waves (skipped on boss waves)
    public const int BUFF_INTERVAL      = 2;   // buff selection every 2 waves
    public const int HERO_PITY_LIMIT    = 5;   // guaranteed hero by the 5th selection

    public static bool LaunchRequested;
    public static bool IsActive;

    public static int CurrentWave;          // 1-based; 0 = pre-run
    public static int BuffSelectionsTaken;
    public static int PityCounter;          // resets when a hero is picked
    private static readonly List<string> pickedBuffHistory = new List<string>();

    public static IReadOnlyList<string> PickedBuffHistory => pickedBuffHistory;

    public static EnemyData[] EnemyPool;    // [0]=basic [1]=fast [2]=tank [3]=shielded [4]=stealth [5]=boss [6]=splitter [7]=shieldAura
    public static int SpawnPointCount = 1;

    /// <summary>How many spawn points are currently active. Grows with the
    /// current wave so early-game has only one entrance and the map opens up
    /// over time. Visual unlock is handled by <c>MarathonRunController</c>.</summary>
    public static int ActiveSpawnCount(int wave1Based)
    {
        // Progressive unlocks across the 6 marathon spawns.
        //   wave 1+  : 1 (left mid avenue)
        //   wave 5+  : 2 (+ top-left)
        //   wave 10+ : 3 (+ right-mid upper)
        //   wave 15+ : 4 (+ top-right)
        //   wave 25+ : 5 (+ right-mid lower)
        //   wave 35+ : 6 (+ bottom-right)
        int unlocked;
        if      (wave1Based >= 35) unlocked = 6;
        else if (wave1Based >= 25) unlocked = 5;
        else if (wave1Based >= 15) unlocked = 4;
        else if (wave1Based >= 10) unlocked = 3;
        else if (wave1Based >=  5) unlocked = 2;
        else                       unlocked = 1;
        return Mathf.Min(unlocked, Mathf.Max(1, SpawnPointCount));
    }

    /// <summary>Buff offers eligible to drop in this run. Filled by bootstrap.</summary>
    public static List<BuffOffer> BuffPool = new List<BuffOffer>();

    public static event Action<int> OnWaveChanged;
    public static event Action<int> OnBossWaveIncoming;

    static bool _hooked;

    public static void Launch()
    {
        LaunchRequested = true;
        IsActive = false;
        CurrentWave = 0;
        BuffSelectionsTaken = 0;
        PityCounter = 0;
        BuffPool.Clear();
        pickedBuffHistory.Clear();

        if (!_hooked)
        {
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        SceneManager.LoadScene("Gameplay");
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "Gameplay") return;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        _hooked = false;

        if (UnityEngine.Object.FindAnyObjectByType<QuickTestBootstrap>() != null) return;
        var go = new GameObject("--- Marathon QuickTest ---");
        go.AddComponent<QuickTestBootstrap>();
    }

    public static void ConsumeRequest() { LaunchRequested = false; }

    public static void EndRun()
    {
        IsActive = false;
        EnemyPool = null;
        BuffPool.Clear();
        pickedBuffHistory.Clear();
    }

    /// <summary>Generate the WaveData for round N (0-based). Boss waves drop
    /// the boss as the only enemy. Standard waves mix archetypes that have
    /// been unlocked by the wave number.</summary>
    public static WaveData GenerateWave(int round)
    {
        if (EnemyPool == null || EnemyPool.Length == 0) return null;

        int wave1Based = round + 1;
        CurrentWave = wave1Based;
        OnWaveChanged?.Invoke(wave1Based);

        bool isBossWave     = wave1Based % BOSS_INTERVAL == 0;
        bool isMinibossWave = !isBossWave && wave1Based % MINIBOSS_INTERVAL == 0;
        int sp = ActiveSpawnCount(wave1Based);

        var groups = new List<EnemyGroup>();
        // HP scaling — steeper than endless. Roughly +12% per wave linear,
        // plus a soft exponential kick after wave 20.
        float hpMul = 1f + (wave1Based - 1) * 0.12f;
        if (wave1Based > 20) hpMul *= 1f + (wave1Based - 20) * 0.04f;
        int   goldBonus = (wave1Based - 1) / 4;

        if (isBossWave && EnemyPool.Length > 5)
        {
            // Big boss: per-tier HP curve + ability set.
            int bossTier = wave1Based / BOSS_INTERVAL; // 1..4
            float bossHp = 6f + (bossTier - 1) * 5f;   // 6x, 11x, 16x, 21x base
            EnemyData boss = ScaledClone(EnemyPool[5], bossHp * hpMul / 2f, goldBonus * 8);
            boss.enemyName = BossNameForWave(wave1Based);
            ApplyBossAbilities(boss, bossTier, EnemyPool[0]);
            groups.Add(new EnemyGroup { enemyType = boss, count = 1 + (bossTier >= 3 ? 1 : 0),
                                        spawnInterval = 3f, spawnPointIndex = 0 });
            // A handful of grunts arriving with the boss.
            EnemyData minion = ScaledClone(EnemyPool[0], hpMul * 0.8f, 0);
            for (int i = 0; i < sp; i++)
                groups.Add(new EnemyGroup { enemyType = minion, count = 6 + bossTier * 2,
                                            spawnInterval = 0.7f, spawnPointIndex = i });
        }
        else if (isMinibossWave && EnemyPool.Length > 5)
        {
            // Miniboss: scaled-down boss with one ability based on wave.
            int mbTier = wave1Based / MINIBOSS_INTERVAL;
            float mbHp = 2.5f + (mbTier - 1) * 0.6f;
            EnemyData mb = ScaledClone(EnemyPool[5], mbHp * hpMul / 2f, goldBonus * 4);
            mb.enemyName = MinibossNameForWave(wave1Based);
            mb.bossScale = 1.7f;
            ApplyMinibossAbility(mb, mbTier);
            groups.Add(new EnemyGroup { enemyType = mb, count = 1,
                                        spawnInterval = 2f, spawnPointIndex = 0 });
            // Standard wave content also spawns alongside the miniboss for filler.
            EnemyData basic = ScaledClone(EnemyPool[0], hpMul, goldBonus);
            for (int i = 0; i < sp; i++)
                groups.Add(new EnemyGroup { enemyType = basic, count = 4 + wave1Based / 4,
                                            spawnInterval = 0.7f, spawnPointIndex = i });
        }
        else
        {
            // Basic
            int basicPerSpawn = 4 + wave1Based / 3;
            EnemyData basic = ScaledClone(EnemyPool[0], hpMul, goldBonus);
            for (int i = 0; i < sp; i++)
                groups.Add(new EnemyGroup { enemyType = basic, count = basicPerSpawn,
                                            spawnInterval = Mathf.Max(0.4f, 1f - wave1Based * 0.015f),
                                            spawnPointIndex = i });

            // Fast — wave 3+
            if (wave1Based >= 3 && EnemyPool.Length > 1)
            {
                EnemyData fast = ScaledClone(EnemyPool[1], hpMul, goldBonus);
                groups.Add(new EnemyGroup { enemyType = fast, count = 3 + wave1Based / 4,
                                            spawnInterval = 0.55f, spawnPointIndex = round % sp });
            }
            // Tank — wave 6+
            if (wave1Based >= 6 && EnemyPool.Length > 2)
            {
                EnemyData tank = ScaledClone(EnemyPool[2], hpMul * 1.1f, goldBonus * 2);
                groups.Add(new EnemyGroup { enemyType = tank, count = 1 + wave1Based / 8,
                                            spawnInterval = 1.4f, spawnPointIndex = (round + 1) % sp });
            }
            // Splitter — wave 8+
            if (wave1Based >= 8 && EnemyPool.Length > 6)
            {
                EnemyData splitter = ScaledClone(EnemyPool[6], hpMul, goldBonus);
                groups.Add(new EnemyGroup { enemyType = splitter, count = 2 + wave1Based / 10,
                                            spawnInterval = 1.2f, spawnPointIndex = round % sp });
            }
            // Shielded — wave 12+
            if (wave1Based >= 12 && EnemyPool.Length > 3)
            {
                EnemyData shielded = ScaledClone(EnemyPool[3], hpMul, goldBonus);
                groups.Add(new EnemyGroup { enemyType = shielded, count = 2 + wave1Based / 8,
                                            spawnInterval = 0.9f, spawnPointIndex = (round + 2) % sp });
            }
            // Shield-Aura support — wave 15+
            if (wave1Based >= 15 && EnemyPool.Length > 7)
            {
                EnemyData aura = ScaledClone(EnemyPool[7], hpMul, goldBonus * 2);
                groups.Add(new EnemyGroup { enemyType = aura, count = 1 + wave1Based / 14,
                                            spawnInterval = 1.5f, spawnPointIndex = round % sp });
            }
            // Stealth — wave 18+
            if (wave1Based >= 18 && EnemyPool.Length > 4)
            {
                EnemyData stealth = ScaledClone(EnemyPool[4], hpMul, goldBonus);
                groups.Add(new EnemyGroup { enemyType = stealth, count = 2 + wave1Based / 12,
                                            spawnInterval = 0.85f, spawnPointIndex = round % sp });
            }
        }

        // Telegraph upcoming boss for HUD use.
        int wavesUntilBoss = BOSS_INTERVAL - (wave1Based % BOSS_INTERVAL);
        if (wavesUntilBoss == BOSS_INTERVAL) wavesUntilBoss = 0;
        if (wavesUntilBoss == 1) OnBossWaveIncoming?.Invoke(wave1Based + 1);

        var wave = ScriptableObject.CreateInstance<WaveData>();
        wave.waveName = isBossWave
            ? $"Wave {wave1Based} \u2014 {BossNameForWave(wave1Based)}"
            : $"Wave {wave1Based}";
        wave.enemyGroups = groups.ToArray();
        wave.delayBetweenGroups = 0.7f;
        return wave;
    }

    public static string BossNameForWave(int wave1Based)
    {
        switch (wave1Based)
        {
            case 10: return "Midterm";
            case 20: return "Final Exam";
            case 30: return "FYP Defense";
            case 40: return "Graduation Ceremony";
            default: return "Boss";
        }
    }

    public static string MinibossNameForWave(int wave1Based)
    {
        switch (wave1Based)
        {
            case 5:  return "Pop Quiz";
            case 15: return "Group Project";
            case 25: return "Lab Report";
            case 35: return "Oral Presentation";
            default: return "Miniboss";
        }
    }

    /// <summary>Assign ability sets to the four big bosses.
    ///   Tier 1 (Midterm)    \u2014 Regen
    ///   Tier 2 (Final)      \u2014 Teleport
    ///   Tier 3 (FYP)        \u2014 Teleport + Enrage + Summon
    ///   Tier 4 (Graduation) \u2014 ALL abilities, faster cadence
    /// </summary>
    static void ApplyBossAbilities(EnemyData boss, int tier, EnemyData minionTemplate)
    {
        boss.summonTemplate = minionTemplate;
        switch (tier)
        {
            case 1:
                boss.bossAbilities    = BossAbilityFlags.Regen;
                boss.regenPerSecond   = Mathf.Max(8, boss.maxHealth / 80);
                break;
            case 2:
                boss.bossAbilities    = BossAbilityFlags.Teleport;
                boss.teleportInterval = 6f;
                boss.teleportSkipWaypoints = 2;
                break;
            case 3:
                boss.bossAbilities    = BossAbilityFlags.Teleport
                                      | BossAbilityFlags.Enrage
                                      | BossAbilityFlags.Summon;
                boss.teleportInterval = 7f;
                boss.teleportSkipWaypoints = 1;
                boss.enrageHpThreshold = 0.5f;
                boss.enrageSpeedMult   = 1.6f;
                boss.summonInterval    = 9f;
                boss.summonCount       = 2;
                break;
            default: // tier 4 (Graduation) and above
                boss.bossAbilities    = BossAbilityFlags.Teleport
                                      | BossAbilityFlags.Enrage
                                      | BossAbilityFlags.Regen
                                      | BossAbilityFlags.Summon;
                boss.teleportInterval = 5f;
                boss.teleportSkipWaypoints = 2;
                boss.enrageHpThreshold = 0.6f;
                boss.enrageSpeedMult   = 1.8f;
                boss.regenPerSecond   = Mathf.Max(12, boss.maxHealth / 60);
                boss.summonInterval   = 6f;
                boss.summonCount      = 3;
                break;
        }
    }

    /// <summary>Assign a single ability to a miniboss based on its tier
    /// (1: Pop Quiz \u2192 Regen, 2: Group Project \u2192 Teleport,
    ///  3: Lab Report \u2192 Enrage, 4+: Oral Presentation \u2192 Summon + Teleport).</summary>
    static void ApplyMinibossAbility(EnemyData mb, int tier)
    {
        switch (tier)
        {
            case 1:
                mb.bossAbilities = BossAbilityFlags.Regen;
                mb.regenPerSecond = Mathf.Max(4, mb.maxHealth / 120);
                break;
            case 2:
                mb.bossAbilities = BossAbilityFlags.Teleport;
                mb.teleportInterval = 7f;
                mb.teleportSkipWaypoints = 1;
                break;
            case 3:
                mb.bossAbilities = BossAbilityFlags.Enrage;
                mb.enrageHpThreshold = 0.4f;
                mb.enrageSpeedMult = 1.6f;
                break;
            default:
                mb.bossAbilities = BossAbilityFlags.Teleport | BossAbilityFlags.Summon;
                mb.teleportInterval = 8f;
                mb.teleportSkipWaypoints = 1;
                mb.summonInterval = 10f;
                mb.summonCount = 2;
                mb.summonTemplate = EnemyPool != null && EnemyPool.Length > 0 ? EnemyPool[0] : null;
                break;
        }
    }

    /// <summary>True if a buff selection should be triggered after the just-completed wave.</summary>
    public static bool ShouldOfferBuffsAfterWave(int wave1Based)
    {
        if (!IsActive) return false;
        if (wave1Based <= 0 || wave1Based > TOTAL_WAVES) return false;
        return wave1Based % BUFF_INTERVAL == 0;
    }

    /// <summary>Pick three offers using rarity weights, with hero-pity applied.</summary>
    public static List<BuffOffer> RollBuffOffers()
    {
        var pool = BuffPool;
        var picks = new List<BuffOffer>();
        if (pool == null || pool.Count == 0) return picks;

        bool guaranteeHero = PityCounter >= HERO_PITY_LIMIT - 1;
        bool heroIncluded = false;

        // Increase hero base weight as pity climbs.
        float heroWeightBoost = 1f + PityCounter * 0.5f;

        for (int slot = 0; slot < 3; slot++)
        {
            // Last slot of a guaranteed-hero offer: force-include a hero if missing.
            bool forceHero = guaranteeHero && !heroIncluded && slot == 2;
            var pick = WeightedPick(pool, forceHero, heroWeightBoost, picks);
            if (pick != null)
            {
                picks.Add(pick);
                if (pick.rarity == BuffRarity.Hero) heroIncluded = true;
            }
        }
        return picks;
    }

    static BuffOffer WeightedPick(List<BuffOffer> pool, bool forceHero, float heroWeightBoost,
                                  List<BuffOffer> already)
    {
        float total = 0f;
        var candidates = new List<BuffOffer>();
        foreach (var o in pool)
        {
            if (o == null) continue;
            if (already.Contains(o)) continue;
            if (forceHero && o.rarity != BuffRarity.Hero) continue;
            float w = WeightFor(o.rarity);
            if (o.rarity == BuffRarity.Hero) w *= heroWeightBoost;
            total += w;
            candidates.Add(o);
        }
        if (candidates.Count == 0) return null;
        float r = UnityEngine.Random.value * total;
        float acc = 0f;
        foreach (var o in candidates)
        {
            float w = WeightFor(o.rarity);
            if (o.rarity == BuffRarity.Hero) w *= heroWeightBoost;
            acc += w;
            if (r <= acc) return o;
        }
        return candidates[candidates.Count - 1];
    }

    static float WeightFor(BuffRarity r)
    {
        switch (r)
        {
            case BuffRarity.Common: return 60f;
            case BuffRarity.Rare:   return 25f;
            case BuffRarity.Epic:   return 12f;
            case BuffRarity.Hero:   return 3f;
            default: return 10f;
        }
    }

    public static void RecordBuffPicked(BuffOffer picked)
    {
        BuffSelectionsTaken++;
        if (picked == null) return;

        string title = string.IsNullOrWhiteSpace(picked.title)
            ? picked.kind.ToString()
            : picked.title;
        pickedBuffHistory.Add($"Wave {CurrentWave}: {title} ({picked.RarityLabel()})");

        if (picked.rarity == BuffRarity.Hero) PityCounter = 0;
        else PityCounter++;
        // One-shot offers (tower / hero unlocks) should never reappear once
        // picked — they have no further effect on subsequent picks.
        if (picked.kind == BuffOfferKind.UnlockTower ||
            picked.kind == BuffOfferKind.UnlockHero)
        {
            BuffPool.Remove(picked);
        }
    }

    static EnemyData ScaledClone(EnemyData src, float hpMul, int goldBonus)
    {
        var c = ScriptableObject.CreateInstance<EnemyData>();
        c.enemyName    = src.enemyName;
        c.moveSpeed    = src.moveSpeed;
        c.maxHealth    = Mathf.Max(1, Mathf.RoundToInt(src.maxHealth * hpMul));
        c.goldReward   = src.goldReward + goldBonus;
        c.courseTier   = src.courseTier;
        c.sprite       = src.sprite;
        c.animatorController = src.animatorController;
        c.flipWithDirection  = src.flipWithDirection;
        c.artFacesRight      = src.artFacesRight;
        c.visualScale        = src.visualScale;
        c.deathAnimatorOverride = src.deathAnimatorOverride;
        c.deathSpriteOverride   = src.deathSpriteOverride;
        c.deathFxDuration       = src.deathFxDuration;
        c.deathFxScale          = src.deathFxScale;
        c.archetype    = src.archetype;
        c.shieldHealth = Mathf.Max(0, Mathf.RoundToInt(src.shieldHealth * hpMul));
        c.bossScale    = src.bossScale;
        c.splitInto    = src.splitInto;
        c.splitCount   = src.splitCount;
        c.shieldAuraRadius   = src.shieldAuraRadius;
        c.shieldAuraAmount   = src.shieldAuraAmount;
        c.shieldAuraInterval = src.shieldAuraInterval;
        // Copy boss-ability defaults (callers may override after the clone).
        c.bossAbilities         = src.bossAbilities;
        c.teleportInterval      = src.teleportInterval;
        c.teleportSkipWaypoints = src.teleportSkipWaypoints;
        c.regenPerSecond        = src.regenPerSecond;
        c.enrageHpThreshold     = src.enrageHpThreshold;
        c.enrageSpeedMult       = src.enrageSpeedMult;
        c.summonInterval        = src.summonInterval;
        c.summonCount           = src.summonCount;
        c.summonTemplate        = src.summonTemplate;
        return c;
    }
}
