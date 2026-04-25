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
    public const int BUFF_INTERVAL      = 2;   // buff selection every 2 waves
    public const int HERO_PITY_LIMIT    = 5;   // guaranteed hero by the 5th selection

    public static bool LaunchRequested;
    public static bool IsActive;

    public static int CurrentWave;          // 1-based; 0 = pre-run
    public static int BuffSelectionsTaken;
    public static int PityCounter;          // resets when a hero is picked

    public static EnemyData[] EnemyPool;    // [0]=basic [1]=fast [2]=tank [3]=shielded [4]=stealth [5]=boss [6]=splitter [7]=shieldAura
    public static int SpawnPointCount = 1;

    /// <summary>How many spawn points are currently active. Grows with the
    /// current wave so early-game has only one entrance and the map opens up
    /// over time. Visual unlock is handled by <c>MarathonRunController</c>.</summary>
    public static int ActiveSpawnCount(int wave1Based)
    {
        if (wave1Based >= 25) return Mathf.Min(3, SpawnPointCount);
        if (wave1Based >= 10) return Mathf.Min(2, SpawnPointCount);
        return 1;
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

        bool isBossWave = wave1Based % BOSS_INTERVAL == 0;
        int sp = ActiveSpawnCount(wave1Based);

        var groups = new List<EnemyGroup>();
        float hpMul = 1f + (wave1Based - 1) * 0.06f;
        int   goldBonus = (wave1Based - 1) / 4;

        if (isBossWave && EnemyPool.Length > 5)
        {
            // Static boss difficulty per-tier (static = same for all players).
            int bossTier = wave1Based / BOSS_INTERVAL; // 1..4
            float bossHp = 1f + (bossTier - 1) * 0.6f;
            EnemyData boss = ScaledClone(EnemyPool[5], bossHp, goldBonus * 6);
            boss.enemyName = BossNameForWave(wave1Based);
            groups.Add(new EnemyGroup { enemyType = boss, count = 1 + (bossTier >= 3 ? 1 : 0),
                                        spawnInterval = 3f, spawnPointIndex = 0 });
            // A handful of grunts arriving with the boss.
            EnemyData minion = ScaledClone(EnemyPool[0], hpMul * 0.8f, 0);
            for (int i = 0; i < sp; i++)
                groups.Add(new EnemyGroup { enemyType = minion, count = 6 + bossTier * 2,
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
        if (picked != null && picked.rarity == BuffRarity.Hero) PityCounter = 0;
        else PityCounter++;
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
        c.archetype    = src.archetype;
        c.shieldHealth = Mathf.Max(0, Mathf.RoundToInt(src.shieldHealth * hpMul));
        c.bossScale    = src.bossScale;
        c.splitInto    = src.splitInto;
        c.splitCount   = src.splitCount;
        c.shieldAuraRadius   = src.shieldAuraRadius;
        c.shieldAuraAmount   = src.shieldAuraAmount;
        c.shieldAuraInterval = src.shieldAuraInterval;
        return c;
    }
}
